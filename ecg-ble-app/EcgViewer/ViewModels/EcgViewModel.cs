using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EcgBLEApp.Filtering;
using EcgViewer.Filtering;
using InTheHand.Bluetooth;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Data;
using Windows.Devices.Input;

namespace EcgViewer.ViewModels
{
    public partial class EcgViewModel : ObservableObject
    {
        //private EcgFile _currentEcgFile;

        private BluetoothLEScan? _scan = null;
        private CancellationTokenSource _scanCancellation = new();

        private BLE BleManager => BLE.Instance;

        private readonly DataSimulator _simulator = new DataSimulator();

        private ISourceBlock<ushort[]>? _bleSource;
        private IPropagatorBlock<ushort, int>? _heartRateDetection;
        private IPropagatorBlock<ushort, ushort>? _broadcastSamples;

        private readonly QrsDetector _qrsDetector = new();

        private BandpassFilterButterworthImplementation? _bandpass;
        private LowpassFilterButterworthImplementation? _lowPassFilter;
        private HighpassFilterButterworthImplementation? _highPass;
        private DCBlockFilter _dcBlockFilter = new DCBlockFilter();
        private NotchFilter? _acNotchFilter;

        public EcgViewModel()
        {
            Title = "Ecg";
            StartScanningCommand = new RelayCommand(async () =>
            {
                RequestDeviceOptions scanOptions = new()
                {
                    Filters = {
                        new BluetoothLEScanFilter() {
                            Services = { BLE.ECG_SERVICE_UUID },
                        }
                    }
                };

                _scan = await Bluetooth.RequestLEScanAsync(new()
                {
                    AcceptAllAdvertisements = false,
                    KeepRepeatedDevices = true,
                    Filters = {
                        new BluetoothLEScanFilter() {
                            Services = { BLE.ECG_SERVICE_UUID }
                        }
                    }
                });

                //_ = Bluetooth.ScanForDevicesAsync(scanOptions, _scanCancellation.Token)
                //    .ContinueWith(t =>
                //    {
                //        IsScanning = false;

                //        if (t.IsCompletedSuccessfully)
                //        {
                //            Application.Current.Dispatcher.Invoke(() =>
                //            {
                //                foreach (var device in t.Result)
                //                {
                //                    DiscoveredDevices.Add(new() { Device = device, Name = device.Name });
                //                }
                //            });
                //        }
                //        else
                //        {
                //            t.Exception?.Handle(e => true);
                //        }
                //    });

                DiscoveredDevices.Clear();
                IsScanning = true;
            }, () => !IsScanning && (_scan is null || !_scan.Active));

            StopScanningCommand = new RelayCommand(() =>
            {
                _scanCancellation.Cancel();
                _scanCancellation = new();
                _scan?.Stop();
                IsScanning = false;

                OnPropertyChanged(nameof(IsScanning));
            }, () => IsScanning);

            Bluetooth.AdvertisementReceived += Bluetooth_AdvertisementReceived;

            ConnectToDeviceCommand = new RelayCommand<DiscoveredDeviceViewModel>(async (device) =>
            {
                try
                {
                    if (device is not null)
                    {
                        StopScanningCommand.Execute(null);
                        await BleManager.ConnectToDevice(device.Device);
                        await OnDeviceConnected(device.Device);
                    }
                }
                catch
                {
                }
            });

            DisconnectCommand = new RelayCommand(async () =>
            {
                ConnectedDevice?.Gatt.Disconnect();
                //await Adapter.DisconnectDeviceAsync(ConnectedDevice);
            });

            UpdatePollingRateCommand = new RelayCommand(async () =>
            {
                if (await BleManager.SetPollingRate((ushort)PollingRate))
                {
                    OnPollingRateChanged();
                }
            });

            BleManager.CurrentDeviceDisconnected += OnDeviceDisconnected;

            BleManager.PollingRateChanged += (pollingRate) =>
            {
                if (pollingRate == PollingRate) { return; }

                PollingRate = pollingRate;
                OnPollingRateChanged();
            };

            //_simulator.NewMessage += Simulator_NewMessage;
            //_simulator.Start();

            Filters = new([
                new("DC Block", (samplingRate) => new DCBlockFilter(), true),
                new("50Hz Low Pass", (samplingRate) => new LowpassFilterButterworthImplementation(50, 1, samplingRate), true),
                new("50/60Hz Notch", (samplingRate) => new NotchFilter(samplingRate), false)
            ]);

            var devicesView = CollectionViewSource.GetDefaultView(DiscoveredDevices);
            //devicesView.SortDescriptions.Add(new(nameof(DiscoveredDeviceViewModel.RSSI), System.ComponentModel.ListSortDirection.Descending));
        }

