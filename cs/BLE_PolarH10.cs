﻿//*********************************************************
//
// Author: Luis Quintero
// Date: 10/07/2020
// Project: Excite-O-Meter / XR4ALL
//
//*********************************************************

using System;
using System.IO;
using System.Collections.Generic;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;

namespace ExciteOMeter
{
    /// <summary>
    /// Processes all the data exchanged from the Polar H10 through
    /// Bluetooth LE protocol. Mapping the hexadecimal values to
    /// readable strings.
    /// </summary>
    class BLE_PolarH10
    {
        // Battery
        public static readonly Guid BATTERY_SERVICE              = new Guid("0000180f-0000-1000-8000-00805f9b34fb");
        public static readonly Guid BATTERY_LEVEL_CHARACTERISTIC = new Guid("00002a19-0000-1000-8000-00805f9b34fb");

        // Heart Rate
        public static readonly Guid HR_SERVICE              = new Guid("0000180D-0000-1000-8000-00805f9b34fb");
        public static readonly Guid BODY_SENSOR_LOCATION    = new Guid("00002a38-0000-1000-8000-00805f9b34fb"); // NOT USED
        public static readonly Guid HR_MEASUREMENT          = new Guid("00002a37-0000-1000-8000-00805f9b34fb");

        // Polar Measurement Data (PMD)
        public static readonly Guid PMD_SERVICE     = new Guid("FB005C80-02E7-F387-1CAD-8ACD2D8DF0C8");
        public static readonly Guid PMD_DATA        = new Guid("FB005C82-02E7-F387-1CAD-8ACD2D8DF0C8");
        public static readonly Guid PMD_CP          = new Guid("FB005C81-02E7-F387-1CAD-8ACD2D8DF0C8");

        // Instances of objects to store data from 
        public static BatteryData batteryData = new BatteryData();
        public static HeartRateMeasurementData hrmData = new HeartRateMeasurementData();
        public static PmdControlPointResponse pmdCpResponse = new PmdControlPointResponse();
        public static PmdDataResponse pmdDataResponse = new PmdDataResponse();

        public static string docPath = Windows.Storage.ApplicationData.Current.LocalFolder.Path;

        /// <summary>
        /// Type of actions to execute in the PMD Control Point
        /// </summary>
        public enum PmdControlPointCommand
        {
            NULL = 0x00,
            GET_MEASUREMENT_SETTINGS = 0x01,
            REQUEST_MEASUREMENT_START = 0x02,
            STOP_MEASUREMENT = 0x03,
        }

        /// <summary>
        /// Readable response from the Control Point of the PMD service.
        /// </summary>
        public enum PmdControlPointResponseCode
        {
            SUCCESS = 0x00,
            ERROR_INVALID_OP_CODE = 0x01,

            ERROR_INVALID_MEASUREMENT_TYPE = 0x02,
            ERROR_NOT_SUPPORTED = 0x03,
            ERROR_INVALID_LENGTH = 0x04,
            ERROR_INVALID_PARAMETER = 0x05,
            ERROR_INVALID_STATE = 0x06,
            ERROR_INVALID_RESOLUTION = 0x07,
            ERROR_INVALID_SAMPLE_RATE = 0x08,
            ERROR_INVALID_RANGE = 0x09,
            ERROR_INVALID_MTU = 0x0A,
        }

        /// <summary>
        /// Available sensors in Polar devices
        /// </summary>
        public enum MeasurementType
        {
            ECG = 0x00,     // Electrocardiographic Signal  // Supported by Polar H10
            PPG = 0x01,
            ACC = 0x02,     // Accelerometer Signal         // Supported by Polar H10
            PPI = 0x03,
            BIOZ = 0x04,
            GYRO = 0x05,
            MAGNETOMETER = 0x06,
            BAROMETER = 0x07,
            AMBIENT = 0x08,
            UNKNOWN_TYPE = 0xFF,
        }


