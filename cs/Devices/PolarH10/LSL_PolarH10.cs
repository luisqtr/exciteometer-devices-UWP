using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LSL.SAFE;

namespace ExciteOMeter.Devices
{
    /// <summary>
    /// Configures the StreamInfo and StreamOutlets
    /// of the Polar H10 data, and sends the data
    /// through the network
    /// </summary>
    class LSL_PolarH10
    {

        public enum MEASUREMENT_STREAM
        {
            HR,
            RRi,
            ECG,
            ACC,
        }

        public static string deviceId = "Polar H10 ";

        // Definition of Stream Information
        static liblsl.StreamInfo streamInfoHR;
        static liblsl.StreamInfo streamInfoRRi;
        static liblsl.StreamInfo streamInfoECG;
        static liblsl.StreamInfo streamInfoACC;

        // Definition of Stream Outlets
        public static liblsl.StreamOutlet streamHR;
        public static liblsl.StreamOutlet streamRRi;
        public static liblsl.StreamOutlet streamECG;
        public static liblsl.StreamOutlet streamACC;


        public LSL_PolarH10(string deviceIdentifier)
        {
            deviceId = deviceIdentifier;
            InitializeComponents();
        }

        private void InitializeComponents()
        {        
            // Heart Rate
            streamInfoHR = new liblsl.StreamInfo("HeartRate",                      // Name
                                                "ExciteOMeter",                    // Type
                                                1,                                 // Channels
                                                liblsl.IRREGULAR_RATE,             // Sampling Rate (Hz)
                                                liblsl.channel_format_t.cf_int16,  // Format
                                                deviceId);                         // Source ID (serial number) to identify source
            

            // RR-interval
            streamInfoRRi = new liblsl.StreamInfo("RRinterval",                    // Name
                                                "ExciteOMeter",                    // Type
                                                1,                                 // Channels
                                                liblsl.IRREGULAR_RATE,             // Sampling Rate (Hz)
                                                liblsl.channel_format_t.cf_float32,// Format
                                                deviceId);                         // Source ID (serial number) to identify source

            // ECG
            streamInfoECG = new liblsl.StreamInfo("RawECG",
                                                "ExciteOMeter",
                                                1,
                                                liblsl.IRREGULAR_RATE,
                                                liblsl.channel_format_t.cf_int32,   // 3B ECG microVolts
                                                deviceId);

            // ACC
            streamInfoACC = new liblsl.StreamInfo("RawACC",
                                                "ExciteOMeter",  
                                                3,                                  // X-Y-Z axes
                                                liblsl.IRREGULAR_RATE,
                                                liblsl.channel_format_t.cf_int32,   // Each axis can be 24-bit
                                                deviceId);
        }

        /// <summary>
        /// In case a streaming outlet needs a different chunk size
        /// than what it was initially setup with.
        /// </summary>
        /// <param name="type">Type of stream</param>
        /// <param name="chunk_size">New chunk size to use in the outlet </param>
        public static void OpenStreamOutlet(MEASUREMENT_STREAM type, int new_chunk_size = 1)
        {
            switch (type)
            {
                case MEASUREMENT_STREAM.HR:
                    streamHR = new liblsl.StreamOutlet(streamInfoHR); // Constant chunk_size = 1
                    break;
                case MEASUREMENT_STREAM.RRi:
                    streamRRi = new liblsl.StreamOutlet(streamInfoRRi); // Constant chunk_size = 1
                    break;
                case MEASUREMENT_STREAM.ECG:
                    // This can be setup in runtime depending on how many samples are received from the sensor
                    streamECG = new liblsl.StreamOutlet(streamInfoECG, chunk_size: new_chunk_size);
                    break;
                case MEASUREMENT_STREAM.ACC:
                    // This can be setup in runtime depending on how many samples are received from the sensor
                    streamACC = new liblsl.StreamOutlet(streamInfoACC, chunk_size: new_chunk_size);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Close the outlet
        /// </summary>
        /// <param name="type"></param>

        public static void CloseStreamOutlet(MEASUREMENT_STREAM type)
        {
            switch (type)
            {
                case MEASUREMENT_STREAM.HR:
                    if (streamHR != null) streamHR.Dispose();
                    break;
                case MEASUREMENT_STREAM.RRi:
                    if (streamRRi != null) streamRRi.Dispose();
                    break;
                case MEASUREMENT_STREAM.ECG:
                    if (streamECG != null) streamECG.Dispose();
                    break;
                case MEASUREMENT_STREAM.ACC:
                    if (streamACC != null) streamACC.Dispose();
                    break;
                default:
                    break;
            }
        }

    }
}
