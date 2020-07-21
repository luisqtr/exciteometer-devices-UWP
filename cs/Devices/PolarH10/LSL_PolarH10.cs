using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LSL;
using Windows.ApplicationModel.UserDataAccounts.SystemAccess;
using Windows.Devices.Enumeration;
using Windows.Media.Core;

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

        public string deviceId = "Polar H10 ";

        // Definition of Stream Information
        public liblsl.StreamInfo streamInfoHR;
        public liblsl.StreamInfo streamInfoRRi;
        public liblsl.StreamInfo streamInfoECG;
        public liblsl.StreamInfo streamInfoACC;

        // Definition of Stream Outlets
        public liblsl.StreamOutlet streamHR;
        public liblsl.StreamOutlet streamRRi;
        public liblsl.StreamOutlet streamECG;
        public liblsl.StreamOutlet streamACC;


        public LSL_PolarH10(string deviceIdentifier)
        {
            deviceId = deviceIdentifier;
            InitializeComponents();
        }

        private void InitializeComponents()
        {        
            // Heart Rate
            streamInfoHR = new liblsl.StreamInfo("Heart Rate",                     // Name
                                                "Markers",                         // Type
                                                1,                                 // Channels
                                                liblsl.IRREGULAR_RATE,             // Sampling Rate (Hz)
                                                liblsl.channel_format_t.cf_int16,  // Format
                                                deviceId);                         // Source ID (serial number) to identify source
            streamHR = new liblsl.StreamOutlet(streamInfoHR);

            // RR-interval
            streamInfoRRi = new liblsl.StreamInfo("RR interval",                   // Name
                                                "Markers",                         // Type
                                                1,                                 // Channels
                                                liblsl.IRREGULAR_RATE,             // Sampling Rate (Hz)
                                                liblsl.channel_format_t.cf_float32,// Format
                                                deviceId);                         // Source ID (serial number) to identify source
            streamRRi = new liblsl.StreamOutlet(streamInfoRRi);

            // ECG
            streamInfoECG = new liblsl.StreamInfo("Raw ECG",
                                                "EEG",
                                                1,
                                                liblsl.IRREGULAR_RATE,
                                                liblsl.channel_format_t.cf_int32,   // 3B ECG microVolts
                                                deviceId);
            // streamECG = new liblsl.StreamOutlet(streamInfoECG);  // This is setup with SetupStreamOutlet() after the first packet is received

            // ACC
            streamInfoACC = new liblsl.StreamInfo("Raw Accelerometer",
                                                "EEG",  
                                                3,                                  // X-Y-Z axes
                                                liblsl.IRREGULAR_RATE,
                                                liblsl.channel_format_t.cf_int32,   // Each axis can be 24-bit
                                                deviceId);
            // streamACC = new liblsl.StreamOutlet(streamInfoACC); // This is setup with SetupStreamOutlet() after the first packet is received
        }

        /// <summary>
        /// In case a streaming outlet needs a different chunk size
        /// than what it was initially setup with.
        /// </summary>
        /// <param name="type">Type of stream</param>
        /// <param name="chunk_size">New chunk size to use in the outlet </param>
        public void SetupStreamOutlet(MEASUREMENT_STREAM type, int new_chunk_size)
        {
            switch (type)
            {
                case MEASUREMENT_STREAM.HR:
                    // This is setup from constructor
                    break;
                case MEASUREMENT_STREAM.RRi:
                    // This is setup from constructor
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

    }
}