        /// <summary>
        /// Create ATT package based on API instructions
        /// </summary>
        /// <param name="command">Type of command to execute on the device. Use enum PmdControlPointCommand</param>
        /// <param name="sensor">Type of sensor to stream data from. Use enum MeasurementType</param>
        /// <param name="additionalData"></param>
        /// <returns></returns>
        public static IBuffer CreateStreamingRequest(PmdControlPointCommand command, MeasurementType sensor /*ADD PARAMETERS FOR SPECIFIC SETUP OF STREAMING*/)
        {
            int numBytes;
            byte[] parameters = null;

            if (command == PmdControlPointCommand.GET_MEASUREMENT_SETTINGS || command == PmdControlPointCommand.STOP_MEASUREMENT)
            {
                numBytes = 2;
                parameters = new byte[numBytes];
            }
            else if (command == PmdControlPointCommand.REQUEST_MEASUREMENT_START)
            {
                switch (sensor)
                {
                    case MeasurementType.ECG:
                        numBytes = 10;
                        parameters = new byte[numBytes];

                        // Specific setup for ECG
                        parameters[2] = 0x00;     // SAMPLE_RATE
                        parameters[3] = 0x01;     // array_count(1)
                        parameters[4] = 0x82;     // 130Hz - A
                        parameters[5] = 0x00;     // 130Hz - B
                        parameters[6] = 0x01;     // RESOLUTION
                        parameters[7] = 0x01;     // array_count(1)
                        parameters[8] = 0x0E;     // 14bit - A
                        parameters[9] = 0x00;     // 14bit - B

                        break;
                    case MeasurementType.ACC:
                        numBytes = 14;
                        parameters = new byte[numBytes];

                        // Specific Setup for ACC
                        parameters[2] = 0x00;     // SAMPLE_RATE
                        parameters[3] = 0x01;     // array_count(1)
                        parameters[4] = 0xC8;     // 200Hz - A
                        parameters[5] = 0x00;     // 200Hz - B
                        parameters[6] = 0x01;     // RESOLUTION
                        parameters[7] = 0x01;     // array_count(1)
                        parameters[8] = 0x10;     // 16bit - A
                        parameters[9] = 0x00;     // 16bit - B
                        parameters[10] = 0x02;     // RANGE
                        parameters[11] = 0x01;     // array_count(1)
                        parameters[12] = 0x08;     // 8G - A
                        parameters[13] = 0x00;     // 8G - B
                        break;
                }
            }
            else
            {
                return null;
            }

            // Always at the beginning goes the command and type of sensor
            parameters[0] = (byte)command;
            parameters[1] = (byte)sensor;

            DataWriter writer = new DataWriter();
            writer.WriteBytes(parameters);
            return writer.DetachBuffer();
        }


        /*
         * 
         * CLASSES DEFINITIONS
         * 
         * */
        /// <summary>
        /// Structure of data received from PMD Control Point.
        /// Indicates the settings of the devices and activation of
        /// data streaming.
        /// </summary>
        public class PmdControlPointResponse
        {
            // Types of response from PMD CP
            public enum PmdResponseCode
            {
                NULL = 0x00,
                FEATURES_READ_RESPONSE = 0x0F,
                CONTROL_POINT_RESPONSE = 0xF0,
            }
            public string stringHex;
            public PmdResponseCode responseCode;

            // When response from Read Supported Measurements
            private enum SupportedStreamsCode : byte
            {
                ECG_SUPPORTED = 0x01,
                PPG_SUPPORTED = 0x02,
                ACC_SUPPORTED = 0x04,
                PPI_SUPPORTED = 0x08,
            }
            public bool readAvailableMeasurements = false;
            public bool EcgSupported;
            public bool PpgSupported;
            public bool AccSupported;
            public bool PpiSupported;

            // For response from Polar Measurement Settings
            public enum MeasurementSettingType
            {
                SAMPLE_RATE = 0x00,
                RESOLUTION = 0x01,
                RANGE = 0x02,
            }

            public PmdControlPointCommand opCode = PmdControlPointCommand.NULL;
            public MeasurementType measurementType;
            public PmdControlPointResponseCode status;
            public IBuffer parameters;
            public bool more;
            public string streamSettingsString = "";

            public PmdControlPointResponse()
            {
                SetDefaultValues();
            }

            private void SetDefaultValues()
            {
                stringHex = "";
                responseCode = PmdResponseCode.NULL;

                readAvailableMeasurements = false;
                EcgSupported = false;
                PpgSupported = false;
                AccSupported = false;
                PpiSupported = false;

                opCode = PmdControlPointCommand.NULL;
                measurementType = MeasurementType.UNKNOWN_TYPE;
                status = PmdControlPointResponseCode.ERROR_NOT_SUPPORTED;
                parameters = null;
                more = false;
                streamSettingsString = "";
            }

