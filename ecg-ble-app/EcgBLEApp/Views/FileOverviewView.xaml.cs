using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EcgBLEApp.Filtering;
using EcgBLEApp.ViewModels;
using Plugin.Permissions;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace EcgBLEApp.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class FileOverviewView : ContentPage
    {
        public FileOverviewView()
        {
            InitializeComponent();

            ViewModel.RefreshFilesCommand.Execute(null);
        }

        public FileOverviewViewModel ViewModel => BindingContext as FileOverviewViewModel;

        protected override void OnAppearing()
        {
            base.OnAppearing();
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            
        }

        private void ViewCell_Tapped(object sender, EventArgs e)
        {
            var file = (sender as ViewCell).BindingContext;

            ViewModel.FileTapped.Execute(file);
        }
    }
}