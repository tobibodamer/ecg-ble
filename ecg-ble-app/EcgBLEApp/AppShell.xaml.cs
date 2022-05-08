using EcgBLEApp.ViewModels;
using EcgBLEApp.Views;
using System;
using System.Collections.Generic;
using Microsoft.Maui; 
using Microsoft.Maui.Controls;

namespace EcgBLEApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(ItemDetailPage), typeof(ItemDetailPage));
            Routing.RegisterRoute(nameof(NewItemPage), typeof(NewItemPage));
            Routing.RegisterRoute(nameof(EcgFileView), typeof(EcgFileView));
        }

        private async void OnMenuItemClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//LoginPage");
        }
    }
}