            public void UpdateData(byte[] data)
            {
                // Restart values before setting new values
                SetDefaultValues();

                stringHex = BitConverter.ToString(data);
                responseCode = (PmdResponseCode) data[0];

                if (responseCode.Equals(PmdResponseCode.FEATURES_READ_RESPONSE))
                {
                    // Byte that contains which measurements are supported
                    byte settingsByte = data[1];

                    readAvailableMeasurements = true;
                    EcgSupported = (settingsByte & (byte)SupportedStreamsCode.ECG_SUPPORTED) != 0;
                    PpgSupported = (settingsByte & (byte)SupportedStreamsCode.PPG_SUPPORTED) != 0;
                    AccSupported = (settingsByte & (byte)SupportedStreamsCode.ACC_SUPPORTED) != 0;
                    PpiSupported = (settingsByte & (byte)SupportedStreamsCode.PPI_SUPPORTED) != 0;
                }
                else if(responseCode.Equals(PmdResponseCode.CONTROL_POINT_RESPONSE))
                {
                    // Response from Streaming Request
                    opCode = (PmdControlPointCommand)data[1];
                    measurementType = (MeasurementType)data[2];
                    status = (PmdControlPointResponseCode)data[3];
                    more = data.Length > 4 && data[4] != 0;

                    // Response is from setup of streaming
                    if (opCode == PmdControlPointCommand.GET_MEASUREMENT_SETTINGS)
                    {
                        short offset = 5; // Variable to keep track of where to read the byte array
                        ushort array_count = 0;
                        // Translate the setup bytes to text when answer includes data
                        switch (measurementType)
                        {
                            case MeasurementType.ECG:
                                // SAMPLE_RATE: 19 00 = 25hz , 32 00 = 50hz, 64 00 = 100hz, C8 00 = 200hz
                                streamSettingsString += $"\t{(MeasurementSettingType)data[offset++]}:";
                                // Read array_count and print the sample rate in correct units
                                array_count = data[offset++];
                                for (int i = 0; i < array_count; i++)
                                {
                                    streamSettingsString += $"\t{BitConverter.ToUInt16(data, offset)}Hz";
                                    offset += 2;
                                }

                                // RESOLUTION: 10 00 = 16bit
                                streamSettingsString += $"\n\t{(MeasurementSettingType)data[offset++]}:";
                                // Read array_count and print the sample rate in correct units
                                array_count = data[offset++];
                                for (int i = 0; i < array_count; i++)
                                {
                                    streamSettingsString += $"\t{BitConverter.ToUInt16(data, offset)}-bit";
                                    offset += 2;
                                }
                                break;
                            case MeasurementType.ACC:
                                // SAMPLE_RATE: 19 00 = 25hz , 32 00 = 50hz, 64 00 = 100hz, C8 00 = 200hz
                                streamSettingsString += $"\t{(MeasurementSettingType)data[offset++]}:";
                                // Read array_count and print the sample rate in correct units
                                array_count = data[offset++];
                                for (int i = 0; i < array_count; i++)
                                {
                                    streamSettingsString += $"\t{BitConverter.ToUInt16(data, offset)}Hz";
                                    offset += 2;
                                }

                                // RESOLUTION: 10 00 = 16bit
                                streamSettingsString += $"\n\t{(MeasurementSettingType)data[offset++]}:";
                                // Read array_count and print the sample rate in correct units
                                array_count = data[offset++];
                                for (int i = 0; i < array_count; i++)
                                {
                                    streamSettingsString += $"\t{BitConverter.ToUInt16(data, offset)}-bit";
                                    offset += 2;
                                }

                                // RANGE: 02 00 = 2G , 04 00 = 4G , 08 00 = 8G
                                streamSettingsString += $"\n\t{(MeasurementSettingType)data[offset++]}:";
                                // Read array_count and print the sample rate in correct units
                                array_count = data[offset++];
                                for (int i = 0; i < array_count; i++)
                                {
                                    streamSettingsString += $"\t{BitConverter.ToUInt16(data, offset)}G";
                                    offset += 2;
                                }
                                break;
                            default:
                                break;
                        }
                    }

                    // COPY OF MORE PARAMETERS WHEN THEY EXIST
                    if (data.Length > 5)
                    {
                        // Copy the remaining data in a new variable
                        byte[] additional_data = new byte[data.Length - 5];
                        Array.Copy(data, 5, additional_data, 0, data.Length - 5);

                        DataWriter writer = new DataWriter();
                        writer.WriteBytes(additional_data);
                        parameters = writer.DetachBuffer();
                    }

                }
            }

