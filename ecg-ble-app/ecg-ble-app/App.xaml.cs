using ecg_ble_app.Services;
using ecg_ble_app.Views;
using System;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace ecg_ble_app
{
    public partial class App : Application
    {

        public App()
        {
            InitializeComponent();

            DependencyService.Register<MockDataStore>();
            MainPage = new AppShell();
        }

        protected override void OnStart()
        {
        }

        protected override void OnSleep()
        {
        }

        protected override void OnResume()
        {
        }
    }
}
