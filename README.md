# Excite-O-Meter

Source code of the Universal Windows Platform (UWP) application for the [Excite-O-Meter](https://github.com/luisqtr/exciteometer)

- App published on https://www.microsoft.com/store/apps/9PFMNFQJB99Q

This repo is a mirror that allows building a local copy enabling developer mode.

## Publishing

To create another build of the project go to `Project`>`Publish`>`Create App Packages`

The project is associated to the project in the Microsoft Partner Center, and the generated file `bundle.msixupload` should be checked by the Microsoft Cert Kit and then uploaded for publishing.

*Developed in Visual Studio 2022 Community Edition*

## Contributing

If you make a contribution on the repository, please make a Pull Request so I can sign a new build and update the application in the Windows Store.

## Related topics

### Conceptual

* Documentation
  * [Bluetooth GATT Client](https://msdn.microsoft.com/windows/uwp/devices-sensors/gatt-client)
  * [Bluetooth GATT Server](https://msdn.microsoft.com/windows/uwp/devices-sensors/gatt-server)
  * [Bluetooth LE Advertisements](https://docs.microsoft.com/windows/uwp/devices-sensors/ble-beacon)
* [Windows Bluetooth Core Team Blog](https://blogs.msdn.microsoft.com/btblog/)
* Videos from Build 2017
  * [Introduction to the Bluetooth LE Explorer app](https://channel9.msdn.com/Events/Build/2017/P4177)
    * [Source code](https://github.com/Microsoft/BluetoothLEExplorer)
    * [Install it from the Microsoft Store](https://www.microsoft.com/store/apps/9n0ztkf1qd98)
  * [Unpaired Bluetooth LE Device Connectivity](https://channel9.msdn.com/Events/Build/2017/P4178)
  * [Bluetooth GATT Server](https://channel9.msdn.com/Events/Build/2017/P4179)

## System requirements

**Client:** Windows 10 Anniversary Edition

**Server:** Windows Server 2016 Technical Preview

**Phone:** Windows 10 Anniversary Edition

## Build the sample

1. If you download the samples ZIP, be sure to unzip the entire archive, not just the folder with the sample you want to build. 
2. Start Microsoft Visual Studio and select **File** \> **Open** \> **Project/Solution**.
3. Double-click the Visual Studio Solution (.sln) file.
4. Press Ctrl+Shift+B, or select **Build** \> **Build Solution**.

## Run the sample

The next steps depend on whether you just want to deploy the sample or you want to both deploy and run it.

### Deploying the sample

- Select Build > Deploy Solution. 

### Deploying and running the sample

- To debug the sample and then run it, press F5 or select Debug >  Start Debugging. To run the sample without debugging, press Ctrl+F5 or selectDebug > Start Without Debugging. 

---

## Previous errors with LibLSL in Windows store

(Fixed on 2023-03-26, the bundle passes the tests with LSL.dll)

Since the LSL library uses API that are not compatible with UWP it needed to be recompiled using this [reference](https://docs.microsoft.com/en-us/cpp/porting/how-to-use-existing-cpp-code-in-a-universal-windows-platform-app?view=vs-2019).

After generating the project in CMake for VS2022, it is necessary to:
- Add to the compiler options in `Properties>C/C++>Command Line` the following options to check compatibility with UWP: `/ZW:platform.winmd /EHsc`
- Add the linker options in `Properties>Linker>Command Line`: `/SAFESEH /DYNAMICBASE /NXCOMPAT /APPCONTAINER`

Adding the flag `/ZW` as suggested by the tutorial was not necessary, the workaround was found [here](https://docs.microsoft.com/en-us/cpp/build/reference/zw-windows-runtime-compilation?view=vs-2019)