            public override string ToString()
            {
                string text = $"PmdControlPoint >> ";

                if (readAvailableMeasurements)
                text += $"Available Measurements:" +
                        $"\n\t- EcgSupported:\t{EcgSupported}" +
                        $"\n\t- PpgSupported:\t{PpgSupported}" +
                        $"\n\t- AccSupported:\t{AccSupported}" +
                        $"\n\t- PpiSupported:\t{PpiSupported}\n";

                if (!opCode.Equals(PmdControlPointCommand.NULL))
                {
                    text += $"Status: {status} | opCode: {opCode} | Feature: {measurementType} \n";

                    if (streamSettingsString.CompareTo("") != 0)
                        text += $"{streamSettingsString}";
                }
                return text;
            }
        }

        /// <summary>
        /// Structure of data received from PMD Streaming. This class maps
        /// to EcgData or AccData according the `MeasurementType`
        /// </summary>
        public class PmdDataResponse
        {
            public string stringHex = "";
            public MeasurementType measurementType;
            public ulong timestamp;
            public FrameType frameType; // Each sample is 3, 6 or 9 bytes?

            public int numSamples;      // Samples in
            public string streamString = "";

            public static EcgData ECG = new EcgData();
            public static AccData ACC = new AccData();

            public PmdDataResponse()
            {
                SetDefaultValues();
            }

            private void SetDefaultValues()
            {
                stringHex = "";
                measurementType = MeasurementType.UNKNOWN_TYPE;
                timestamp = 0;     // last sample timestamp in nS
                numSamples = 0;      // Samples in
                frameType = FrameType.NULL;
                //EcgData and AccData are defaulted inside their own class.
                streamString = "";
            }

            public void UpdateData(byte[] data)
            {
                // Restart values before setting new values
                SetDefaultValues();

                stringHex = BitConverter.ToString(data);
                measurementType = (MeasurementType)data[0];
                timestamp = BitConverter.ToUInt64(data, 1); // Reads 8 bytes from array
                frameType = (FrameType)data[9];
                numSamples = (data.Length - 10) / 3; // Reduce the header, each sample is 3 bytes

                switch (measurementType)
                {
                    case MeasurementType.ECG:
                        ECG.UpdateData(data);
                        break;
                    case MeasurementType.ACC:
                        ACC.UpdateData(data);
                        break;
                    default:
                        break;
                }

            }
            public override string ToString()
            {
                string text = $"PmdDataResponse >> Measurement Type: {measurementType}";
                text += $"\tTimestamp={timestamp} nS | Num Samples={numSamples}";

                if (streamString.CompareTo("") != 0)
                    text += $"Data: \n{streamString}\n";

                return text;
            }
        }

        /// <summary>
        /// Structure of data received from Battery Service
        /// </summary>
        public class BatteryData
        {
            public ushort battery; // 16-bit

            public BatteryData()
            {
                SetDefaultValues();
            }

            private void SetDefaultValues()
            {
                battery = 0;
            }

            public void UpdateData(byte[] value)
            {
                // Restart values before setting new values
                SetDefaultValues();

                // Converts HEX number to INT
                battery = value[0];
            }

            public override string ToString()
            {
                return $"BatteryData >> Battery Level = {battery}%";
            }

            public string ToStringValue()
            {
                return $"{battery}%";
            }
        }

        /// <summary>
        /// Structure of data received from Heart Rate Measurement Service
        /// </summary>
        public class HeartRateMeasurementData
        {
            public int size;
            public byte flagsByte;

            // FLAGS BYTE
            public bool formatUINT16 = false;           // bit0   | 0:UINT8, 1:UINT16
            public byte sensorContact = 0;                  // bit1-2 | NOT USED >> Sensor contact feature
            public bool hasEnergyExpenditure = false;   // bit3   | 1: Includes Values Energy Expenditure
            public bool hasRRinterval = false;          // bit4   | 1: Values RR interval are present
                                                        // bit5-8 | RESERVED
            public ushort HR = 0;                       // Heart Rate Value | Unit: beats per min
            public ushort EE = 0;                       // Energy Expended | Unit: Kilo Joules
            public float  RR = 0;                       // RR-interval | Unit: ms

            public HeartRateMeasurementData()
            {
                SetDefaultValues();
            }

            private void SetDefaultValues()
            {
                size = 0;
                flagsByte = 0;

                // FLAGS BYTE
                formatUINT16 = false;
                sensorContact = 0;
                hasEnergyExpenditure = false;
                hasRRinterval = false;

                HR = 0;
                EE = 0;
                RR = 0;
        }

