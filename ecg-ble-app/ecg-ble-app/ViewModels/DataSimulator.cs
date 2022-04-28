using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ecg_ble_app.ViewModels
{
    internal class DataSimulator
    {
        private bool _isRunning;
        private SynchronizationContext _currentSyncContext = null;
        public int PollingRate { get; set; } = 500;
        public int PacketsPerMessage { get; set; } = 4;

        private double PollingTimeDelta => 1000.0 / PollingRate;
        private int SendBufferSize => PacketsPerMessage * 5;
        public bool IsRunning => _isRunning;


        public delegate void NewMessageEventHandler(byte[] value);

        public event NewMessageEventHandler NewMessage;

        public void Start()
        {
            if (_isRunning)
            {
                return;
            }

            _isRunning = true;
            _currentSyncContext = SynchronizationContext.Current;

            int x = 0;

            int sendBufferIndex = 0;
            int payloadIndex = 0;
            ushort[] fourValues = new ushort[4];
            byte[] sendBuffer = new byte[SendBufferSize];

            _ = Task.Run(async () =>
              {
                  while (_isRunning)
                  {
                      // Create sine value
                      ushort value = SineWave10Bit(2f, SineWave(0.2f, 350, 150, x), x++);

                      // Set four 10 bit value payload
                      fourValues[payloadIndex++] = value;

                      if (payloadIndex == 4)
                      {
                          // Pack 10 bit values into 8 bit

                          sendBuffer[sendBufferIndex++] = (byte)(fourValues[0] >> 2);
                          sendBuffer[sendBufferIndex++] = (byte)((fourValues[0] & 0b0000000011) << 6 | (fourValues[1] >> 4));
                          sendBuffer[sendBufferIndex++] = (byte)((fourValues[1] & 0b0000001111) << 4 | (fourValues[2] >> 6));
                          sendBuffer[sendBufferIndex++] = (byte)((fourValues[2] & 0b0000111111) << 2 | (fourValues[3] >> 8));
                          sendBuffer[sendBufferIndex++] = (byte)((fourValues[3] & 0b0011111111) << 0);

                          payloadIndex = 0;
                      }

                      if (sendBufferIndex == SendBufferSize)
                      {
                          // Dispatch send buffer

                          SendMessage(sendBuffer);
                          sendBufferIndex = 0;
                      }

                      if (x == int.MaxValue)
                      {
                          x = 0;
                      }

                      // Wait polling time delay
                      await Task.Delay(TimeSpan.FromMilliseconds(PollingTimeDelta));
                  }
              });
        }

        public void Stop()
        {
            _isRunning = false;
            _currentSyncContext = null;
        }

        private void SendMessage(byte[] buffer)
        {
            byte[] bufferCopy = new byte[buffer.Length];
            buffer.CopyTo(bufferCopy, 0);

            _currentSyncContext?.Post((value) =>
            {
                NewMessage?.Invoke(value as byte[]);
            }, bufferCopy);
        }
        private ushort SineWave10Bit(float frequency, float amplitude, int x)
        {
            double sin = Math.Sin((2 * Math.PI * x * frequency) / PollingRate);
            return (ushort)(amplitude * sin + 512);
        }

        private float SineWave(float frequency, float max, float min, int x)
        {
            float mean = (max - min) / 2;
            float sin = (float)Math.Sin((2 * Math.PI * x * frequency) / PollingRate);

            return min + mean + sin * mean;
        }
    }

}
