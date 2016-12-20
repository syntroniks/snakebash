using System;
using Prism.Mvvm;
using Windows.Devices.Bluetooth.Advertisement;
using System.Linq;

namespace HiddenListener
{
    public class HiddenListenerData : BindableBase
    {
        private string _address;
        public string Address
        {
            get { return _address; }
            set { SetProperty(ref _address, value); }
        }

        private DateTimeOffset _timeCaptured;
        public DateTimeOffset TimeCaptured
        {
            get { return _timeCaptured; }
            set { SetProperty(ref _timeCaptured, value); }
        }

        private int _rssi;
        public int RSSI
        {
            get { return _rssi; }
            set { SetProperty(ref _rssi, value); }
        }

        private string _beaconType;
        public string BeaconType
        {
            get { return _beaconType; }
            set { SetProperty(ref _beaconType, value); }
        }

        public HiddenListenerData(BluetoothLEAdvertisementReceivedEventArgs args)
            : base()
        {
            // reformat the address to a more familiar format
            var macBytes = BitConverter.GetBytes(args.BluetoothAddress).Take(6).ToArray();
            var macString = BitConverter.ToString(macBytes).Replace('-', ':');
            Address = macString;
            TimeCaptured = args.Timestamp;
            RSSI = args.RawSignalStrengthInDBm;
        }
        
        public override int GetHashCode()
        {
            return _address.GetHashCode() ^ _timeCaptured.GetHashCode() ^ _rssi.GetHashCode() ^ _beaconType.GetHashCode();
        }
    }

    public class EddystoneHiddenListenerData : HiddenListenerData
    {
        public EddystoneHiddenListenerData(BluetoothLEAdvertisementReceivedEventArgs args)
            : base(args)
        {
            BeaconType = "EDDYSTONE";
        }
    }

    public class iBeaconHiddenListenerData : HiddenListenerData
    {
        public iBeaconHiddenListenerData(BluetoothLEAdvertisementReceivedEventArgs args)
            : base(args)
        {
            BeaconType = "IBEACON";
        }
    }
}