            public void UpdateData(byte[] value)
            {
                // Restart values before setting new values
                SetDefaultValues();

                size = value.Length;
                flagsByte = value[0];

                formatUINT16            = (flagsByte & (0x01)) != 0;
                sensorContact           = (byte)(flagsByte & (0x06));
                hasEnergyExpenditure    = (flagsByte & (0x08)) != 0;
                hasRRinterval           = (flagsByte & (0x10)) != 0;

                // Move pointer to read values from specific bytes
                int offset = 1;

                if (formatUINT16) // UINT16
                {
                    HR = BitConverter.ToUInt16(value, offset); // Takes two bytes for UINT16
                    offset += 2; // Next value is two bytes away
                }
                else // UINT8
                {
                    HR = value[offset];
                    offset++;   // Next value is one byte away
                }

                if (hasEnergyExpenditure)
                {
                    // If has EE, bytes 2 and 3 are EE
                    EE = BitConverter.ToUInt16(value, offset);
                    offset += 2;
                }

                if (hasRRinterval)
                {
                    // If has RR interval data
                    ushort receivedRR = BitConverter.ToUInt16(value, offset);
                    RR = (float)receivedRR * 1000 / 1024; // Convert from resolution 1/1024 seconds to ms
                }
            }

            public override string ToString()
            {
                string text = $"HeartRateMeasurementData >> ";
                text += $"packetSize:{size} | HR_UINT16:{formatUINT16} | has_EE:{hasEnergyExpenditure} | has_RR:{hasRRinterval} \n";

                text += $"\tHR={HR}bpm";
                if (hasEnergyExpenditure)
                    text += $"| EE={EE}kJ ";
                if (hasRRinterval)
                    text += $"| RR={RR:F3}ms";

                return text;
            }
        }


        /// <summary>
        /// How is each sample coded in the data stream?
        /// </summary>
        public enum FrameType : byte
        {
            NULL = 0xFF,  // DEFAULT VALUE - INVALID
            T3_BYTES = 0, // Type is: ACC=x,y,z * 8-bit  | ECG=24-bit uV
            T6_BYTES = 1, // Type is: ACC=x,y,z * 16-bit | ECG=(Not available)
            T9_BYTES = 2, // Type is: ACC=x,y,z * 24-bit | ECG=(Not available)
        }

        /// <summary>
        /// Structure of data received from PMD for ECG
        /// </summary>
        public class EcgData
        {
            public struct EcgSample
            {
                public int microVolts; // Samples in signed microvolts
            }

            public List<EcgSample> ecgSamples = new List<EcgSample>();

            public EcgData()
            {
                SetDefaultValues();
            }

            private void SetDefaultValues()
            {
                ecgSamples.Clear();
            }

            public void UpdateData(byte[] data)
            {
                // Restart values before setting new values
                SetDefaultValues();

                int offset = 10; // Variable to keep track of where to read the byte array

                // Print Debug text
                string text = "Samples: ";
                string textHEX = "Samples HEX: ";

                while (offset < data.Length)
                {
                    EcgSample sample = new EcgSample();

                    // Take only three bytes and put it in a four-byte-long array
                    byte[] value = new byte[] { 0x00, data[offset], data[offset+1], data[offset+2] };
                    sample.microVolts = BitConverter.ToInt32(value, 0);
                    offset += 3; // Every sample is 3-Bytes

                    ecgSamples.Add(sample);

                    // Print results
                    text += $"{sample.microVolts},";
                    textHEX += $"{BitConverter.ToString(data, offset-3, 3).Replace("-", string.Empty)},";
                }

                /// SEND DATA THROUGH LSL
                System.Diagnostics.Debug.WriteLine(text);
                System.Diagnostics.Debug.WriteLine(textHEX);
            }
        }

        /// <summary>
        /// Structure of data received from PMD for Accelerometer
        /// </summary>
        public class AccData
        {
            public struct AccSample
            {
                public readonly int x;
                public readonly int y;
                public readonly int z;

                public AccSample(int x, int y, int z)
                {
                    this.x = x;
                    this.y = y;
                    this.z = z;
                }
            }
            public List<AccSample> accSamples = new List<AccSample>();

            public AccData()
            {
                SetDefaultValues();
            }

            private void SetDefaultValues()
            {
                accSamples.Clear();
            }

