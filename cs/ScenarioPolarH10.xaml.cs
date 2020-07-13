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

namespace SDKTemplate
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
                            AppendConsoleText($"Battery Level Characteristic UUID: {characteristic.Uuid.ToString()}");
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
                    BLE_PolarH10.PmdControlPointResponse response = new BLE_PolarH10.PmdControlPointResponse(configPmdCP);

                    AppendConsoleText($"ECG supported = {response.streamSettings.EcgSupported}");
                    AppendConsoleText($"PPG supported = {response.streamSettings.PpgSupported}");
                    AppendConsoleText($"ACC supported = {response.streamSettings.AccSupported}");
                    AppendConsoleText($"PPI supported = {response.streamSettings.PpiSupported}");
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
                    IBuffer bufferRequest = BLE_PolarH10.CreateStreamingRequest(BLE_PolarH10.PmdControlPointCommand.REQUEST_MEASUREMENT_START, BLE_PolarH10.MeasurementSensor.ECG);
                    if (await CharacteristicWriteIBuffer(PmdControlPointCharacteristic, bufferRequest))
                    {
                        AppendConsoleText("ECG Stream has started");
                    }
                    else
                    {
                        Debug.WriteLine("Error on ECG Request");
                    }
                }
                else
                {
                    // STOP ECG STREAMING
                    IBuffer bufferRequest = BLE_PolarH10.CreateStreamingRequest(BLE_PolarH10.PmdControlPointCommand.STOP_MEASUREMENT, BLE_PolarH10.MeasurementSensor.ECG);
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
                    IBuffer bufferRequest = BLE_PolarH10.CreateStreamingRequest(BLE_PolarH10.PmdControlPointCommand.REQUEST_MEASUREMENT_START, BLE_PolarH10.MeasurementSensor.ACC);
                    if (await CharacteristicWriteIBuffer(PmdControlPointCharacteristic, bufferRequest))
                    {
                        AppendConsoleText("ACC Stream has started");
                    }
                    else
                    {
                        Debug.WriteLine("Error on ACC Request");
                    }
                }
                else
                {
                    // STOP ACC STREAMING
                    IBuffer bufferRequest = BLE_PolarH10.CreateStreamingRequest(BLE_PolarH10.PmdControlPointCommand.STOP_MEASUREMENT, BLE_PolarH10.MeasurementSensor.ACC);
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

            System.Diagnostics.Debug.WriteLine("Received Notification:" + newValue + " from " + sender.ToString());

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

            if (format != null)
            {
                if (format.FormatType == GattPresentationFormatTypes.UInt32 && data.Length >= 4)
                {
                    return BitConverter.ToInt32(data, 0).ToString();
                }
                else if (format.FormatType == GattPresentationFormatTypes.Utf8)
                {
                    try
                    {
                        return Encoding.UTF8.GetString(data);
                    }
                    catch (ArgumentException)
                    {
                        return "(error: Invalid UTF-8 string)";
                    }
                }
                else
                {
                    // Add support for other format types as needed.
                    return "Unsupported format: " + CryptographicBuffer.EncodeToHexString(buffer);
                }
            }
            return "Unknown format";
        }

        /// <summary>
        /// Process the raw data received from the device into application usable data,
        /// according the the Bluetooth Heart Rate Profile.
        /// https://www.bluetooth.com/specifications/gatt/viewer?attributeXmlFile=org.bluetooth.characteristic.heart_rate_measurement.xml&u=org.bluetooth.characteristic.heart_rate_measurement.xml
        /// This function throws an exception if the data cannot be parsed.
        /// </summary>
        /// <param name="data">Raw data received from the heart rate monitor.</param>
        /// <returns>The heart rate measurement value.</returns>
        private static ushort ParseHeartRateValue(byte[] data)
        {

            /*To get the rr-interval you have to read the flags from the first byte you receive. You read the flags as binary from right to left.
                bit 0 = 0: Heart Rate Value Format is set to UINT8.Units:BPM(1 byte).
                bit 0 = 1: Heart Rate Value Format is set to UINT16.Units:BPM(2 bytes).

                bit 1 and 2: Sensor Contact Status bits. These are not relevant for this.

                bit 3 = 0: Energy Expended field is not present.
                bit 3 = 1: Energy Expended field is present.Format = uint16.Units: kilo Joules.

                bit 4 = 0: RR - Interval values are not present.
                bit 4 = 1: One or more RR - Interval values are present.Format = uint16.unit 1 / 1024 sec.

                bit 5, 6 and 7: reserved for future use.

            If your first byte for example = 16 = 0x10 = 0b00010000 then byte 2 = is heart rate.
                Byte 3 and 4 are the rr - interval.
                Byte 5 and 6(if present) rr - interval.
            */

            System.Diagnostics.Debug.WriteLine("Parsing HR Value: " + BitConverter.ToString(data));

            // Heart Rate profile defined flag values
            const byte heartRateValueFormat = 0x01;

            byte flags = data[0];
            bool isHeartRateValueSizeLong = ((flags & heartRateValueFormat) != 0);

            if (isHeartRateValueSizeLong)
            {
                return BitConverter.ToUInt16(data, 1);
            }
            else
            {
                return data[1];
            }
        }

        private void AppendConsoleText(string text)
        {
            consolePanel.Text += text + "\n";
        }
    }
}
