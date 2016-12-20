using Prism.Mvvm;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.UI.Xaml.Data;

namespace HiddenListener
{
    public class BluetoothAdvertisementsModel : BindableBase, IDisposable
    {
        private readonly TimeSpan UPLOAD_INTERVAL = TimeSpan.FromSeconds(30);

        // Advertisement filters select matching packets
        private List<BTAdvertisementFilterBase> advertFilters = new List<BTAdvertisementFilterBase>();

        // The advertisement watcher listenes to BLE advertisements and reports them to our application
        private BluetoothLEAdvertisementWatcher bluetoothLEAdvertisementWatcher = new BluetoothLEAdvertisementWatcher();

        // this address -> advertisement data map lets us associate advertisement data with a bt address
        // (note: eddystone addresses change, and may/must be resolved with eids if you need to track them)
        // we will be accessing this concurrent dictionary from multiple threads, so just use a concurrent one.
        private ConcurrentDictionary<ulong, BluetoothLEAdvertisementReceivedEventArgs> btAddressToLatestAdvertisementEventMap = new ConcurrentDictionary<ulong, BluetoothLEAdvertisementReceivedEventArgs>();

        private ObservableCollection<BluetoothLEAdvertisementReceivedEventArgs> _filteredScans = new ObservableCollection<BluetoothLEAdvertisementReceivedEventArgs>();

        public ObservableCollection<BluetoothLEAdvertisementReceivedEventArgs> FilteredScans
        {
            get { return _filteredScans; }
            set { SetProperty(ref _filteredScans, value); }
        }


        // periodic (30 second) timer tries to upload scan results to a web service
        private ThreadPoolTimer periodicTimer;

        private async void PeriodicEventUpload(ThreadPoolTimer timer)
        {
            // upload
            var publicKey = "RMxw8yD6KATwDjDg9jD3";
            var privateKey = "lzE1VB25ebfBpoprzop9"; // mac, misc_data, timestamp

            var baseUri = new Uri("http://data.sparkfun.com/input/");
            var streamUri = new Uri(baseUri, publicKey);

            // All the matched advertisement packets in this interval
            var valuesArray = this.btAddressToLatestAdvertisementEventMap.Values.ToArray();

            for (int i = 0; i < valuesArray.Length; i++)
            {
                var item = valuesArray[i];
                // reformat the address to a more familiar format
                var macBytes = BitConverter.GetBytes(item.BluetoothAddress).Take(6).ToArray();
                var macString = BitConverter.ToString(macBytes);
                var queryParameters = $"?private_key={privateKey}&mac={macString}" +
                                      $"&timestamp={item.Timestamp.ToUnixTimeSeconds()}" +
                                      $"&type={GetDeviceType(item.Advertisement)}&rssi={item.RawSignalStrengthInDBm}&misc_data=0";

                HttpClient http = new System.Net.Http.HttpClient();
                Debug.WriteLine($"Sending data to phant stream {streamUri.ToString() + queryParameters}");
                HttpResponseMessage response = await http.GetAsync(streamUri.ToString() + queryParameters);
                if (response.StatusCode == System.Net.HttpStatusCode.OK ||
                    response.StatusCode == System.Net.HttpStatusCode.Accepted ||
                    response.StatusCode == System.Net.HttpStatusCode.Created)
                {
                    // this element has been uploaded, it is now safe to remove
                    btAddressToLatestAdvertisementEventMap[item.BluetoothAddress] = null;
                }
            }
        }

        private string GetDeviceType(BluetoothLEAdvertisement advertisement)
        {
            if (new EddystoneAdvertisementFilter().PacketMatches(advertisement))
            {
                return "EDDYSTONE";
            }
            else if (new iBeaconAdvertisementFilter().PacketMatches(advertisement))
            {
                return "IBEACON";
            }
            else
            {
                return "";
            }
        }

        public BluetoothAdvertisementsModel()
        {
            advertFilters.Add(new EddystoneAdvertisementFilter());
            advertFilters.Add(new iBeaconAdvertisementFilter());

            bluetoothLEAdvertisementWatcher.Received += BluetoothLEAdvertisementWatcher_Received;
            bluetoothLEAdvertisementWatcher.Start();

            periodicTimer = ThreadPoolTimer.CreatePeriodicTimer(PeriodicEventUpload, UPLOAD_INTERVAL);
        }

        private void BluetoothLEAdvertisementWatcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            // give each of the filters a chance to flag a packet for further processing
            for (int i = 0; i < advertFilters.Count; i++)
            {
                if (advertFilters[i].PacketMatches(args.Advertisement))
                {
                    // we got a hit, save it and break out
                    btAddressToLatestAdvertisementEventMap[args.BluetoothAddress] = args;
                    break;
                }
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    bluetoothLEAdvertisementWatcher.Received -= BluetoothLEAdvertisementWatcher_Received;
                }
                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
