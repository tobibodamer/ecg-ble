﻿using System;
using System.Windows.Input;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace ecg_ble_app.ViewModels
{
    public class AboutViewModel : BaseViewModel
    {
        public AboutViewModel()
        {
            Title = "About";
            OpenWebCommand = new Command(async () => await Browser.OpenAsync("https://aka.ms/xamarin-quickstart"));
        }

        public ICommand OpenWebCommand { get; }
    }
}