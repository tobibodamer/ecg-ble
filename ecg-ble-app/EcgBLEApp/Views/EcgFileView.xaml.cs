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
    public partial class EcgFileView : ContentPage
    {
        public EcgFileView()
        {
            InitializeComponent();

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        public EcgFileViewModel ViewModel => BindingContext as EcgFileViewModel;

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (ViewModel != null && ViewModel.PollingRate > 0)
            {
                UpdateSizing();
            }
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals(nameof(ViewModel.PollingRate)))
            {
                UpdateSizing();
            }
        }

        private void UpdateSizing()
        {
            // 0.5mv x 0.2s
            ChartControl.GridSize = new Size(ViewModel.PollingRate * 0.2f, 0.5f);

            // 0.1mv x 40ms
            ChartControl.FineGridSize = new Size(ViewModel.PollingRate * 0.04f, 0.1f);

            ChartControl.XScale = 70 / (ViewModel.PollingRate * 0.2f); // 70px / 0.2s
            ChartControl.YScale = 70 / 0.5f; // 70px / 0.5mV
        }
    }
}