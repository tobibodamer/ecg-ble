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

namespace ecg_ble_app.ViewModels
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

        const int SEND_BUFFER_SIZE = 20;
        const int PACKET_SIZE = 5;
        private readonly static int PacketsPerMessage = SEND_BUFFER_SIZE / PACKET_SIZE;
        private readonly static int ValuesPerMessage = PacketsPerMessage * 4;

        private static void ParseValues(byte[] arr, ushort[] destArray, int index)
        {
            for (int i = 0; i < 4; i++)
            {
                int offset = i * PACKET_SIZE;

                uint a = (arr[offset + 0] & 0b11111111u) << 2 | ((arr[offset + 1] & 0b11000000u) >> (8 - 2));
                uint b = (arr[offset + 1] & 0b00111111u) << 4 | ((arr[offset + 2] & 0b11110000u) >> (8 - 4));
                uint c = (arr[offset + 2] & 0b00001111u) << 6 | ((arr[offset + 3] & 0b11111100u) >> (8 - 6));
                uint d = (arr[offset + 3] & 0b00000011u) << 8 | ((arr[offset + 4] & 0b11111111u) >> (8 - 8));

                destArray[index++] = (ushort)a;
                destArray[index++] = (ushort)b;
                destArray[index++] = (ushort)c;
                destArray[index++] = (ushort)d;
            }
        }


        private readonly ushort[] _buffer = new ushort[PacketsPerMessage * 4];
        private void SignalChar_ValueUpdated(object sender, Plugin.BLE.Abstractions.EventArgs.CharacteristicUpdatedEventArgs e)
        {
            Value = BitConverter.ToString(e.Characteristic.Value);

            byte[] arr = e.Characteristic.Value;

            ParseValues(arr, _buffer, 0);

            for (int i = 0; i < ValuesPerMessage; i++)
            {
                Values.Add(_buffer[i]);
            }
        }

        private void Simulator_NewMessage(byte[] arr)
        {
            Value = BitConverter.ToString(arr);

            ParseValues(arr, _buffer, 0);

            for (int i = 0; i < ValuesPerMessage; i++)
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