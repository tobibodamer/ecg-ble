using EcgBLEApp.Views;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui; using Microsoft.Maui.Controls;

namespace EcgBLEApp.ViewModels
{
    public class FileOverviewViewModel : BaseViewModel
    {
        public ObservableCollection<FileViewModel> Files { get; } = new ObservableCollection<FileViewModel>();

        public ICommand RefreshFilesCommand { get; }

        public Command<FileViewModel> FileTapped { get; }

        private bool _isRefreshing;
        public bool IsRefreshing
        {
            get => _isRefreshing; private set
            {
                SetProperty(ref _isRefreshing, value);
            }
        }

        public FileOverviewViewModel()
        {
            Title = "Browse files";

            RefreshFilesCommand = new Command(async () =>
            {
                IsRefreshing = true;

                try
                {
                    var orderedFiles = await Task.Run(() => GetEcgFiles().ToList().OrderByDescending(x => x.LastWriteTime));

                    Files.Clear();

                    foreach (var file in orderedFiles)
                    {
                        Files.Add(file);
                    }
                }
                finally
                {
                    IsRefreshing = false;
                }
            });

            FileTapped = new Command<FileViewModel>(async (file) =>
            {
                if (file is null)
                    return;

                // This will push the ItemDetailPage onto the navigation stack
                await Shell.Current.GoToAsync($"{nameof(EcgFileView)}?{nameof(EcgFileViewModel.Path)}={file.Path}");
            });
        }

        private IEnumerable<FileViewModel> GetEcgFiles()
        {
            // Get the external storage path from the platform specific services.
            string externalStoragePath = DependencyService.Get<IExternalStorage>().GetPath();

            string folderPath = Path.Combine(externalStoragePath, "EcgBleApp");

            //List<FileViewModel> fileViewModels = new List<FileViewModel>();

            foreach (var file in Directory.EnumerateFiles(folderPath, "*.ecg", SearchOption.AllDirectories))
            {
                var lastWriteTime = File.GetLastWriteTime(file);
                using (var ecgFile = EcgFile.OpenRead(file))
                {
                    if (ecgFile.SamplingRate == 0 || ecgFile.Version == 0 || ecgFile.SamplesCount == 0)
                    {
                        continue;
                    }

                    yield return new FileViewModel(
                        path: file,
                        fileName: Path.GetFileName(file),
                        samplesCount: ecgFile.SamplesCount,
                        samplingRate: ecgFile.SamplingRate,
                        lastWriteTime);
                }
            }

            //return fileViewModels;
        }
    }
}