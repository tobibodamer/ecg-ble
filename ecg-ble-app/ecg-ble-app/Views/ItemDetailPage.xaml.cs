using ecg_ble_app.ViewModels;
using System.ComponentModel;
using Xamarin.Forms;

namespace ecg_ble_app.Views
{
    public partial class ItemDetailPage : ContentPage
    {
        public ItemDetailPage()
        {
            InitializeComponent();
            BindingContext = new ItemDetailViewModel();
        }
    }
}