using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EcgViewer.Filtering;
using EcgViewer.Models;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows.Input;

namespace EcgViewer.ViewModels
{
    public partial class EcgFileViewModel : ObservableObject
    {
        private EcgFile? _currentFile = null;
        private readonly BufferBlock<ushort> _sampleBlock = new BufferBlock<ushort>();        

        [ObservableProperty]
        private string _title = "File Viewer";

        [ObservableProperty]
        private int _pollingRate;

        [ObservableProperty]
        private bool _isFileOpen;

        [ObservableProperty]
        private string? _fileName;

        [ObservableProperty]
        private string? _path;

        public ObservableCollection<float> Values { get; } = new ObservableCollection<float>();

        public ICommand OpenFileCommand { get; }

        partial void OnPathChanged(string? oldValue, string? newValue)
        {
            if (newValue == null) { return; }

            StartReadFile(newValue);
        }

        public EcgFileViewModel()
        {
            //var filter = new LowpassFilterButterworthImplementation(40, 5, 1000);
            //var filter = new DCBlockFilter();

            var convertTo_mV = FilteringUtils.ConvertSampleToMillivolts();

            var applyFilter = new TransformBlock<double, double>(sample =>
            {
                // TODO: pipe through filters
                //return filter.compute(sample);
                return sample;
            });

            var addToValues = new ActionBlock<double>(sample =>
            {
                Values.Add((float)sample);
                //await Task.Delay(TimeSpan.FromSeconds(1.0 / PollingRate));
            });

            _sampleBlock.LinkTo(convertTo_mV);
            convertTo_mV.LinkTo(applyFilter);
            applyFilter.LinkTo(addToValues);

            OpenFileCommand = new RelayCommand(() =>
            {
                var openFileDialog = new OpenFileDialog()
                {
                    Title = "Choose ecg file",
                    Filter = "Ecg Files (*.ecg)|*.ecg"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    StartReadFile(openFileDialog.FileName);
                }
            });
        }

        private void StartReadFile(string fileName)
        {
            if (_currentFile is not null)
            {
                _currentFile.Dispose();
                _sampleBlock.TryReceiveAll(out _);
            }

            _currentFile = EcgFile.OpenRead(fileName);

            IsFileOpen = true;
            FileName = fileName;
            PollingRate = _currentFile.SamplingRate;

            Task.Run(() =>
            {
                try
                {
                    foreach (var sample in _currentFile.ReadSamples())
                    {
                        _sampleBlock.Post(sample);
                    }
                }
                catch
                {
                    Console.WriteLine("Error while reading samples from file");
                }
                finally
                {
                    Console.WriteLine("Reading file finished.");
                }
            });
        }
    }
}