//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace HiddenListener
{
    // This scenario uses a DeviceWatcher to enumerate nearby Bluetooth Low Energy devices,
    // displays them in a ListView, and lets the user select a device and pair it.
    // This device will be used by future scenarios.
    // For more information about device discovery and pairing, including examples of
    // customizing the pairing process, see the DeviceEnumerationAndPairing sample.
    public sealed partial class Scenario1_DiscoverServer
    {
        private BluetoothLEAdvertisementWatcher bluetoothLEAdvertisementWatcher;

        public Scenario1_DiscoverServer()
        {
            bluetoothLEAdvertisementWatcher = new BluetoothLEAdvertisementWatcher()
            {
                ScanningMode = BluetoothLEScanningMode.Passive
            };
            bluetoothLEAdvertisementWatcher.Received += BluetoothLEAdvertisementWatcher_Received;
            bluetoothLEAdvertisementWatcher.Start();
        }

        private void BluetoothLEAdvertisementWatcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            Debug.WriteLine(args.Advertisement);
            var EDDYSTONE_SERVICE_GUID = new Guid("0000feaa-0000-1000-8000-00805f9b34fb");
            var APPLE_COMPANY_ID = 0x004C;
            var APPLE_IBEACON_SELECTOR = new byte[] { 0x02, 0x15 };

            var filterAcceptDevice = false;

            if (args.Advertisement.ServiceUuids.Count > 0 &&
                args.Advertisement.ServiceUuids.Contains(EDDYSTONE_SERVICE_GUID))
            {
                // device is eddystone device
                filterAcceptDevice = true;
            }
            else if (args.Advertisement.ManufacturerData.Count > 0)
            {
                // Select Apple branded manufacturer specific data
                // that has a beacon identifying sequence in it
                var matchingManufacturerData = args.Advertisement.ManufacturerData.Where((n) =>
                {
                    // we have apple data -- let's make sure it is a beacon type
                    if (n.CompanyId == APPLE_COMPANY_ID)
                    {
                        // data length has to include some magic bytes to indicate that it is a beacon
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
                    filterAcceptDevice = true;
                }
            }

            if (filterAcceptDevice)
            {
                // accepted device
                Debug.WriteLine($">> Accepted device {args.BluetoothAddress}, {args.Timestamp}, {args.Advertisement.LocalName} <<");
            }
            else
            {
                // rejected device
                Debug.WriteLine($"Rejected device {args.BluetoothAddress}, {args.Timestamp}, {args.Advertisement.LocalName}");
            }
        }
    }
}