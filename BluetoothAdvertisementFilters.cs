using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Advertisement;

namespace HiddenListener
{
    /// <summary>
    /// This base class represents a generic advertisement filter
    /// </summary>
    public abstract class BTAdvertisementFilterBase
    {
        /// <summary>
        /// This function tests an adv packet to determine whether it should be matched by thie filter
        /// </summary>
        /// <param name="adv">advertising packet to test</param>
        /// <returns>true if the advertising packet is matched by this filter</returns>
        public abstract bool PacketMatches(BluetoothLEAdvertisement adv);
    }

    /// <summary>
    /// This filter implementation selects advertising packets with the eddystone service guid
    /// </summary>
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

    /// <summary>
    /// This filter implementation selects advertising packets with apple manufacturer data and some key bytes
    /// </summary>
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
}
