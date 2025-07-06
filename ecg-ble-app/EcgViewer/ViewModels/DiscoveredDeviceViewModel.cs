using CommunityToolkit.Mvvm.ComponentModel;
using InTheHand.Bluetooth;

namespace EcgViewer.ViewModels
{
    public partial class DiscoveredDeviceViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name = "";

        public required BluetoothDevice Device { get; init; }

        public int RSSI { get; init; }
    }
}