using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EcgBLEApp.Filtering;
using EcgBLEApp.ViewModels;
using SkiaSharp;
using Microsoft.Maui; using Microsoft.Maui.Controls;
using Microsoft.Maui; using Microsoft.Maui.Controls.Xaml;

namespace EcgBLEApp.Views
{
    public partial class FileOverviewView : ContentPage
    {
        public FileOverviewView()
        {
            InitializeComponent();

            Loaded += FileOverviewView_Loaded;
        }

        private void FileOverviewView_Loaded(object sender, EventArgs e)
        {
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