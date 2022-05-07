using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace EcgBLEApp.ViewModels
{
    public sealed class EcgFile : IDisposable
    {
        public const ushort SAMPLE_RATE_IDENTIFIER = 0xFFFF;
        public const ushort VERSION = 1;

        private readonly BinaryWriter _writer = null;
        private readonly BinaryReader _reader = null;

        private EcgFile(string fileName, BinaryReader reader = null, BinaryWriter writer = null)
        {
            _writer = writer;
            _reader = reader;
            FileName = fileName;
        }

        public static string CreateFileName() => $"ECG_Recording_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.ecg";
        public static EcgFile OpenRead(string fileName, Stream fileStream = null)
        {
            fileStream = fileStream ?? File.OpenRead(fileName);
            var reader = new BinaryReader(fileStream);

            EcgFile file = new EcgFile(fileName, reader: reader);

            file.ReadVersion();
            file.ReadSamplingRate();
            file.GetSamplesCount();

            return file;
        }
        public static EcgFile OpenWrite(string fileName, ushort sampleRate)
        {
            var fileStream = File.Open(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            var writer = new BinaryWriter(fileStream);

            EcgFile file = new EcgFile(fileName, writer: writer);

            file.WriteVersion();
            file.WriteSamplingRate(sampleRate);

            return file;
        }

        public bool CanRead => _reader?.BaseStream.CanRead == true;
        public bool CanWrite => _writer?.BaseStream.CanWrite == true;

        private ushort _version;
        public ushort Version => _version;

        private ushort _samplingRate;
        public ushort SamplingRate => _samplingRate;

        private long _samplesCount;
        public long SamplesCount => _samplesCount;

        public string FileName { get; }

        private bool ReadSamplingRate()
        {
            if (!CanRead)
            {
                throw new InvalidOperationException("Not allowed to read file.");
            }

            if (Version == 1)
            {
                try
                {
                    if (_reader.BaseStream.Seek(2, SeekOrigin.Begin) != 2 || _reader.BaseStream.Length < 4)
                    {
                        return false;
                    }

                    ushort samplingRateIdentifier = _reader.ReadUInt16();

                    if (samplingRateIdentifier != EcgFile.SAMPLE_RATE_IDENTIFIER)
                    {
                        return false;
                    }

                    _samplingRate = _reader.ReadUInt16();

                    return true;
                }
                catch
                {

                }
            }

            return false;
        }
        public bool WriteSamplingRate(ushort sampleRate)
        {
            if (!CanWrite)
            {
                throw new InvalidOperationException("Not allowed to write file.");
            }

            if (Version == 1)
            {
                try
                {
                    if (_writer.BaseStream.Seek(2, SeekOrigin.Begin) != 2)
                    {
                        return false;
                    }

                    _writer.Write(SAMPLE_RATE_IDENTIFIER);
                    _writer.Write(sampleRate);

                    _samplingRate = sampleRate;

                    return true;
                }
                catch
                {

                }
            }

            return false;
        }

        private bool ReadVersion()
        {
            if (!CanRead)
            {
                throw new InvalidOperationException("Not allowed to read file.");
            }

            try
            {
                if (_reader.BaseStream.Seek(0, SeekOrigin.Begin) == 0)
                {
                    _version = _reader.ReadUInt16();
                    return true;
                }
            }
            catch
            {

            }

            return false;
        }
        private bool WriteVersion(ushort version = VERSION)
        {
            if (!CanWrite)
            {
                throw new InvalidOperationException("Not allowed to write file.");
            }

            try
            {
                if (_writer.BaseStream.Seek(0, SeekOrigin.Begin) == 0)
                {
                    _writer.Write(version);
                    _version = version;
                    return true;
                }
            }
            catch
            {

            }

            return false;
        }


        public IEnumerable<ushort> ReadSamples(int startPos = 0)
        {
            if (!CanRead)
            {
                throw new InvalidOperationException("Not allowed to read file.");
            }

            int samplesPosition = -1;

            if (Version == 1)
            {
                samplesPosition = 6 + startPos * 2;
            }

            if (_reader.BaseStream.Seek(samplesPosition, SeekOrigin.Begin) != samplesPosition)
            {
                yield break;
            }

            while (samplesPosition < _reader.BaseStream.Length)
            {
                _reader.BaseStream.Seek(samplesPosition, SeekOrigin.Begin);

                yield return _reader.ReadUInt16();

                samplesPosition += 2;
            }
        }
        public void WriteMessage(ushort[] message)
        {
            if (!CanWrite)
            {
                throw new InvalidOperationException("Not allowed to write file.");
            }

            _writer.BaseStream.Seek(0, SeekOrigin.End);

            Span<byte> bytes = MemoryMarshal.AsBytes(message.AsSpan());
            _writer.Write(bytes.ToArray(), 0, bytes.Length);
            _samplesCount += 2;
        }

        private void GetSamplesCount()
        {
            if (!CanRead)
            {
                return;
            }

            _samplesCount = (_reader.BaseStream.Length - 6) / 2;
        }

        public void Dispose()
        {
            try
            {
                _writer?.Dispose();
                _reader?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, probably underlying stream }
            }
        }
    }
}