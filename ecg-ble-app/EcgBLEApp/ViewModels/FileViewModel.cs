using System;

namespace EcgBLEApp.ViewModels
{
    public class FileViewModel
    {
        public FileViewModel(string path, string fileName, long samplesCount, int samplingRate, DateTime lastWriteTime)
        {
            Path = path;
            FileName = fileName;
            SamplesCount = samplesCount;
            SamplingRate = samplingRate;
            LastWriteTime = lastWriteTime;

            Length = SamplingRate > 0 ? TimeSpan.FromSeconds(SamplesCount / SamplingRate) : TimeSpan.Zero;
        }

        public string Path { get; }
        public string FileName { get; }
        public long SamplesCount { get; }
        public int SamplingRate { get; }
        public DateTime LastWriteTime { get; }
        public TimeSpan Length { get; }
    }
}