            public void UpdateData(byte[] data)
            {
                // Restart values before setting new values
                SetDefaultValues();

                int offset = 9; // Variable to keep track of where to read the byte array
                FrameType type = (FrameType)data[offset]; // Encoded in 3, 6 or 9 bytes
                offset++;

                // Step: How many bytes to move to find other axis data.
                int step = (int)type + 1; // If type=1(16-bit), next value is 2 bytes away.

                // Print Debug text
                string text = "Samples: ";
                
                // Intermediate variables to parse HEX data
                int x=0, y=0, z=0;
                byte[] value;

                while (offset < data.Length)
                {
                    // Read specific number of bytes
                    switch (type)
                    {
                        case FrameType.T3_BYTES:
                            x = data[offset];
                            offset += step;
                            y = data[offset];
                            offset += step;
                            z = data[offset];
                            offset += step;
                            break;
                        case FrameType.T6_BYTES:
                            x = BitConverter.ToInt16(data, offset);
                            offset += step;
                            y = BitConverter.ToInt16(data, offset);
                            offset += step;
                            z = BitConverter.ToInt16(data, offset);
                            offset += step;
                            break;
                        case FrameType.T9_BYTES:
                            value = new byte[] { 0x00, data[offset], data[offset + 1], data[offset + 2] };
                            x = BitConverter.ToInt32(value, 0);
                            offset += step;
                            value = new byte[] { 0x00, data[offset], data[offset + 1], data[offset + 2] };
                            y = BitConverter.ToInt32(value, 0);
                            offset += step;
                            value = new byte[] { 0x00, data[offset], data[offset + 1], data[offset + 2] };
                            z = BitConverter.ToInt32(value, 0);
                            offset += step;
                            break;
                    }

                    accSamples.Add(new AccSample(x,y,z));

                    // Print results
                    text += $"{x}|{y}|{z},";
                }

                /// SEND DATA THROUGH LSL
                System.Diagnostics.Debug.WriteLine(text);
            }
        }

        /* PPG ANALYSIS STILL IN JAVA

        public static class PpgData
        {
            public enum PpgFrameType
            {
                PPG0_TYPE(0),
            AFE4410(1),
            AFE4404(2),
            PPG1_TYPE(3),
            ADPD4000(4),
            AFE_OPERATION_MODE(5),
            SPORT_ID(6),
            UNKNOWN_TYPE(0xff);
            private int numVal;
            PpgFrameType(int numVal)
            {
                this.numVal = numVal;
            }
            public int getNumVal()
            {
                return numVal;
            }

            public static PpgFrameType fromId(byte id)
            {
                for (PpgFrameType type : values())
                {
                    if (type.numVal == id)
                    {
                        return type;
                    }
                }
                return UNKNOWN_TYPE;
            }
        }

        public class PpgSample
        {
            public List<Integer> ppgDataSamples;
            public int ppg0;
            public int ppg1;
            public int ppg2;
            public int ambient;
            public int ambient1;
            public long status;

            public PpgSample(List<Integer> ppgDataSamples, int ambient, int ambient1, long status)
            {
                this.ppgDataSamples = ppgDataSamples;
                this.ambient = ambient;
                this.ambient1 = ambient1;
                this.ppg0 = ppgDataSamples.get(0);
                this.ppg1 = ppgDataSamples.get(1);
                this.ppg2 = ppgDataSamples.get(2);
                this.status = status;
            }
        }
        public List<PpgSample> ppgSamples = new ArrayList<>();
        public long timeStamp;
        public byte type;

        public PpgData(byte[] value, long timeStamp, byte type)
        {
            this.timeStamp = timeStamp;
            this.type = type;
            final int step = 3;
            for (int i = 0; i < value.length;)
            {
                List<Integer> samples = new ArrayList<>();
                int ambient, ambient1 = 0;
                int count = type == 0 ? 3 : 16;
                while (count-- > 0)
                {
                    samples.add(BleUtils.convertArrayToSignedInt(value, i, step));
                    i += step;
                }
                ambient = BleUtils.convertArrayToSignedInt(value, i, step);
                i += step;
                long status = 0;
                if (type != 0)
                {
                    ambient1 = BleUtils.convertArrayToSignedInt(value, i, step);
                    i += step;
                    status = BleUtils.convertArrayToUnsignedLong(value, i, 4);
                    i += 4;
                }
                ppgSamples.add(new PpgSample(samples, ambient, ambient1, status));
            }
        }

        */
    }
}
