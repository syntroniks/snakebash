using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Advertisement;

namespace HiddenListener
{
    public abstract class BTAdvertisementFilterBase
    {
        public abstract bool PacketMatches(BluetoothLEAdvertisement adv);
    }

    public sealed class EddystoneAdvertisementFilter : BTAdvertisementFilterBase
    {
        private readonly Guid EDDYSTONE_SERVICE_GUID = new Guid("0000feaa-0000-1000-8000-00805f9b34fb");

        public override bool PacketMatches(BluetoothLEAdvertisement adv)
        {
            if (adv.ServiceUuids.Count > 0 &&
                adv.ServiceUuids.Contains(EDDYSTONE_SERVICE_GUID))
            {
                // device is eddystone device
                return true;
            }
            return false;
        }
    }

    public sealed class iBeaconAdvertisementFilter : BTAdvertisementFilterBase
    {
        private readonly short APPLE_COMPANY_ID = 0x004C;
        private readonly byte[] APPLE_IBEACON_SELECTOR = new byte[] { 0x02, 0x15 };

        public override bool PacketMatches(BluetoothLEAdvertisement adv)
        {
            if (adv.ManufacturerData.Count > 0)
            {
                // Select Apple branded manufacturer specific data
                // that has a beacon identifying sequence in it
                var matchingManufacturerData = adv.ManufacturerData.Where((n) =>
                {
                    // we have apple data -- let's make sure it is a beacon type
                    if (n.CompanyId == APPLE_COMPANY_ID)
                    {
                        // data length has to include some magic bytes (tag & data length) to indicate that it is a beacon
                        if (n.Data.Length >= 2)
                        {
                            return n.Data.ToArray(0, 2).SequenceEqual(APPLE_IBEACON_SELECTOR);
                        }
                    }
                    // default case is to reject
                    return false;
                });

                // If we got at least one match, mark this device as OK to go through
                if (matchingManufacturerData.Count() > 0)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public class BluetoothAdvertisementsModel : BindableBase, IDisposable
    {
        // need advert filters
        private List<BTAdvertisementFilterBase> advertFilters = new List<BTAdvertisementFilterBase>();
        private BluetoothLEAdvertisementWatcher bluetoothLEAdvertisementWatcher = new BluetoothLEAdvertisementWatcher();
        private Dictionary<ulong, BluetoothLEAdvertisementReceivedEventArgs> btAddressToLatestAdvertisementEventMap = new Dictionary<ulong, BluetoothLEAdvertisementReceivedEventArgs>();

        public BluetoothAdvertisementsModel()
        {
            advertFilters.Add(new EddystoneAdvertisementFilter());
            advertFilters.Add(new iBeaconAdvertisementFilter());

            bluetoothLEAdvertisementWatcher.Received += BluetoothLEAdvertisementWatcher_Received;
            bluetoothLEAdvertisementWatcher.Start();
        }

        private void BluetoothLEAdvertisementWatcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
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