        private void Bluetooth_AdvertisementReceived(object? sender, BluetoothAdvertisingEvent e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (e.Device is null)
                {
                    return;
                }

                var newDevice = new DiscoveredDeviceViewModel()
                {
                    Device = e.Device,
                    Name = string.IsNullOrEmpty(e.Device.Name) ? "Unnamed" : e.Device.Name,
                    RSSI = e.Rssi
                };

                var device = DiscoveredDevices.FirstOrDefault(d => d.Device.Id == e.Device.Id);
                if (device is null)
                {
                    DiscoveredDevices.Add(newDevice);
                }
                else
                {
                    int index = DiscoveredDevices.IndexOf(device);
                    if (index >= 0)
                    {
                        DiscoveredDevices[index] = newDevice;
                    }
                }
            });
        }

        #region Handlers
        private async Task OnDeviceConnected(BluetoothDevice device)
        {
            ConnectedDevice = device;

            // Query current polling rate
            PollingRate = await BleManager.GetPollingRate();

            // Init signal filters
            InitFilters();

            // Complete the top source block. Completion should propagate all the way through.
            _bleSource?.Complete();

            // Setup pipeline
            await SetupDataflow();

            //// ECG file recording
            //await InitFileRecording();

            // Run QRS detection on new thread
            _ = Task.Run(() => _qrsDetector.PanTompkins());
        }

        private void OnDeviceDisconnected()
        {
            ConnectedDevice = null;

            // Clear values
            Values.Clear();
            FilteredValues.Clear();

            //// Stop and clean up file recording
            //if (_currentEcgFile != null)
            //{
            //    _currentEcgFile.Dispose();
            //    _currentEcgFile = null;
            //}

            // Complete the top source block. Completion should propagate all the way through.
            _bleSource?.Complete();
        }

        private void OnHeartRateUpdate(int heartRate)
        {
            HeartRate = heartRate.ToString();
        }

        private void OnPollingRateChanged()
        {
            // Reinit signal filters with new polling rate
            InitFilters();

            // Replace heart rate detection pipeline with updated polling rate
            SetupHeartRateDetection();

            // Run QRS detection on new thread
            _ = Task.Run(() => _qrsDetector.PanTompkins());
        }

        #endregion

        private void InitFilters()
        {
            foreach (var filter in Filters)
            {
                filter.InitializeFilter(PollingRate);
            }

            _lowPassFilter = new LowpassFilterButterworthImplementation(50, 1, PollingRate);
            _bandpass = new BandpassFilterButterworthImplementation(0.05, 40, 3, PollingRate);
            _highPass = new HighpassFilterButterworthImplementation(0.5, 2, PollingRate);
            _acNotchFilter = new(PollingRate);
        }
        //private async Task InitFileRecording()
        //{
        //    // Ensure storage permission is granted
        //    var status = await CrossPermissions.Current.CheckPermissionStatusAsync<StoragePermission>();
        //    if (status != PermissionStatus.Granted)
        //    {
        //        if (await CrossPermissions.Current.ShouldShowRequestPermissionRationaleAsync(Permission.Storage))
        //        {
        //            //await DisplayAlert("Need storage, "Request storage permission", "OK");
        //        }

        //        status = await CrossPermissions.Current.RequestPermissionAsync<StoragePermission>();
        //    }

        //    if (status != PermissionStatus.Granted)
        //    {
        //        return;
        //    }


        //    // Get the external storage path from the platform specific services.
        //    string externalStoragePath = DependencyService.Get<IExternalStorage>().GetPath();

        //    string folderPath = Path.Combine(externalStoragePath, "EcgBleApp");

        //    try
        //    {
        //        // Ensure the directory is created
        //        Directory.CreateDirectory(folderPath);

        //        // Create ecg file path
        //        string ecgFileName = Path.Combine(folderPath, EcgFile.CreateFileName());

        //        // Open (create) the new ecg file with the current polling rate
        //        _currentEcgFile = EcgFile.OpenWrite(ecgFileName, (ushort)PollingRate);
        //    }
        //    catch
        //    {
        //        // TODO: handle exception
        //    }
        //}

        #region Dataflow

        private IDisposable? _heartRateDetectionLink;

        private async Task SetupDataflow()
        {
            DataflowLinkOptions propagateLinkOptions = new DataflowLinkOptions() { PropagateCompletion = true };


            /* Message section */

            _bleSource = await CreateBLESource(BleManager.SignalCharacteristic, CancellationToken.None);

            var broadcastMessages = new BroadcastBlock<ushort[]>(null);

            //var fileRecording = new ActionBlock<ushort[]>(message =>
            //{
            //    if (_currentEcgFile != null)
            //    {
            //        _currentEcgFile.WriteSamples(message);
            //    }
            //});

            var unpackSamples = new TransformManyBlock<ushort[], ushort>(sampleBuffer => sampleBuffer);


            /* Sample section */

            _broadcastSamples = new BroadcastBlock<ushort>(null);

            _bleSource.LinkTo(broadcastMessages, propagateLinkOptions);
            //broadcastMessages.LinkTo(fileRecording, propagateLinkOptions);
            broadcastMessages.LinkTo(unpackSamples, propagateLinkOptions);

            unpackSamples.LinkTo(_broadcastSamples, propagateLinkOptions);
            _broadcastSamples.LinkTo(ProcessSamples(), propagateLinkOptions);

            SetupHeartRateDetection();
        }

        private void SetupHeartRateDetection()
        {
            if (_broadcastSamples is null)
            {
                return;
            }

            // Replace heart rate detection dataflow pipeline
            _heartRateDetectionLink?.Dispose();
            _heartRateDetection?.Complete();

            _heartRateDetection = FilteringUtils.HeartRateDetection(PollingRate, _qrsDetector);
            _heartRateDetection.LinkTo(new ActionBlock<int>(OnHeartRateUpdate));
            _heartRateDetectionLink = _broadcastSamples.LinkTo(_heartRateDetection, new DataflowLinkOptions() { PropagateCompletion = true });
        }

        private ITargetBlock<ushort> ProcessSamples()
        {
            DataflowLinkOptions propagateLinkOptions = new DataflowLinkOptions() { PropagateCompletion = true };

            var convertTo_mV = FilteringUtils.ConvertSampleToMillivolts();

            var applyFilter = new TransformBlock<double, double>(value =>
            {
                return Filters
                    .Where(f => f.IsEnabled && f.Filter is not null)
                    .ToList()
                    .Aggregate(value, (v, vm) => vm.Filter!.compute(v));

                //double dcBlocked = _dcBlockFilter.compute(value);

                //if (_lowPassFilter is null || !LowPassFilterEnabled)
                //{
                //    return dcBlocked;
                //}

                //return _lowPassFilter.compute(dcBlocked);
            });

            var handleOutput = new ActionBlock<double>(value =>
            {
                FilteredValues.Add((float)value);
            }, new() { TaskScheduler = TaskScheduler.FromCurrentSynchronizationContext()! });

            convertTo_mV.LinkTo(applyFilter, propagateLinkOptions);
            applyFilter.LinkTo(handleOutput, propagateLinkOptions);

            return convertTo_mV;
        }

        private static async Task<ISourceBlock<ushort[]>> CreateBLESource(
            GattCharacteristic ecgSignalChar, CancellationToken cancellationToken)
        {
            // Create the source buffer block for the incoming messages
            BufferBlock<ushort[]> bufferBlock = new BufferBlock<ushort[]>(
                new DataflowBlockOptions()
                {
                    CancellationToken = cancellationToken,
                    EnsureOrdered = true,
                });

            // BLE Notify handler of the ECG signal characteristic
            void onValueUpdated(object? sender, GattCharacteristicValueChangedEventArgs e)
            {
                if (e.Error is not null)
                {
                    Console.WriteLine(e.Error.ToString());
                    return;
                }

                if (e.Value is null)
                {
                    return;
                }

                byte[] message = e.Value;
                ushort[] buffer = new ushort[BLE.SamplesPerMessage];

                BLE.UnpackSamplesFromMessage(message, buffer, 0);

                // Post the sample buffer into the pipeline
                _ = bufferBlock.Post(buffer);
            }

            // Subscribe to BLE notify handler
            ecgSignalChar.CharacteristicValueChanged += onValueUpdated;

            // After the target block is completed, clean up
            _ = bufferBlock.Completion.ContinueWith(async (task) =>
            {
                // Remove the notify handler
                ecgSignalChar.CharacteristicValueChanged -= onValueUpdated;

                if (!ecgSignalChar.Service.Device.Gatt.IsConnected)
                {
                    // Device is disconnected, don't need to stop updates from characteristic
                    return;
                }

                await ecgSignalChar.StopNotificationsAsync();
            }, TaskScheduler.FromCurrentSynchronizationContext());

            // Start receiving updates
            await ecgSignalChar.StartNotificationsAsync();

            return bufferBlock;
        }

        #endregion


        #region Bindings

        public IRelayCommand StartScanningCommand { get; }

        public IRelayCommand StopScanningCommand { get; }

        public IRelayCommand ConnectToDeviceCommand { get; }

        public IRelayCommand DisconnectCommand { get; }

        public IRelayCommand UpdatePollingRateCommand { get; }

        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private bool _isScanning;
        public ObservableCollection<DiscoveredDeviceViewModel> DiscoveredDevices { get; } = new();

        [ObservableProperty]
        private string _heartRate = "";

        [ObservableProperty]
        private int _pollingRate;

        [ObservableProperty]
        private bool _lowPassFilterEnabled = false;

        public bool IsConnected => ConnectedDevice is not null;

        [NotifyPropertyChangedFor(nameof(IsConnected))]
        [ObservableProperty]
        private BluetoothDevice? _connectedDevice;

        public ObservableCollection<int> Values { get; } = new ObservableCollection<int>();

        public ObservableCollection<float> FilteredValues { get; } = new ObservableCollection<float>();

        public ObservableCollection<FilterViewModel> Filters { get; } = [];

        #endregion
    }

    public partial class FilterViewModel : ObservableObject
    {
        private readonly Func<int, IFilter> _filterFactory;

        [ObservableProperty]
        private bool _isEnabled;

        public string Name { get; }

        public IFilter? Filter { get; private set; }

        public FilterViewModel(string name, Func<int, IFilter> filterFactory, bool isEnabled)
        {
            Name = name;
            IsEnabled = isEnabled;
            _filterFactory = filterFactory;
        }

        [MemberNotNull(nameof(Filter))]
        public void InitializeFilter(int samplingRate)
        {
            Filter = _filterFactory(samplingRate);
        }
    }
}