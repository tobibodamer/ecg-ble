using EcgBLEApp.ViewModels;
using System.ComponentModel;
using Xamarin.Forms;

namespace EcgBLEApp.Views
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