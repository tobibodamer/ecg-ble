using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows.Input;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace EcgBLEApp.ViewModels
{
    public class EcgFileViewModel : BaseViewModel
    {
        private EcgFile _currentFile = null;
        private readonly BufferBlock<ushort> _sampleBlock = new BufferBlock<ushort>();

        public ObservableCollection<float> Values { get; } = new ObservableCollection<float>();

        private int _pollingRate;
        public int PollingRate
        {
            get => _pollingRate;
            set => SetProperty(ref _pollingRate, value);
        }

        private bool _isFileOpen;
        public bool IsFileOpen
        {
            get => _isFileOpen;
            set => SetProperty(ref _isFileOpen, value);
        }

        private string _fileName;
        public string FileName
        {
            get => _fileName;
            set => SetProperty(ref _fileName, value);
        }

        public ICommand OpenFileCommand { get; }

        public EcgFileViewModel()
        {
            var applyFilter = new TransformBlock<ushort, ushort>(sample =>
            {
                // TODO: pipe through filters
                return sample;
            });

            var convertTo_mV = new TransformBlock<ushort, double>(sample =>
            {
                const int ampGain = 1300;
                return ((sample - 512) * 3.3) / (1024 * ampGain) * 1000;
            });

            var addToValues = new ActionBlock<double>(async sample =>
            {
                Values.Add((float)sample);
                await Task.Delay(TimeSpan.FromSeconds(1.0 / PollingRate));
            });

            _sampleBlock.LinkTo(applyFilter);
            applyFilter.LinkTo(convertTo_mV);
            convertTo_mV.LinkTo(addToValues);

            OpenFileCommand = new Command(async () =>
            {
                var result = await FilePicker.PickAsync(new PickOptions()
                {
                    PickerTitle = "Choose ecg file",
                });

                await ReadFile(result);
            });
        }

        private async Task ReadFile(FileResult fileResult)
        {
            string fileName = fileResult.FileName;
            Stream stream = await fileResult.OpenReadAsync();

            if (_currentFile != null)
            {
                _currentFile.Dispose();
                _sampleBlock.TryReceiveAll(out _);
            }

            _currentFile = EcgFile.OpenRead(fileName, stream);

            IsFileOpen = true;
            FileName = fileName;
            PollingRate = _currentFile.SamplingRate;

            foreach (var sample in _currentFile.ReadSamples())
            {
                _sampleBlock.Post(sample);
            }
        }
    }
}