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
    public sealed partial class Scenario1_DiscoverServer : Page
    {
        private MainPage rootPage = MainPage.Current;

        private ObservableCollection<BluetoothLEDeviceDisplay> ResultCollection = new ObservableCollection<BluetoothLEDeviceDisplay>();

        private DeviceWatcher deviceWatcher;

        private BluetoothLEAdvertisementWatcher bluetoothLEAdvertisementWatcher = new BluetoothLEAdvertisementWatcher();

        public Scenario1_DiscoverServer()
        {
            InitializeComponent();
            bluetoothLEAdvertisementWatcher.Received += BluetoothLEAdvertisementWatcher_Received;
            bluetoothLEAdvertisementWatcher.Start();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
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

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            StopBleDeviceWatcher();
            bluetoothLEAdvertisementWatcher.Stop();

            // Save the selected device's ID for use in other scenarios.
            var bleDeviceDisplay = ResultsListView.SelectedItem as BluetoothLEDeviceDisplay;
            if (bleDeviceDisplay != null)
            {
                rootPage.SelectedBleDeviceId = bleDeviceDisplay.Id;
                rootPage.SelectedBleDeviceName = bleDeviceDisplay.Name;
            }
        }

        #region Device discovery
        private void EnumerateButton_Click()
        {
            if (deviceWatcher == null)
            {
                StartBleDeviceWatcher();

                rootPage.NotifyUser($"Device watcher started.", NotifyType.StatusMessage);
            }
            else
            {
                StopBleDeviceWatcher();

                rootPage.NotifyUser($"Device watcher stopped.", NotifyType.StatusMessage);
            }
        }

        private void StopBleDeviceWatcher()
        {
            if (deviceWatcher != null)
            {
                // Unregister the event handlers.
                deviceWatcher.Added -= DeviceWatcher_Added;
                deviceWatcher.Updated -= DeviceWatcher_Updated;
                deviceWatcher.Removed -= DeviceWatcher_Removed;
                deviceWatcher.EnumerationCompleted -= DeviceWatcher_EnumerationCompleted;
                deviceWatcher.Stopped -= DeviceWatcher_Stopped;

                // Stop the watcher.
                deviceWatcher.Stop();
                deviceWatcher = null;
            }

            EnumerateButton.Content = "Start enumerating";
        }

        /// <summary>
        ///     Starts a device watcher that looks for all nearby BT devices (paired or unpaired). Attaches event handlers and
        ///     populates the collection of devices.
        /// </summary>
        private void StartBleDeviceWatcher()
        {
            EnumerateButton.Content = "Stop enumerating";

            // Additional properties we would like about the device.
            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected" };

            // BT_Code: Currently Bluetooth APIs don't provide a selector to get ALL devices that are both paired and non-paired.
            deviceWatcher =
                    DeviceInformation.CreateWatcher(
                        "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")",
                        requestedProperties,
                        DeviceInformationKind.AssociationEndpoint);

            // Register event handlers before starting the watcher.
            deviceWatcher.Added += DeviceWatcher_Added;
            deviceWatcher.Updated += DeviceWatcher_Updated;
            deviceWatcher.Removed += DeviceWatcher_Removed;
            deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
            deviceWatcher.Stopped += DeviceWatcher_Stopped;

            // Start over with an empty collection.
            ResultCollection.Clear();

            // Start the watcher.
            deviceWatcher.Start();
        }

        private BluetoothLEDeviceDisplay FindBluetoothLEDeviceDisplay(string id)
        {
            foreach (BluetoothLEDeviceDisplay bleDeviceDisplay in ResultCollection)
            {
                if (bleDeviceDisplay.Id == id)
                {
                    return bleDeviceDisplay;
                }
            }
            return null;
        }

        private async void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation deviceInfo)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                if (sender == deviceWatcher)
                {
                    // Make sure device name isn't blank or already present in the list.
                    if (deviceInfo.Name != string.Empty && FindBluetoothLEDeviceDisplay(deviceInfo.Id) == null)
                    {
                        ResultCollection.Add(new BluetoothLEDeviceDisplay(deviceInfo));
                    }
                }
            });
        }

        private async void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                if (sender == deviceWatcher)
                {
                    BluetoothLEDeviceDisplay bleDeviceDisplay = FindBluetoothLEDeviceDisplay(deviceInfoUpdate.Id);
                    if (bleDeviceDisplay != null)
                    {
                        bleDeviceDisplay.Update(deviceInfoUpdate);
                    }
                }
            });
        }

        private async void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                if (sender == deviceWatcher)
                {
                    // Find the corresponding DeviceInformation in the collection and remove it.
                    BluetoothLEDeviceDisplay bleDeviceDisplay = FindBluetoothLEDeviceDisplay(deviceInfoUpdate.Id);
                    if (bleDeviceDisplay != null)
                    {
                        ResultCollection.Remove(bleDeviceDisplay);
                    }
                }
            });
        }

        private async void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object e)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                if (sender == deviceWatcher)
                {
                    rootPage.NotifyUser($"{ResultCollection.Count} devices found. Enumeration completed.",
                        NotifyType.StatusMessage);
                }
            });
        }

        private async void DeviceWatcher_Stopped(DeviceWatcher sender, object e)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                if (sender == deviceWatcher)
                {
                    rootPage.NotifyUser($"No longer watching for devices.",
                            sender.Status == DeviceWatcherStatus.Aborted ? NotifyType.ErrorMessage : NotifyType.StatusMessage);
                }
            });
        }
        #endregion

        #region Pairing

        private bool isBusy = false;

        private async void PairButton_Click()
        {
            // Do not allow a new Pair operation to start if an existing one is in progress.
            if (isBusy)
            {
                return;
            }

            isBusy = true;

            rootPage.NotifyUser("Pairing started. Please wait...", NotifyType.StatusMessage);

            // For more information about device pairing, including examples of
            // customizing the pairing process, see the DeviceEnumerationAndPairing sample.

            // Capture the current selected item in case the user changes it while we are pairing.
            var bleDeviceDisplay = ResultsListView.SelectedItem as BluetoothLEDeviceDisplay;

            // BT_Code: Pair the currently selected device.
            DevicePairingResult result = await bleDeviceDisplay.DeviceInformation.Pairing.PairAsync();
            rootPage.NotifyUser($"Pairing result = {result.Status}",
                result.Status == DevicePairingResultStatus.Paired || result.Status == DevicePairingResultStatus.AlreadyPaired
                    ? NotifyType.StatusMessage
                    : NotifyType.ErrorMessage);

            isBusy = false;
        }

        #endregion
    }
}