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
        
        private HashSet<HiddenListenerData> _scanData = new HashSet<HiddenListenerData>();
        
        // periodic (30 second) timer tries to upload scan results to a web service
        private ThreadPoolTimer periodicTimer;

        private DataUploaderBase dataUploader = new LogglyDataUploader(new Uri("http://logs-01.loggly.com/inputs/8bb73a13-dda0-4260-ba2d-9038b09e6091/tag/http/"));
        //string phantPublicKey = "RMxw8yD6KATwDjDg9jD3";
        //string phantPrivateKey = "lzE1VB25ebfBpoprzop9"; // mac, misc_data, timestamp
        //Uri phantBaseUri = new Uri("http://data.sparkfun.com/input/");
        //private DataUploaderBase dataUploader = new new PhantDataUploader(phantPublicKey, phantPrivateKey, phantBaseUri);

        public BluetoothAdvertisementsModel()
        {
            advertFilters.Add(new EddystoneAdvertisementFilter());
            advertFilters.Add(new iBeaconAdvertisementFilter());

            bluetoothLEAdvertisementWatcher.Received += BluetoothLEAdvertisementWatcher_Received;
            bluetoothLEAdvertisementWatcher.Start();

            periodicTimer = ThreadPoolTimer.CreatePeriodicTimer(PeriodicEventUpload, UPLOAD_INTERVAL);
        }
        private async void PeriodicEventUpload(ThreadPoolTimer timer)
        {
            // upload
            await dataUploader.Upload(_scanData);
            _scanData.Clear();
            return;
        }

        private void BluetoothLEAdvertisementWatcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            // give each of the filters a chance to flag a packet for further processing
            for (int i = 0; i < advertFilters.Count; i++)
            {
                if (advertFilters[i].PacketMatches(args.Advertisement))
                {
                    if (advertFilters[i].GetType() == typeof(iBeaconAdvertisementFilter))
                    {
                        var candidateData = new iBeaconHiddenListenerData(args);
                        if (_scanData.Where((n) => n.Address == candidateData.Address ).Count() == 0)
                        {
                            _scanData.Add(candidateData);
                        }
                    }
                    else if (advertFilters[i].GetType() == typeof(EddystoneAdvertisementFilter))
                    {
                        var candidateData = new EddystoneHiddenListenerData(args);
                        if (_scanData.Where((n) => n.Address == candidateData.Address).Count() == 0)
                        {
                            _scanData.Add(candidateData);
                        }
                    }
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
