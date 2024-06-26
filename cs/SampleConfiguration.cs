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
using System.Collections.Generic;
using Windows.ApplicationModel.Background;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace ExciteOMeter
{
    public partial class MainPage : Page
    {
        public const string FEATURE_NAME = "BLE to LSL";

        List<Scenario> scenarios = new List<Scenario>
        {
            new Scenario() { Title="Discover servers", ClassType=typeof(Scenario1_Discovery) },
            new Scenario() { Title="Setup Polar H10", ClassType=typeof(ScenarioPolarH10) },
            //new Scenario() { Title="Connect to a server", ClassType=typeof(Scenario2_Client) },
            // new Scenario() { Title="Server Foreground", ClassType=typeof(Scenario3_ServerForeground) },
        };

        public string SelectedBleDeviceId;
        public string SelectedBleDeviceName = "No device selected";
    }

    public class Scenario
    {
        public string Title { get; set; }
        public Type ClassType { get; set; }
    }
}
