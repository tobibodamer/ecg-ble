using EcgBLEApp.ViewModels;
using System.ComponentModel;
using Microsoft.Maui; using Microsoft.Maui.Controls;

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