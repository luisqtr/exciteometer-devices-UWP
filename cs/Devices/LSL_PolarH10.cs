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
        public string deviceId = "Polar H10 ";

        // Definition of Stream Information
        public liblsl.StreamInfo streamInfoHR;
        public liblsl.StreamInfo streamInfoRRi;

        // Definition of Stream Outlets
        public liblsl.StreamOutlet streamHR;
        public liblsl.StreamOutlet streamRRi;


        public LSL_PolarH10(string deviceIdentifier)
        {
            deviceId = deviceIdentifier;
            InitializeComponents();
        }

        public void InitializeComponents()
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
            streamInfoRRi = new liblsl.StreamInfo("RR interval",                     // Name
                                                "Markers",                         // Type
                                                1,                                 // Channels
                                                liblsl.IRREGULAR_RATE,             // Sampling Rate (Hz)
                                                liblsl.channel_format_t.cf_float32,// Format
                                                deviceId);                         // Source ID (serial number) to identify source
            streamRRi = new liblsl.StreamOutlet(streamInfoRRi);
        }




    }
}
