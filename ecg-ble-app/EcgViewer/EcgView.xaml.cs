using EcgViewer.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using static EcgViewer.Controls.ChartControl;

namespace EcgViewer.Views
{
    public partial class EcgView : UserControl
    {
        public EcgView()
        {
            InitializeComponent();
            Loaded += View_Loaded;
        }

        const int GridSize = 370;

        private void View_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            ViewModel.FilteredValues.CollectionChanged += FilteredValues_CollectionChanged;

            ChartControl.YScale = GridSize / 0.5f; // 70px / 0.5mV
        }

        private void FilteredValues_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            //ScrollBar.Maximum = Math.Max(0, ViewModel.FilteredValues.Count - ChartControl.XRange);
            ScrollBar.Maximum = ViewModel.FilteredValues.Count;

            if (ChartControl.TimeMode is TimeModes.Scrolling)
            {
                ScrollBar.Value = ViewModel.FilteredValues.Count - ChartControl.XRange;
            }
        }

        public EcgViewModel ViewModel => (DataContext as EcgViewModel)!;

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

            ChartControl.XScale = GridSize / (ViewModel.PollingRate * 0.2f); // 70px / 0.2s
            ChartControl.YScale = GridSize / 0.5f; // 70px / 0.5mV
        }

        private void SkipToEndButton_Clicked(object sender, RoutedEventArgs e)
        {
            ChartControl.TimeMode = TimeModes.Scrolling;
            ChartControl.SkipToEnd();
        }

        private void ScrollBar_Scroll(object sender, System.Windows.Controls.Primitives.ScrollEventArgs e)
        {
            SkipToEndButton.IsChecked = false;
            ChartControl.TimeMode = TimeModes.Pinned;
            ChartControl.SkipTo((float)(sender as ScrollBar).Value);
            (sender as ScrollBar).SmallChange = ChartControl.XRange;
        }

        private void FiltersUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Filters.Count < 2)
            {
                return;
            }


            if (FiltersListBox.SelectedItem is FilterViewModel filter)
            {
                int index = ViewModel.Filters.IndexOf(filter);
                if (index > 0)
                {
                    ViewModel.Filters.Move(index, index - 1);
                }
            }
        }

        private void FiltersDownButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Filters.Count < 2)
            {
                return;
            }

            if (FiltersListBox.SelectedItem is FilterViewModel filter)
            {
                int index = ViewModel.Filters.IndexOf(filter);
                if (index < ViewModel.Filters.Count)
                {
                    ViewModel.Filters.Move(index, index + 1);
                }
            }
        }

        private void ToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            ChartControl.TimeMode = TimeModes.Pinned;
        }
    }
}