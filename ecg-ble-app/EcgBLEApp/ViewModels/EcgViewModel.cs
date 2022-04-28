using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.Permissions;
using Plugin.Permissions.Abstractions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace EcgBLEApp.ViewModels
{
    public class EcgViewModel : BaseViewModel
    {
        private static IAdapter Adapter => CrossBluetoothLE.Current.Adapter;
        private static ICharacteristic PollingRateChar { get; set; }
        public EcgViewModel()
        {
            Title = "Ecg";
            StartScanningCommand = new Command(async () =>
            {
                try
                {
                    var status = await CrossPermissions.Current.CheckPermissionStatusAsync<LocationPermission>();
                    if (status != Plugin.Permissions.Abstractions.PermissionStatus.Granted)
                    {
                        if (await CrossPermissions.Current.ShouldShowRequestPermissionRationaleAsync(Permission.Location))
                        {

                        }

                        status = await CrossPermissions.Current.RequestPermissionAsync<LocationPermission>();
                    }

                    if (status == Plugin.Permissions.Abstractions.PermissionStatus.Granted)
                    {
                        //Query permission
                    }
                    else if (status != Plugin.Permissions.Abstractions.PermissionStatus.Unknown)
                    {
                        //location denied
                    }
                }
                catch
                {
                    //Something went wrong
                }

                _ = Adapter.StartScanningForDevicesAsync(new Guid[] { ble.ECG_SERVICE_UUID });
                OnPropertyChanged(nameof(IsScanning));
            });

            StopScanningCommand = new Command(async () =>
            {
                await Adapter.StopScanningForDevicesAsync();
                OnPropertyChanged(nameof(IsScanning));
            }, () => Adapter.IsScanning);

            Adapter.DeviceDiscovered += (s, e) =>
                OnPropertyChanged(nameof(DiscoveredDevices));

            Adapter.ScanTimeoutElapsed += (s, e) => OnPropertyChanged(nameof(IsScanning));

            ConnectToDeviceCommand = new Command<IDevice>(async (device) =>
            {
                try
                {
                    await ble.ConnectToDevice(device);

                    ConnectedDevice = device;
                    OnPropertyChanged(nameof(IsConnected));
                    OnPropertyChanged("");

                    var service = await device.GetServiceAsync(ble.ECG_SERVICE_UUID);
                    var signalChar = await service.GetCharacteristicAsync(ble.ECG_SIGNAL_CHAR_UUID);
                    var pollingRateChar = await service.GetCharacteristicAsync(ble.POLLING_RATE_CHAR_UUID);

                    signalChar.ValueUpdated += SignalChar_ValueUpdated;
                    await signalChar.StartUpdatesAsync();

                    pollingRateChar.ValueUpdated += PollingRateChar_ValueUpdated;

                    PollingRate = BitConverter.ToUInt16(await pollingRateChar.ReadAsync());

                    PollingRateChar = pollingRateChar;
                }
                catch
                {
                }
            });

            DisconnectCommand = new Command(async () =>
            {
                await Adapter.DisconnectDeviceAsync(ConnectedDevice);

                ConnectedDevice = null;
                OnPropertyChanged(nameof(IsConnected));
                Values.Clear();
            });

            UpdatePollingRateCommand = new Command(async () =>
            {
                if (PollingRateChar != null && PollingRateChar.CanWrite)
                {
                    await PollingRateChar.WriteAsync(BitConverter.GetBytes((ushort)PollingRate));
                }
            }); 

            _simulator.NewMessage += Simulator_NewMessage;
            //_simulator.Start();
        }

        private void PollingRateChar_ValueUpdated(object sender, Plugin.BLE.Abstractions.EventArgs.CharacteristicUpdatedEventArgs e)
        {
            PollingRate = (int)BitConverter.ToUInt16(e.Characteristic.Value);
        }


        private readonly ushort[] _buffer = new ushort[ble.ValuesPerMessage];
        private void SignalChar_ValueUpdated(object sender, Plugin.BLE.Abstractions.EventArgs.CharacteristicUpdatedEventArgs e)
        {
            Value = BitConverter.ToString(e.Characteristic.Value);

            byte[] arr = e.Characteristic.Value;

            ble.ParseValues(arr, _buffer, 0);

            for (int i = 0; i < ble.ValuesPerMessage; i++)
            {
                Values.Add(_buffer[i]);
            }
        }

        private void Simulator_NewMessage(byte[] arr)
        {
            Value = BitConverter.ToString(arr);

            ble.ParseValues(arr, _buffer, 0);

            for (int i = 0; i < ble.ValuesPerMessage; i++)
            {
                Values.Add(_buffer[i]);
            }
        }

        private readonly DataSimulator _simulator = new DataSimulator();

        byte ReverseByte(byte b)
        {
            b = (byte)((b & 0b11110000) >> 4 | (b & 0b00001111) << 4);
            b = (byte)((b & 0b11001100) >> 2 | (b & 0b00110011) << 2);
            b = (byte)((b & 0b10101010) >> 1 | (b & 0b01010101) << 1);
            return b;
        }

        public ICommand StartScanningCommand { get; }

        public ICommand StopScanningCommand { get; }

        public bool IsScanning => Adapter.IsScanning;

        public IReadOnlyList<IDevice> DiscoveredDevices => Adapter.DiscoveredDevices;

        public ICommand ConnectToDeviceCommand { get; }

        public ICommand DisconnectCommand { get; }

        public ICommand UpdatePollingRateCommand { get; }

        private string _value;
        public string Value
        {
            get
            {
                return _value;
            }
            set
            {
                _value = value;
                OnPropertyChanged();
            }
        }

        private int _pollingRate;
        public int PollingRate
        {
            get
            {
                return _pollingRate;
            }
            set
            {
                _pollingRate = value;
                OnPropertyChanged();
            }
        }

        public bool IsConnected => ConnectedDevice != null;

        private IDevice _connectedDevice;
        public IDevice ConnectedDevice
        {
            get
            {
                return _connectedDevice;
            }
            private set
            {
                _connectedDevice = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<int> Values { get; } = new ObservableCollection<int>();
    }
}