# ECG BluetoothLE Project
ECG reading, displaying and logging over BLE with Xamarin.

This project is about transmitting an ECG (electrocariogram) signal over BLE, and displaying the ECG graph in a Xamarin written app.

### Hardware side
An Arduino Nano 33 BLE (in conjunction with a [AD8232](https://www.analog.com/en/products/ad8232.html) board) is used to sample and transmit the ECG signal.
The arduino samples 10 bit values with a rate of up to 1000Hz (higher is theoretically possible, 23 byte BLE MTU size limits) and packs them into 4x5 byte messages.

See the [Arduino sketch](ecg1.ino)

### App side
On the app side, the samples are unpacked, filtered and displayed in a graph using the [SkiaSharp API](https://docs.microsoft.com/en-us/xamarin/xamarin-forms/user-interface/graphics/skiasharp/).
