# ECG BluetoothLE Project
ECG reading, displaying and logging over BLE with Xamarin.

This project is about transmitting ECG (electrocariogram) samples over BLE, and displaying the ECG graph using a Xamarin app.

An Arduino Nano 33 BLE (in conjunction with a AD3232 board) is used to sample and transmit the ECG signal.
The arduino samples 10 bit values with a sampling rate of up to 1000Hz (higher is theoretically possible, 23 byte ble MTU size limits) and packs them into 5 byte messages.

On the app side, the samples are unpacked, filtered and displayed in a graph using the SkiaSharp API.
