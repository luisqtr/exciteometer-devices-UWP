//*********************************************************
//
// Author: Luis Quintero
// Date: 10/07/2020
// Project: Excite-O-Meter / XR4ALL
//
//*********************************************************

using System;
using System.Collections.Generic;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace SDKTemplate
{
    class BLE_PolarH10
    {
        // Battery
        public static readonly Guid BATTERY_SERVICE              = new Guid("0000180f-0000-1000-8000-00805f9b34fb");
        public static readonly Guid BATTERY_LEVEL_CHARACTERISTIC = new Guid("00002a19-0000-1000-8000-00805f9b34fb");

        // Heart Rate
        public static readonly Guid HR_SERVICE              = new Guid("0000180D-0000-1000-8000-00805f9b34fb");
        public static readonly Guid BODY_SENSOR_LOCATION    = new Guid("00002a38-0000-1000-8000-00805f9b34fb"); // NOT USED
        public static readonly Guid HR_MEASUREMENT          = new Guid("00002a37-0000-1000-8000-00805f9b34fb");

        // Streaming Measurement
        public static readonly Guid PMD_SERVICE     = new Guid("FB005C80-02E7-F387-1CAD-8ACD2D8DF0C8");
        public static readonly Guid PMD_DATA        = new Guid("FB005C82-02E7-F387-1CAD-8ACD2D8DF0C8");
        public static readonly Guid PMD_CP          = new Guid("FB005C81-02E7-F387-1CAD-8ACD2D8DF0C8");

        public enum PmdResponseCode
        {
            FEATURES_READ_RESPONSE = 0x0F,
            CONTROL_POINT_RESPONSE = 0xF0,
        }

        public enum PmdControlPointCommand
        {
            NULL = 0x00,
            GET_MEASUREMENT_SETTINGS = 0x01,
            REQUEST_MEASUREMENT_START = 0x02,
            STOP_MEASUREMENT = 0x03,
        }
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

        public enum MeasurementType
        {
            // Supported by Polar H10
            ECG = 0x00,     // Electrocardiographic Signal
            ACC = 0x02,     // Accelerometer Signal

            // Not supported by Polar H10
            PPG = 0x01,
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
        /// <param name="command"></param>
        /// <param name="sensor"></param>
        /// <param name="additionalData"></param>
        /// <returns></returns>
        public static IBuffer CreateStreamingRequest(PmdControlPointCommand command, MeasurementType sensor /*ADD PARAMETERS FOR SPECIFIC SETUP OF STREAMING*/)
        {
            int numBytes = 0;
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

        public class PmdControlPointResponse
        {
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
            bool readAvailableMeasurements = false;
            bool EcgSupported;
            bool PpgSupported;
            bool AccSupported;
            bool PpiSupported;


            // When response from Streaming Requests

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

            public PmdControlPointResponse(byte[] data)
            {
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
                        // Translate the setup bytes to text when answer includes data
                        switch (measurementType)
                        {
                            case MeasurementType.ECG:
                                                    // SAMPLE_RATE: 19 00 = 25hz , 32 00 = 50hz, 64 00 = 100hz, C8 00 = 200hz
                                streamSettingsString = $"\t{(MeasurementSettingType)data[5]} = {BitConverter.ToUInt16(data, 7)} Hz\n" +
                                    // RESOLUTION: 10 00 = 16bit
                                    $"\t{(MeasurementSettingType)data[9]} = {BitConverter.ToUInt16(data, 11)} bit\n";
                                break;
                            case MeasurementType.ACC:
                                                        // SAMPLE_RATE: 19 00 = 25hz , 32 00 = 50hz, 64 00 = 100hz, C8 00 = 200hz
                                streamSettingsString = $"\t{(MeasurementSettingType)data[5]} = {BitConverter.ToUInt16(data, 7)} Hz\n" +
                                    // RESOLUTION: 10 00 = 16bit
                                    $"\t{(MeasurementSettingType)data[9]} = {BitConverter.ToUInt16(data, 11)} bit\n" +
                                    // RANGE: 02 00 = 2G , 04 00 = 4G , 08 00 = 8G
                                    $"\t{(MeasurementSettingType)data[13]} = {BitConverter.ToUInt16(data, 15)} G\n";
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
                        text += $"Streaming Settings: \n{streamSettingsString}\n";
                }
                    
                    

                return text;
            }
        }

        public class PmdDataResponse
        {
            // TODO: Implement classes to parse streaming from sensor
        }

        public class BatteryData
        {
            public ushort battery; // 16-bit
            public BatteryData(byte[] value)
            {
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

        public class HeartRateMeasurementData
        {
            public int size;
            public byte flagsByte;

            // FLAGS BYTE
            public bool formatUINT16 = false;           // bit0   | 0:UINT8, 1:UINT16
            public byte sensorContact;                  // bit1-2 | NOT USED >> Sensor contact feature
            public bool hasEnergyExpenditure = false;   // bit3   | 1: Includes Values Energy Expenditure
            public bool hasRRinterval = false;          // bit4   | 1: Values RR interval are present
                                                        // bit5-8 | RESERVED
            public ushort HR = 0;                       // Heart Rate Value | Unit: beats per min
            public ushort EE = 0;                       // Energy Expended | Unit: Kilo Joules
            public float  RR = 0;                       // RR-interval | Unit: ms

            public HeartRateMeasurementData(byte[] value)
            {
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

        public class EcgData
        {
            public class EcgSample
            {
                // samples in signed microvolts
                //public PmdEcgDataType type;
                public long timeStamp;
                public int microVolts;
                public bool overSampling;
                public byte skinContactBit;
                public byte contactImpedance;

                public byte ecgDataTag;
                public byte paceDataTag;
            }
            public long timeStamp;
            public List<EcgSample> ecgSamples = new List<EcgSample>();

            public EcgData(byte type, byte[] value, long timeStamp)
            {
                int offset = 0;
                this.timeStamp = timeStamp;
                while (offset < value.Length)
                {
                    EcgSample sample = new EcgSample();
                    //sample.type = PmdEcgDataType.values()[type];
                    sample.timeStamp = timeStamp;

                    //if (type == 0)
                    //{ // production
                    sample.microVolts = 0;///BleUtils.convertArrayToSignedInt(value, offset, 3);
                    //}
                    offset += 3;
                    ecgSamples.Add(sample);
                }
            }
        }

        public class AccData
        {
            public class AccSample
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
            public readonly List<AccSample> accSamples = new List<AccSample>();
            public readonly long timeStamp;

            public AccData(byte type, byte[] value, long timeStamp)
            {
                int offset = 0;
                this.timeStamp = timeStamp;
                int resolution = (type + 1) * 8;
                int z, y, x, step = (int)Math.Ceiling((double)resolution / 8.0);
                while (offset < value.Length)
                {
                    x = 100;//BleUtils.convertArrayToSignedInt(value, offset, step);
                    offset += step;
                    y = 200;//BleUtils.convertArrayToSignedInt(value, offset, step);
                    offset += step;
                    z = 300;//BleUtils.convertArrayToSignedInt(value, offset, step);
                    offset += step;
                    accSamples.Add(new AccSample(x, y, z));
                }
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
