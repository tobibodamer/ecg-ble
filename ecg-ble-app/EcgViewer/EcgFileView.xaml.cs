using EcgViewer.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using static EcgViewer.Controls.ChartControl;

namespace EcgViewer.Views
{
    public partial class EcgFileView : UserControl
    {
        public EcgFileView()
        {
            InitializeComponent();

            Loaded += EcgFileView_Loaded;
        }

        private void EcgFileView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            ChartControl.YScale = 70 / 0.5f; // 70px / 0.5mV
        }

        public EcgFileViewModel ViewModel => DataContext as EcgFileViewModel;        

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.PollingRate))
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

        private void SkipToEndButton_Clicked(object sender, EventArgs e)
        {
            ChartControl.TimeMode = TimeModes.Scrolling;
            ChartControl.SkipToEnd();
        }

        private void ScrollBar_Scroll(object sender, System.Windows.Controls.Primitives.ScrollEventArgs e)
        {
            ChartControl.SkipTo((float)(sender as ScrollBar).Value);
            (sender as ScrollBar).SmallChange = ChartControl.XRange;
        }
    }
}