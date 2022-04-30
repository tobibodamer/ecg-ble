using EcgBLEApp.Filtering;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using Plugin.Permissions;
using Plugin.Permissions.Abstractions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows.Input;
using Xamarin.Forms;

namespace EcgBLEApp.ViewModels
{
    public class EcgViewModel : BaseViewModel
    {
        private static IAdapter Adapter => CrossBluetoothLE.Current.Adapter;
        private EcgFile _currentEcgFile;
        private BLE BleManager => BLE.Instance;

        private readonly DataSimulator _simulator = new DataSimulator();

        private ISourceBlock<ushort[]> _bleSource;
        private ITargetBlock<ushort> _heartRateDetection;
        private IPropagatorBlock<ushort, ushort> _broadcastSamples;

        private readonly QrsDetector _qrsDetector = new QrsDetector();

        private BandpassFilterButterworthImplementation _bandpass;
        private LowpassFilterButterworthImplementation _filter;
        private HighpassFilterButterworthImplementation _highPass;
        private DCBlockFilter _dcBlockFilter = new DCBlockFilter();
        public EcgViewModel()
        {
            Title = "Ecg";
            StartScanningCommand = new Command(async () =>
            {
                // Get location permission to scan bluetooth devices

                try
                {
                    var status = await CrossPermissions.Current.CheckPermissionStatusAsync<LocationPermission>();
                    if (status != PermissionStatus.Granted)
                    {
                        if (await CrossPermissions.Current.ShouldShowRequestPermissionRationaleAsync(Permission.Location))
                        {
                            // TODO: Display alert
                        }

                        status = await CrossPermissions.Current.RequestPermissionAsync<LocationPermission>();
                    }

                    if (status == PermissionStatus.Granted)
                    {
                        //Query permission
                    }
                    else if (status != PermissionStatus.Unknown)
                    {
                        //location denied
                    }
                }
                catch
                {
                    //Something went wrong
                }

                _ = Adapter.StartScanningForDevicesAsync(new Guid[] { BLE.ECG_SERVICE_UUID })
                    .ContinueWith(t => t.Exception?.Handle(e => true));

                OnPropertyChanged(nameof(IsScanning));
            });

            StopScanningCommand = new Command(async () =>
            {
                try
                {
                    await Adapter.StopScanningForDevicesAsync();
                }
                catch (OperationCanceledException)
                {

                }

                OnPropertyChanged(nameof(IsScanning));
            }, () => Adapter.IsScanning);

            Adapter.DeviceDiscovered += (s, e) =>
                OnPropertyChanged(nameof(DiscoveredDevices));

            Adapter.ScanTimeoutElapsed += (s, e) => OnPropertyChanged(nameof(IsScanning));

            ConnectToDeviceCommand = new Command<IDevice>(async (device) =>
            {
                try
                {
                    await BleManager.ConnectToDevice(device);

                    await OnDeviceConnected(device);
                }
                catch
                {
                }
            });

            DisconnectCommand = new Command(async () =>
            {
                await Adapter.DisconnectDeviceAsync(ConnectedDevice);
            });

            UpdatePollingRateCommand = new Command(async () =>
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
        }

        #region Handlers
        private async Task OnDeviceConnected(IDevice device)
        {
            ConnectedDevice = device;
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged("");

            // Query current polling rate
            PollingRate = await BleManager.GetPollingRate();

            // Init signal filters
            InitFilters();

            // Setup pipeline
            await SetupDataflow();

            // ECG file recording
            await InitFileRecording();

            // Run QRS detection on new thread
            _ = Task.Run(async () => await _qrsDetector.PanTompkins());

        }
        private void OnDeviceDisconnected()
        {
            ConnectedDevice = null;
            OnPropertyChanged(nameof(IsConnected));

            // Clear values
            Values.Clear();
            FilteredValues.Clear();

            // Stop and clean up file recording
            if (_currentEcgFile != null)
            {
                _currentEcgFile.Dispose();
                _currentEcgFile = null;
            }

            // Complete the top source block. Completion should propagate all the way through.
            _bleSource.Complete();
        }
        private void OnHeartRateUpdate(int heartRate)
        {
            HR = heartRate.ToString();
        }
        private void OnPollingRateChanged()
        {
            // Reinit signal filters with new polling rate
            InitFilters();

            // Replace heart reate detection pipeline with updated polling rate
            _heartRateDetection = HeartRateDetection(PollingRate, _qrsDetector, OnHeartRateUpdate);
            _broadcastSamples.LinkTo(_heartRateDetection, new DataflowLinkOptions() { PropagateCompletion = true });
        }

        #endregion

        private void InitFilters()
        {
            _filter = new LowpassFilterButterworthImplementation(50, 1, PollingRate);
            _bandpass = new BandpassFilterButterworthImplementation(0.05, 40, 3, PollingRate);
            _highPass = new HighpassFilterButterworthImplementation(0.5, 2, PollingRate);
        }
        private async Task InitFileRecording()
        {
            // Ensure storage permission is granted
            var status = await CrossPermissions.Current.CheckPermissionStatusAsync<StoragePermission>();
            if (status != PermissionStatus.Granted)
            {
                if (await CrossPermissions.Current.ShouldShowRequestPermissionRationaleAsync(Permission.Storage))
                {
                    //await DisplayAlert("Need storage, "Request storage permission", "OK");
                }

                status = await CrossPermissions.Current.RequestPermissionAsync<StoragePermission>();
            }

            if (status != PermissionStatus.Granted)
            {
                return;
            }


            // Get the external storage path from the platform specific services.
            string externalStoragePath = DependencyService.Get<IExternalStorage>().GetPath();

            string folderPath = Path.Combine(externalStoragePath, "EcgBleApp");

            try
            {
                // Ensure the directory is created
                Directory.CreateDirectory(folderPath);

                // Create ecg file path
                string ecgFileName = Path.Combine(folderPath, EcgFile.CreateFileName());

                // Open (create) the new ecg file with the current polling rate
                _currentEcgFile = EcgFile.OpenWrite(ecgFileName, (ushort)PollingRate);
            }
            catch
            {
                // TODO: handle exception
            }
        }

        #region Dataflow
        private async Task SetupDataflow()
        {
            DataflowLinkOptions propagateLinkOptions = new DataflowLinkOptions() { PropagateCompletion = true };


            /* Message section */

            _bleSource = await CreateBLESource(BleManager.SignalCharacteristic, CancellationToken.None);

            var broadcastMessages = new BroadcastBlock<ushort[]>(null);

            var fileRecording = new ActionBlock<ushort[]>(message =>
            {
                if (_currentEcgFile != null)
                {
                    _currentEcgFile.WriteMessage(message);
                }
            });

            var unpackSamples = new TransformManyBlock<ushort[], ushort>(sampleBuffer => sampleBuffer);


            /* Sample section */

            _broadcastSamples = new BroadcastBlock<ushort>(null);

            _heartRateDetection = HeartRateDetection(PollingRate, _qrsDetector, OnHeartRateUpdate);

            _bleSource.LinkTo(broadcastMessages, propagateLinkOptions);
            broadcastMessages.LinkTo(fileRecording, propagateLinkOptions);
            broadcastMessages.LinkTo(unpackSamples, propagateLinkOptions);

            unpackSamples.LinkTo(_broadcastSamples, propagateLinkOptions);
            _broadcastSamples.LinkTo(ProcessSamples(), propagateLinkOptions);
            _broadcastSamples.LinkTo(_heartRateDetection, propagateLinkOptions);
        }

        private ITargetBlock<ushort> ProcessSamples()
        {
            DataflowLinkOptions propagateLinkOptions = new DataflowLinkOptions() { PropagateCompletion = true };

            var convertTo_mV = new TransformBlock<ushort, double>(sample =>
            {
                const int ampGain = 1300;
                return ((sample - 512) * 3.3) / (1024 * ampGain) * 1000;
            });

            var applyFilter = new TransformBlock<double, double>(value =>
            {
                double dcBlocked = _dcBlockFilter.compute(value);

                return _filter.compute(dcBlocked);
            });

            var handleOutput = new ActionBlock<double>(value =>
            {
                FilteredValues.Add((float)value);
            });

            convertTo_mV.LinkTo(applyFilter, propagateLinkOptions);
            applyFilter.LinkTo(handleOutput, propagateLinkOptions);

            return convertTo_mV;
        }

        private static IPropagatorBlock<ushort, double> Downsample(int inputSamplingRate, int outputSamplingRate)
        {
            // Batch the samples to match output sampling rate (NOTE: only accurate to whole numbers)
            var batchSamples = new BatchBlock<ushort>(inputSamplingRate / outputSamplingRate);

            // Average the batched samples
            var downsample = new TransformBlock<ushort[], double>(samples =>
            {
                return samples.Aggregate(0.0, (sum, sample) => sum + sample) / samples.Length;
            });

            batchSamples.LinkTo(downsample);
            batchSamples.Completion.ContinueWith(completion => downsample.Complete());

            return DataflowBlock.Encapsulate(batchSamples, downsample);
        }
        private static ITargetBlock<ushort> HeartRateDetection(int samplingRate, QrsDetector qrsDetector, Action<int> heartRateCallback)
        {
            // Downsample to 200 Hz for QRS detection algorithm
            const int QRS_DETECTION_SAMPLING_RATE = 200;

            var downsampling = Downsample(samplingRate, QRS_DETECTION_SAMPLING_RATE);

            // Output of the qrs detector - calculates the heart rate from the rr samples interval
            var calculateHeartRate = new TransformBlock<int, int>(rrSamples =>
            {
                return ((int)(60 / ((double)rrSamples / QRS_DETECTION_SAMPLING_RATE)));
            });

            // Invoke the provided callback
            var processHeartRate = new ActionBlock<int>(heartRateCallback);

            calculateHeartRate.LinkTo(processHeartRate, new DataflowLinkOptions() { PropagateCompletion = true });

            // Init the QRS detector
            qrsDetector.Init(downsampling, calculateHeartRate, QRS_DETECTION_SAMPLING_RATE);

            return downsampling;
        }
        private static async Task<ISourceBlock<ushort[]>> CreateBLESource(
            ICharacteristic ecgSignalChar, CancellationToken cancellationToken)
        {
            // Create the source buffer block for the incoming messages
            BufferBlock<ushort[]> bufferBlock = new BufferBlock<ushort[]>(
                    new DataflowBlockOptions()
                    {
                        CancellationToken = cancellationToken,
                        EnsureOrdered = true,
                    });

            // BLE Notify handler of the ECG signal characteristic
            void onValueUpdated(object sender, CharacteristicUpdatedEventArgs e)
            {
                byte[] message = e.Characteristic.Value;                
                ushort[] buffer = new ushort[BLE.SamplesPerMessage];

                BLE.UnpackSamplesFromMessage(message, buffer, 0);

                // Post the sample buffer into the pipeline
                _ = bufferBlock.Post(buffer);
            }

            // Subscribe to BLE notify handler
            ecgSignalChar.ValueUpdated += onValueUpdated;

            // After the target block is completed, clean up
            _ = bufferBlock.Completion.ContinueWith(async (task) =>
            {
                // Remove the notify handler
                ecgSignalChar.ValueUpdated -= onValueUpdated;

                if (!Adapter.ConnectedDevices.Any(device => device.Id == ecgSignalChar.Service.Device.Id))
                {
                    // Device is disconnected, dont need to stop updates from characteristic
                    return;
                }

                await ecgSignalChar.StopUpdatesAsync();
            }, TaskScheduler.FromCurrentSynchronizationContext());

            // Start receiving updates
            await ecgSignalChar.StartUpdatesAsync();

            return bufferBlock;
        }

        #endregion


        #region Bindings

        public ICommand StartScanningCommand { get; }

        public ICommand StopScanningCommand { get; }

        public ICommand ConnectToDeviceCommand { get; }

        public ICommand DisconnectCommand { get; }

        public ICommand UpdatePollingRateCommand { get; }

        public bool IsScanning => Adapter.IsScanning;
        public IReadOnlyList<IDevice> DiscoveredDevices => Adapter.DiscoveredDevices;

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

        private string _hr;
        public string HR
        {
            get
            {
                return _hr;
            }
            set
            {
                _hr = value;
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
                if (_pollingRate == value)
                {
                    return;
                }

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

        public ObservableCollection<float> FilteredValues { get; } = new ObservableCollection<float>();

        #endregion
    }
}