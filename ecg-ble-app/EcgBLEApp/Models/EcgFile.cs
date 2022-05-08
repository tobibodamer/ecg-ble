using EcgBLEApp.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace EcgBLEApp.Models
{
    public sealed class EcgFile : IDisposable
    {
        public const int HEADER_LENGTH = 32;
        public const ushort SAMPLE_RATE_IDENTIFIER = 0xFFFF;
        public const ushort VERSION = 2;
        public const ushort SAMPLE_SIZE = sizeof(ushort);

        private readonly BinaryWriter _writer = null;
        private readonly BinaryReader _reader = null;
        private bool _updateHeaderOnDispose;

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

            if (!file.ReadHeader())
            {
                file.Dispose();

                throw new InvalidEcgFileException("Could not open the ecg file: Invalid header or file.");
            }

            return file;
        }
        public static EcgFile OpenWrite(string fileName, ushort sampleRate, bool updateHeaderOnDispose = true)
        {
            var fileStream = File.Open(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            var writer = new BinaryWriter(fileStream);

            EcgFile file = new EcgFile(fileName, writer: writer)
            {
                _samplingRate = sampleRate,
                _version = VERSION,
                _updateHeaderOnDispose = updateHeaderOnDispose
            };

            file.WriteHeader();

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

        public void WriteHeader()
        {
            if (!CanWrite)
            {
                throw new InvalidOperationException("Not allowed to write file.");
            }

            if (_writer.BaseStream.Seek(0, SeekOrigin.Begin) != 0)
            {
                return;
            }

            int remainingZeros = HEADER_LENGTH;

            _writer.Write(Version);
            remainingZeros -= sizeof(ushort);

            _writer.Write(SamplingRate);
            remainingZeros -= sizeof(ushort);

            _writer.Write(SamplesCount);
            remainingZeros -= sizeof(long);

            _writer.Write(new byte[remainingZeros]); // Write zeros for rest of header
        }

        public bool ReadHeader()
        {
            if (!CanRead)
            {
                throw new InvalidOperationException("Not allowed to read file.");
            }

            if (_reader.BaseStream.Seek(0, SeekOrigin.Begin) != 0)
            {
                return false;
            }

            _version = _reader.ReadUInt16();

            if (_version == 1)
            {
                if (_reader.BaseStream.Length < 6)
                {
                    return false;
                }

                _ = _reader.ReadUInt16();
                _samplingRate = _reader.ReadUInt16();
                _samplesCount = (_reader.BaseStream.Length - 6) / SAMPLE_SIZE;
            }
            else
            {
                if (_reader.BaseStream.Length < HEADER_LENGTH)
                {
                    return false;
                }

                _samplingRate = _reader.ReadUInt16();
                _samplesCount = _reader.ReadInt64();
            }

            return true;
        }

        private (long start, long end) GetSamplePositions()
        {
            long startPosition, endPosition;

            if (Version == 1)
            {
                startPosition = 6;
            }
            else
            {
                startPosition = HEADER_LENGTH;
            }

            endPosition = startPosition + SamplesCount * SAMPLE_SIZE;

            return (startPosition, endPosition);
        }

        public IEnumerable<ushort> ReadSamples()
        {
            if (!CanRead)
            {
                throw new InvalidOperationException("Not allowed to read file.");
            }

            (long samplesPosition, long endPosition) = GetSamplePositions();

            if (_reader.BaseStream.Seek(samplesPosition, SeekOrigin.Begin) != samplesPosition)
            {
                yield break;
            }

            while (samplesPosition < endPosition)
            {
                yield return _reader.ReadUInt16();

                samplesPosition += SAMPLE_SIZE;
            }
        }
        public void WriteSamples(ushort[] samples)
        {
            if (!CanWrite)
            {
                throw new InvalidOperationException("Not allowed to write file.");
            }

            _writer.BaseStream.Seek(0, SeekOrigin.End);

            Span<byte> bytes = MemoryMarshal.AsBytes(samples.AsSpan());

            _writer.Write(bytes.ToArray(), 0, bytes.Length);
            _samplesCount += samples.Length;
        }
        public void Dispose()
        {
            try
            {
                if (_updateHeaderOnDispose && _writer != null && CanWrite)
                {
                    WriteHeader();
                }

                _writer?.Dispose();
                _reader?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, probably underlying stream
            }
        }
    }
}