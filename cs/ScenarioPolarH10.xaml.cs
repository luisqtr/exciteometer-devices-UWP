//*********************************************************
//
// Author: Luis Quintero
// Date: 10/07/2020
// Project: Excite-O-Meter / XR4ALL
//
//*********************************************************

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

using ExciteOMeter.Devices;

namespace ExciteOMeter
{
    // This scenario connects to the device selected in the "Discover
    // GATT Servers" scenario and communicates with it.
    // Note that this scenario is rather artificial because it communicates
    // with an unknown service with unknown characteristics.
    // In practice, your app will be interested in a specific service with
    // a specific characteristic.
    public sealed partial class ScenarioPolarH10 : Page
    {
        private MainPage rootPage = MainPage.Current;

        private BluetoothLEDevice bluetoothLeDevice = null;

        private BLE_PolarH10 devicePolarH10 = new BLE_PolarH10();

        private GattDeviceService BatteryService;
        private GattDeviceService PmdService;
        private GattDeviceService HrmService;

        // Stores characteristics while searching
        IReadOnlyList<GattCharacteristic> temp_characteristics = null;

        private GattCharacteristic BatteryLevelCharacteristic;
        private GattCharacteristic HeartRateMeasurementCharacteristic;
        private GattCharacteristic PmdControlPointCharacteristic;
        private GattCharacteristic PmdDataCharacteristic;

        // Flags for indication and notification
        private bool subscribedForNotification_Hrm = false;
        private bool subscribedForIndications_PmdCP = false;
        private bool subscribedForNotifications_PmdData = false;

        private GattPresentationFormat presentationFormat;

        // Console variables
        private bool is_console_enabled = true;
        private readonly int MAX_BUFFER_CONSOLE = 100;
        private int bufferCounter = 0;

        #region Error Codes
        readonly int E_BLUETOOTH_ATT_WRITE_NOT_PERMITTED = unchecked((int)0x80650003);
        readonly int E_BLUETOOTH_ATT_INVALID_PDU = unchecked((int)0x80650004);
        readonly int E_ACCESSDENIED = unchecked((int)0x80070005);
        readonly int E_DEVICE_NOT_AVAILABLE = unchecked((int)0x800710df); // HRESULT_FROM_WIN32(ERROR_DEVICE_NOT_AVAILABLE)
        #endregion


        #region UI Code
        public ScenarioPolarH10()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            SelectedDeviceRun.Text = rootPage.SelectedBleDeviceName;
            if (string.IsNullOrEmpty(rootPage.SelectedBleDeviceId))
            {
                ConnectButton.IsEnabled = false;
                rootPage.NotifyUser("Error: Please select a device in the first page", NotifyType.ErrorMessage);
            }
            else
            {
                SetupConnectionPolarH10();
            }
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            var success = await ClearBluetoothLEDeviceAsync();
            if (!success)
            {
                rootPage.NotifyUser("Error: Unable to reset app state", NotifyType.ErrorMessage);
            }
        }
        #endregion

        private async Task<bool> ClearBluetoothLEDeviceAsync()
        {
            if (subscribedForNotification_Hrm)
            {
                // Need to clear the CCCD from the remote device so we stop receiving notifications
                var result = await HeartRateMeasurementCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None);
                if (result != GattCommunicationStatus.Success)
                {
                    return false;
                }
                else
                {
                    HeartRateMeasurementCharacteristic.ValueChanged -= Characteristic_ValueChanged;
                    subscribedForNotification_Hrm = false;
                }
            }

