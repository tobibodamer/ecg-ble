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
    public partial class EcgFileView : ContentPage
    {
        public EcgFileView()
        {
            InitializeComponent();
        }

        public EcgFileViewModel ViewModel => BindingContext as EcgFileViewModel;

        protected override void OnAppearing()
        {
            base.OnAppearing();

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            ChartControl.YScale = 70 / 0.5f; // 70px / 0.5mV
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