﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;

namespace EcgBLEApp.ViewModels
{
    internal class ble
    {
        private static IAdapter Adapter => CrossBluetoothLE.Current.Adapter;

        public static readonly Guid ECG_SERVICE_UUID = Guid.Parse("2a264ccf-a9ca-4097-8efd-c5b6fba390a6");
        public static readonly Guid ECG_SIGNAL_CHAR_UUID = Guid.Parse("2a264cdf-a9ca-4097-8efd-c5b6fba390a6");
        public static readonly Guid POLLING_RATE_CHAR_UUID = Guid.Parse("2a264cef-a9ca-4097-8efd-c5b6fba390a6");

        public const int SEND_BUFFER_SIZE = 20;

        /// <summary>
        /// Number of bytes per packet.
        /// </summary>
        public const int PACKET_SIZE = 5;

        /// <summary>
        /// Number of packets per message (= full send buffer).
        /// </summary>
        public static readonly int PacketsPerMessage = SEND_BUFFER_SIZE / PACKET_SIZE;

        /// <summary>
        /// Number of values per message.
        /// </summary>
        public static readonly int ValuesPerMessage = PacketsPerMessage * 4;

        public static IDevice CurrentDevice { get; private set; }
        private static IService EcgService { get; set; }

        static ble()
        {
            Adapter.DeviceDisconnected += Adapter_DeviceDisconnected;
            Adapter.DeviceConnectionLost += Adapter_DeviceConnectionLost;
        }

        private static void Adapter_DeviceConnectionLost(object sender, Plugin.BLE.Abstractions.EventArgs.DeviceErrorEventArgs e)
        {
            if (CurrentDevice?.Id == e.Device.Id)
            {
                CurrentDevice = null;
                CurrentDeviceDisconnected?.Invoke(null, EventArgs.Empty);
                SignalStreams.Clear();
            }
        }

        private static void Adapter_DeviceDisconnected(object sender, Plugin.BLE.Abstractions.EventArgs.DeviceEventArgs e)
        {
            if (CurrentDevice?.Id == e.Device.Id)
            {
                CurrentDevice = null;
                CurrentDeviceDisconnected?.Invoke(null, EventArgs.Empty);
                foreach (var stream in SignalStreams.Values)
                {
                    stream.Dispose();
                }
                SignalStreams.Clear();
            }
        }

        public async static Task ConnectToDevice(IDevice device)
        {
            await Adapter.ConnectToDeviceAsync(device);

            CurrentDevice = device;
            EcgService = await CurrentDevice.GetServiceAsync(ECG_SERVICE_UUID);
        }

        public static event EventHandler CurrentDeviceDisconnected;

        private static readonly Dictionary<Guid, MemoryStream> SignalStreams = new Dictionary<Guid, MemoryStream>();
        public async static Task<Stream> GetData()
        {
            if (CurrentDevice == null)
            {
                throw new InvalidOperationException("No device connected.");
            }

            var stream = new MemoryStream();
            Guid id = Guid.NewGuid();

            SignalStreams.Add(id, stream);

            if (SignalStreams.Count == 1)
            {
                
                var signalChar = await EcgService.GetCharacteristicAsync(ECG_SIGNAL_CHAR_UUID);
                
                signalChar.ValueUpdated += SignalChar_ValueUpdated;
                await signalChar.StartUpdatesAsync();
            }

            return stream;
        }

        private static async void SignalChar_ValueUpdated(object sender, CharacteristicUpdatedEventArgs e)
        {
            var value = e.Characteristic.Value;

            List<Guid> toRemove = new List<Guid>();

            foreach (var (id, stream) in SignalStreams)
            {
                try
                {
                    await stream.WriteAsync(value, 0, value.Length);
                }
                catch (ObjectDisposedException)
                {
                    toRemove.Add(id);
                }
            }

            toRemove.ForEach(id => SignalStreams.Remove(id));
        }

        public static async Task<ushort> GetPollingRate()
        {
            if (CurrentDevice == null)
            {
                throw new InvalidOperationException("No device connected.");
            }

            var pollingRateChar = await EcgService.GetCharacteristicAsync(POLLING_RATE_CHAR_UUID);

            var pollingRateBytes = await pollingRateChar.ReadAsync();

            return BitConverter.ToUInt16(pollingRateBytes);
        }



        /// <summary>
        /// Unpacks 10 bit values packed to 8 bit array from the received message buffer.
        /// </summary>
        /// <param name="arr">The received message buffer. (Must have a length of <see cref="SEND_BUFFER_SIZE"/>)</param>
        /// <param name="destArray">The destination array. (Must have a length of <see cref="ValuesPerMessage"/> + <paramref name="index"/>)</param>
        /// <param name="index">The starting index in the destination array.</param>
        public static void ParseValues(byte[] arr, ushort[] destArray, int index)
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
    }
}