            if (subscribedForIndications_PmdCP)
            {
                // Need to clear the CCCD from the remote device so we stop receiving notifications
                var result = await PmdControlPointCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None);
                if (result != GattCommunicationStatus.Success)
                {
                    return false;
                }
                else
                {
                    PmdControlPointCharacteristic.ValueChanged -= Characteristic_ValueChanged;
                    subscribedForIndications_PmdCP = false;
                }
            }

            if (subscribedForNotifications_PmdData)
            {
                // Need to clear the CCCD from the remote device so we stop receiving notifications
                var result = await PmdDataCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None);
                if (result != GattCommunicationStatus.Success)
                {
                    return false;
                }
                else
                {
                    PmdDataCharacteristic.ValueChanged -= Characteristic_ValueChanged;
                    subscribedForNotifications_PmdData = false;
                }
            }

            bluetoothLeDevice?.Dispose();
            bluetoothLeDevice = null;
            return true;
        }


        private async void SetupConnectionPolarH10()
        {
            // Request services
            if (await ConnectAndFetchServices())
            {
                AppendConsoleText($"Requesting Services from Polar H10... OK");
            }
            else
            {
                rootPage.NotifyUser("Error accessing services from Polar H10.", NotifyType.ErrorMessage);
                return;
            }

            // Characteristics from Battery Service
            if (BatteryService != null)
            {
                if (await EnumerateCharacteristics(BatteryService))
                {
                    foreach (GattCharacteristic characteristic in temp_characteristics)
                    {
                        // Store the characteristics of interest
                        if (characteristic.Uuid.CompareTo(BLE_PolarH10.BATTERY_LEVEL_CHARACTERISTIC) == 0)
                        {
                            BatteryLevelCharacteristic = characteristic;
                            Debug.WriteLine("Found Battery Level Characteristic: " + characteristic.Uuid.ToString());
                            AppendConsoleText($"Battery Level Characteristic UUID: {characteristic.Uuid}");

                            // Ask for battery level
                            ReadBattery_Click();
                        }
                    }
                }
            }
            else
            {
                Debug.WriteLine("Battery Service could not be found");
            }

            // Characteristics from Heart Rate Monitor Service
            if (HrmService != null)
            {
                if (await EnumerateCharacteristics(HrmService))
                {
                    foreach (GattCharacteristic characteristic in temp_characteristics)
                    {
                        // Store the characteristics of interest
                        if (characteristic.Uuid.CompareTo(BLE_PolarH10.HR_MEASUREMENT) == 0)
                        {
                            HeartRateMeasurementCharacteristic = characteristic;
                            Debug.WriteLine("Found HR Measurement Characteristic: " + characteristic.Uuid.ToString());
                            AppendConsoleText($"Heart Rate Measurement Characteristic UUID: {characteristic.Uuid}");
                        }
                    }
                }
            }
            else
            {
                Debug.WriteLine("Heart Rate Monitor Service could not be found");
            }

            // Characteristics from Streaming Measurement Service
            if (PmdService != null)
            {
                if (await EnumerateCharacteristics(PmdService))
                {
                    foreach (GattCharacteristic characteristic in temp_characteristics)
                    {
                        // Store the characteristics of interest
                        if (characteristic.Uuid.CompareTo(BLE_PolarH10.PMD_CP) == 0)
                        {
                            PmdControlPointCharacteristic = characteristic;
                            Debug.WriteLine("Found PMD_CP Characteristic: " + characteristic.Uuid.ToString());
                            AppendConsoleText($"Streaming PMD Control Point Characteristic UUID: {characteristic.Uuid.ToString()}");
                        }
                        else if (characteristic.Uuid.CompareTo(BLE_PolarH10.PMD_DATA) == 0)
                        {
                            PmdDataCharacteristic = characteristic;
                            Debug.WriteLine("Streaming PMD Data Characteristic: " + characteristic.Uuid.ToString());
                        }
                    }
                }
            }
            else
            {
                Debug.WriteLine("Streaming of ECG and ACC could not be found");
            }

            ///// SETUP STREAMING CHARACTERISTICS

            if (await ConfigurePmDStreaming())
            {
                Debug.WriteLine("Setup Streaming was OK");
                AppendConsoleText($"Configuring Streaming... OK");

                byte[] configPmdCP = await CharacteristicRead(PmdControlPointCharacteristic);

                if (configPmdCP != null)
                {
                    // Data from the PMD Control Point has been received
                    BLE_PolarH10.pmdCpResponse.UpdateData(configPmdCP);
                    AppendConsoleText($"{BLE_PolarH10.pmdCpResponse}");
                }
            }
            
            // TODO: Show buttons
            PanelCharacteristics.Visibility = Visibility.Visible;
        }

        private async Task<bool> ConnectAndFetchServices()
        {
            ConnectButton.IsEnabled = false;
            ConnectButton.Content = "Connecting...";

            if (!await ClearBluetoothLEDeviceAsync())
            {
                rootPage.NotifyUser("Error: Unable to reset state, try again.", NotifyType.ErrorMessage);
                ConnectButton.IsEnabled = true;
                return false;
            }

            try
            {
                // BT_Code: BluetoothLEDevice.FromIdAsync must be called from a UI thread because it may prompt for consent.
                bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(rootPage.SelectedBleDeviceId);

                if (bluetoothLeDevice == null)
                {
                    rootPage.NotifyUser("Failed to connect to device.", NotifyType.ErrorMessage);
                }
            }
            catch (Exception ex) when (ex.HResult == E_DEVICE_NOT_AVAILABLE)
            {
                rootPage.NotifyUser("Bluetooth radio is not on.", NotifyType.ErrorMessage);
            }

            if (bluetoothLeDevice != null)
            {
                // Note: BluetoothLEDevice.GattServices property will return an empty list for unpaired devices. For all uses we recommend using the GetGattServicesAsync method.
                // BT_Code: GetGattServicesAsync returns a list of all the supported services of the device (even if it's not paired to the system).
                // If the services supported by the device are expected to change during BT usage, subscribe to the GattServicesChanged event.

                //gattSession = await GattSession.FromDeviceIdAsync(bluetoothLeDevice.BluetoothDeviceId);
                //System.Diagnostics.Debug.WriteLine("GattSession MTU (MaxPduSize): " + gattSession.MaxPduSize);

                GattDeviceServicesResult result = await bluetoothLeDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached);

                if (result.Status == GattCommunicationStatus.Success)
                {
                    var services = result.Services;
                    rootPage.NotifyUser(String.Format("Found {0} services", services.Count), NotifyType.StatusMessage);
                    foreach (var service in services)
                    {
                        //ServiceList.Items.Add(new ComboBoxItem { Content = DisplayHelpers.GetServiceName(service), Tag = service });

                        // Store the services of interest
                        if (service.Uuid.CompareTo(BLE_PolarH10.PMD_SERVICE) == 0)
                        {
                            PmdService = service;
                            Debug.WriteLine("Found PMD Service" + service.Uuid.ToString());

                        }
                        else if (service.Uuid.CompareTo(BLE_PolarH10.HR_SERVICE) == 0)
                        {
                            HrmService = service;
                            Debug.WriteLine("Found HRM Service" + service.Uuid.ToString());
                        }
                        else if (service.Uuid.CompareTo(BLE_PolarH10.BATTERY_SERVICE) == 0)
                        {
                            BatteryService = service;
                            Debug.WriteLine("Found Battery Service" + service.Uuid.ToString());
                        }

                    }
                    ConnectButton.Visibility = Visibility.Collapsed;
                    //ServiceList.Visibility = Visibility.Visible;

                    ConnectButton.Content = "Connect";
                    ConnectButton.IsEnabled = true;

                    return true;
                }
                else
                {
                    rootPage.NotifyUser("Device unreachable", NotifyType.ErrorMessage);
                }
            }

            ConnectButton.Content = "Connect";
            ConnectButton.IsEnabled = true;

            return false;
        }

        private async Task<bool> EnumerateCharacteristics(GattDeviceService service)
        {
            temp_characteristics = null;
            try
            {
                // Ensure we have access to the device.
                var accessStatus = await service.RequestAccessAsync();
                if (accessStatus == DeviceAccessStatus.Allowed)
                {
                    // BT_Code: Get all the child characteristics of a service. Use the cache mode to specify uncached characterstics only 
                    // and the new Async functions to get the characteristics of unpaired devices as well. 
                    var result = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                    if (result.Status == GattCommunicationStatus.Success)
                    {
                        temp_characteristics = result.Characteristics;
                        return true;
                    }
                    else
                    {
                        rootPage.NotifyUser("Error accessing service.", NotifyType.ErrorMessage);

                        // On error, act as if there are no characteristics.
                        temp_characteristics = new List<GattCharacteristic>();
                        return false;
                    }
                }
                else
                {
                    // Not granted access
                    rootPage.NotifyUser("Error accessing service.", NotifyType.ErrorMessage);

                    // On error, act as if there are no characteristics.
                    temp_characteristics = new List<GattCharacteristic>();
                    return false;
                }
            }
            catch (Exception ex)
            {
                rootPage.NotifyUser("Restricted service. Can't read characteristics: " + ex.Message,
                    NotifyType.ErrorMessage);
                // On error, act as if there are no characteristics.
                temp_characteristics = new List<GattCharacteristic>();

                return false;
            }
        }

        /// <summary>
        /// Perform the prerequisites to connect to the Polar H10 for data streaming
        /// according to the documentation on
        /// https://github.com/polarofficial/polar-ble-sdk/blob/master/technical_documentation/Polar_Measurement_Data_Specification.pdf
        /// </summary>
        /// <returns>bool with result of processes</returns>
        private async Task<bool> ConfigurePmDStreaming()
        {
            // Set the Indication flag for the PMD Control Point
            bool cp_ok = false;
            // Set the notifications flag for the PMD Data
            bool data_ok = false;

            if (!subscribedForIndications_PmdCP)
            {
                cp_ok = await ValueChanged_SetNotifications(PmdControlPointCharacteristic, GattClientCharacteristicConfigurationDescriptorValue.Indicate);
                if (cp_ok)
                {
                    // Set callback
                    PmdControlPointCharacteristic.ValueChanged += Characteristic_ValueChanged;
                    subscribedForIndications_PmdCP = true;
                }
            }

            if (!subscribedForNotifications_PmdData)
            {
                data_ok = await ValueChanged_SetNotifications(PmdDataCharacteristic, GattClientCharacteristicConfigurationDescriptorValue.Notify);
                if (data_ok)
                {
                    // Set callback
                    PmdDataCharacteristic.ValueChanged += Characteristic_ValueChanged;
                    subscribedForNotifications_PmdData = true;
                }
            }
            return cp_ok & data_ok;
        }

        private async void ReadBattery_Click()
        {
            byte[] response = await CharacteristicRead(BatteryLevelCharacteristic);
            if (response != null)
            {
                Debug.WriteLine("Battery value was read: " + BitConverter.ToString(response));

                BLE_PolarH10.batteryData.UpdateData(response);

                AppendConsoleText(BLE_PolarH10.batteryData.ToString());

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () => textBatteryLevel.Text = BLE_PolarH10.batteryData.ToStringValue());
            }
        }

        private async void SettingsECG_Click()
        {
            IBuffer bufferRequest = BLE_PolarH10.CreateStreamingRequest(BLE_PolarH10.PmdControlPointCommand.GET_MEASUREMENT_SETTINGS, BLE_PolarH10.MeasurementType.ECG);
            if (await CharacteristicWriteIBuffer(PmdControlPointCharacteristic, bufferRequest) == false)
            {
                AppendConsoleText("Error requesting ECG settings...");
            }
        }

        private async void SettingsACC_Click()
        {
            IBuffer bufferRequest = BLE_PolarH10.CreateStreamingRequest(BLE_PolarH10.PmdControlPointCommand.GET_MEASUREMENT_SETTINGS, BLE_PolarH10.MeasurementType.ACC);
            if (await CharacteristicWriteIBuffer(PmdControlPointCharacteristic, bufferRequest) == false)
            {
                AppendConsoleText("Error requesting ACC settings...");
            }
        }

        private async void ToggleHeartRate_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleSwitch toggleSwitch = sender as ToggleSwitch;
            if (toggleSwitch != null)
            {
                if (toggleSwitch.IsOn == true)
                {
                    if (!subscribedForNotification_Hrm)
                    {
                        if (await ValueChanged_SetNotifications(HeartRateMeasurementCharacteristic) == true)
                        {
                            // Set callback
                            HeartRateMeasurementCharacteristic.ValueChanged += Characteristic_ValueChanged;
                            subscribedForNotification_Hrm = true;
                        }
                        else { toggleSwitch.IsOn = false; }
                    }
                }
                else
                {
                    if (subscribedForNotification_Hrm)
                    {
                        if (await ValueChanged_UnsetNotifications(HeartRateMeasurementCharacteristic) == true)
                        {
                            // Unset Callback
                            subscribedForNotification_Hrm = false;
                            HeartRateMeasurementCharacteristic.ValueChanged -= Characteristic_ValueChanged;
                        }
                        else { toggleSwitch.IsOn = true; }
                    }
                }
            }
        }

        private async void ToggleECG_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleSwitch toggleSwitch = sender as ToggleSwitch;
            if (toggleSwitch != null)
            {
                if (toggleSwitch.IsOn == true)
                {
                    // ACTIVATE ECG STREAMING
                    if (!subscribedForNotifications_PmdData)
                    {
                        if (await ValueChanged_SetNotifications(PmdDataCharacteristic) == true)
                        {
                            // Set callback
                            PmdDataCharacteristic.ValueChanged += Characteristic_ValueChanged;
                            subscribedForNotifications_PmdData = true;
                        }
                        else 
                        { 
                            toggleSwitch.IsOn = false;
                            rootPage.NotifyUser("Error Setting Notification for PMD Data", NotifyType.ErrorMessage);
                            return;
                        }
                    }
                    
                    // Streaming Request
                    IBuffer bufferRequest = BLE_PolarH10.CreateStreamingRequest(BLE_PolarH10.PmdControlPointCommand.REQUEST_MEASUREMENT_START, BLE_PolarH10.MeasurementType.ECG);
                    if (await CharacteristicWriteIBuffer(PmdControlPointCharacteristic, bufferRequest))
                    {
                        AppendConsoleText("ECG Request has been successful. \n\tInitializing... (this might take up to 20 seconds)");
                    }
                    else
                    {
                        Debug.WriteLine("Error on ECG Request");
                    }
                }
                else
                {
                    // STOP ECG STREAMING
                    IBuffer bufferRequest = BLE_PolarH10.CreateStreamingRequest(BLE_PolarH10.PmdControlPointCommand.STOP_MEASUREMENT, BLE_PolarH10.MeasurementType.ECG);
                    if (await CharacteristicWriteIBuffer(PmdControlPointCharacteristic, bufferRequest))
                    {
                        AppendConsoleText("ECG Stream has been stopped");
                    }
                    else
                    {
                        Debug.WriteLine("Error on ECG Stop Request");
                    }
                }
            }
        }

        private async void ToggleACC_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleSwitch toggleSwitch = sender as ToggleSwitch;
            if (toggleSwitch != null)
            {
                if (toggleSwitch.IsOn == true)
                {
                    // ACTIVATE AAC STREAMING
                    if (!subscribedForNotifications_PmdData)
                    {
                        if (await ValueChanged_SetNotifications(PmdDataCharacteristic) == true)
                        {
                            // Set callback
                            PmdDataCharacteristic.ValueChanged += Characteristic_ValueChanged;
                            subscribedForNotifications_PmdData = true;
                        }
                        else
                        {
                            toggleSwitch.IsOn = false;
                            rootPage.NotifyUser("Error Setting Notification for PMD Data", NotifyType.ErrorMessage);
                            return;
                        }
                    }

                    // Streaming Request
                    IBuffer bufferRequest = BLE_PolarH10.CreateStreamingRequest(BLE_PolarH10.PmdControlPointCommand.REQUEST_MEASUREMENT_START, BLE_PolarH10.MeasurementType.ACC);
                    if (await CharacteristicWriteIBuffer(PmdControlPointCharacteristic, bufferRequest))
                    {
                        AppendConsoleText("ACC Request has been successful. \n\tInitializing... (this might take up to 20 seconds)");
                    }
                    else
                    {
                        Debug.WriteLine("Error on ACC Request");
                    }
                }
                else
                {
                    // STOP ACC STREAMING
                    IBuffer bufferRequest = BLE_PolarH10.CreateStreamingRequest(BLE_PolarH10.PmdControlPointCommand.STOP_MEASUREMENT, BLE_PolarH10.MeasurementType.ACC);
                    if (await CharacteristicWriteIBuffer(PmdControlPointCharacteristic, bufferRequest))
                    {
                        AppendConsoleText("ACC Stream has been stopped");
                    }
                    else
                    {
                        Debug.WriteLine("Error on ACC Stop Request");
                    }
                }
            }
        }

        private void StopMessagesConsole_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox= sender as CheckBox;
            if (checkBox != null)
            {
                if (checkBox.IsChecked == true)
                    // Stop showing messages
                    is_console_enabled = false;
                else
                    is_console_enabled = true;
            }
        }

        private void ClearConsole_Click(object sender, RoutedEventArgs e)
        {
            AppendConsoleText("", true); // Clear console
        }

        private async Task<bool> ValueChanged_SetNotifications(GattCharacteristic selectedCharacteristic,
                                                          GattClientCharacteristicConfigurationDescriptorValue subscriptionType = GattClientCharacteristicConfigurationDescriptorValue.Notify)
        {
            // initialize status
            GattCommunicationStatus status = GattCommunicationStatus.Unreachable;
            var cccdValue = GattClientCharacteristicConfigurationDescriptorValue.None;

            if (subscriptionType == GattClientCharacteristicConfigurationDescriptorValue.Notify &&
                selectedCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify))
            {
                cccdValue = GattClientCharacteristicConfigurationDescriptorValue.Notify;
            }
            else if (subscriptionType == GattClientCharacteristicConfigurationDescriptorValue.Indicate &&
                selectedCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate))
            {
                cccdValue = GattClientCharacteristicConfigurationDescriptorValue.Indicate;
            }

            try
            {
                // BT_Code: Must write the CCCD in order for server to send indications.
                // We receive them in the ValueChanged event handler.
                status = await selectedCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(cccdValue);

                if (status == GattCommunicationStatus.Success)
                {
                    rootPage.NotifyUser("Successfully subscribed for value changes", NotifyType.StatusMessage);
                    // CALL THE CALLBACK FUNCTION
                    return true;
                }
                else
                {
                    rootPage.NotifyUser($"Error registering for value changes: {status}", NotifyType.ErrorMessage);
                    return false;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                // This usually happens when a device reports that it support indicate, but it actually doesn't.
                rootPage.NotifyUser(ex.Message, NotifyType.ErrorMessage);
                return false;
            }
        }

        private async Task<bool> ValueChanged_UnsetNotifications(GattCharacteristic selectedCharacteristic)
        {
            try
            {
                // BT_Code: Must write the CCCD in order for server to send notifications.
                var result = await
                        selectedCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                            GattClientCharacteristicConfigurationDescriptorValue.None);
                if (result == GattCommunicationStatus.Success)
                {
                    // UNSET FLAG AND UNSET CALLBACK
                    rootPage.NotifyUser("Successfully un-registered for notifications", NotifyType.StatusMessage);
                    return true;
                }
                else
                {
                    rootPage.NotifyUser($"Error un-registering for notifications: {result}", NotifyType.ErrorMessage);
                    return false;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                // This usually happens when a device reports that it support notify, but it actually doesn't.
                rootPage.NotifyUser(ex.Message, NotifyType.ErrorMessage);
                return false;
            }
        }

        private async Task<byte[]> CharacteristicRead(GattCharacteristic selectedCharacteristic)
        {
            // BT_Code: Read the actual value from the device by using Uncached.
            GattReadResult result = await selectedCharacteristic.ReadValueAsync(BluetoothCacheMode.Uncached);

            if (result.Status == GattCommunicationStatus.Success)
            {
                byte[] data;
                CryptographicBuffer.CopyToByteArray(result.Value, out data);
                Debug.WriteLine("Read Result:" + BitConverter.ToString(data));

                string formattedResult = FormatValueByPresentation(result.Value, presentationFormat);
                rootPage.NotifyUser($"Read result: {formattedResult}", NotifyType.StatusMessage);
                return data;
            }
            else
            {
                rootPage.NotifyUser($"Read failed: {result.Status}", NotifyType.ErrorMessage);
                return null;
            }
        }

        private async Task<bool> CharacteristicWriteIBuffer(GattCharacteristic selectedCharacteristic, IBuffer buffer)
        {
            try
            {
                byte[] data;
                CryptographicBuffer.CopyToByteArray(buffer, out data);
                Debug.WriteLine("Write HEX:" + BitConverter.ToString(data));
                Debug.WriteLine("\t Buffer Length:" + buffer.Length.ToString());

                AppendConsoleText($"Writing to characteristic: {selectedCharacteristic.Uuid}. Value: {BitConverter.ToString(data)} | {buffer.Length} bytes");

                return await WriteBufferToSelectedCharacteristicAsync(selectedCharacteristic, buffer);
            }
            catch (FormatException exception)
            {
                Debug.WriteLine(exception.Message);

                // wrong format
                rootPage.NotifyUser("Data to write has to be an HEX string without Ox at the beginning", NotifyType.ErrorMessage);

                return false;
            }
        }

        private async Task<bool> WriteBufferToSelectedCharacteristicAsync(GattCharacteristic selectedCharacteristic, IBuffer buffer)
        {
            try
            {
                // BT_Code: Writes the value from the buffer to the characteristic.
                var result = await selectedCharacteristic.WriteValueWithResultAsync(buffer);

                if (result.Status == GattCommunicationStatus.Success)
                {
                    rootPage.NotifyUser("Successfully wrote value to device", NotifyType.StatusMessage);
                    return true;
                }
                else
                {
                    rootPage.NotifyUser($"Write failed: {result.Status}", NotifyType.ErrorMessage);
                    return false;
                }
            }
            catch (Exception ex) when (ex.HResult == E_BLUETOOTH_ATT_INVALID_PDU)
            {
                rootPage.NotifyUser(ex.Message, NotifyType.ErrorMessage);
                return false;
            }
            catch (Exception ex) when (ex.HResult == E_BLUETOOTH_ATT_WRITE_NOT_PERMITTED || ex.HResult == E_ACCESSDENIED)
            {
                // This usually happens when a device reports that it support writing, but it actually doesn't.
                rootPage.NotifyUser(ex.Message, NotifyType.ErrorMessage);
                return false;
            }
        }

        private async void Characteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            // BT_Code: An Indicate or Notify reported that the value has changed.
            // Display the new value with a timestamp.
            var newValue = FormatValueByPresentation(args.CharacteristicValue, presentationFormat);
            var message = $"Value at {DateTime.Now:hh:mm:ss.FFF}: {newValue}";

            // Convert from Buffer to Byte
            byte[] data;
            CryptographicBuffer.CopyToByteArray(args.CharacteristicValue, out data);

            // Battery Service
            if (sender.Uuid.Equals(BLE_PolarH10.BATTERY_LEVEL_CHARACTERISTIC))
            {
                BLE_PolarH10.batteryData.UpdateData(data);
                AppendConsoleText($"{BLE_PolarH10.batteryData}");
            }
            // Heart Rate Measurement Service (HR, EE, RRi)
            else if (sender.Uuid.Equals(BLE_PolarH10.HR_MEASUREMENT))
            {
                BLE_PolarH10.hrmData.UpdateData(data);
                AppendConsoleText($"{BLE_PolarH10.hrmData}");
            }
            // Response from Stream PMD Control Point to configure streaming
            else if (sender.Uuid.Equals(BLE_PolarH10.PMD_CP))
            {
                // Response is from Settings Request or about to start streaming
                BLE_PolarH10.pmdCpResponse.UpdateData(data);
                AppendConsoleText($"{BLE_PolarH10.pmdCpResponse}");
            }
            // Response from Stream PMD Data to get streaming bytes
            else if (sender.Uuid.Equals(BLE_PolarH10.PMD_DATA))
            {
                // Response is already a streaming value
                BLE_PolarH10.pmdDataResponse.UpdateData(data);
                AppendConsoleText($"{BLE_PolarH10.pmdDataResponse}");
            }
            else
            {
                AppendConsoleText($"Data from unknown characteristic uuid: {sender.Uuid} | Data: {BitConverter.ToString(data)}");
            }

            Debug.WriteLine("Received:" + newValue + " from " + sender.ToString());

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () => CharacteristicLatestValue.Text = message);
        }

        private string FormatValueByPresentation(IBuffer buffer, GattPresentationFormat format)
        {
            // BT_Code: For the purpose of this sample, this function converts only UInt32 and
            // UTF-8 buffers to readable text. It can be extended to support other formats if your app needs them.
            byte[] data;
            CryptographicBuffer.CopyToByteArray(buffer, out data);

            return BitConverter.ToString(data);

            //if (format != null)
            //{
            //    if (format.FormatType == GattPresentationFormatTypes.UInt32 && data.Length >= 4)
            //    {
            //        return BitConverter.ToInt32(data, 0).ToString();
            //    }
            //    else if (format.FormatType == GattPresentationFormatTypes.Utf8)
            //    {
            //        try
            //        {
            //            return Encoding.UTF8.GetString(data);
            //        }
            //        catch (ArgumentException)
            //        {
            //            return "(error: Invalid UTF-8 string)";
            //        }
            //    }
            //    else
            //    {
            //        // Add support for other format types as needed.
            //        return "Unsupported format: " + CryptographicBuffer.EncodeToHexString(buffer);
            //    }
            //}
            //return "Unknown format";
        }

        private async void AppendConsoleText(string text, bool clear_console=false)
        {
            if(clear_console)
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () => consolePanel.Text = "\n");
            if (is_console_enabled)
            {
                if (bufferCounter++ < MAX_BUFFER_CONSOLE)
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () => consolePanel.Text += text + "\n");
                }
                else
                {
                    bufferCounter = 0;
                    // Clean buffer
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () => consolePanel.Text = text + "\n");
                }
            }
        }
    }
}
