using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using ecg_ble_app.Droid;
using EcgBLEApp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xamarin.Forms;

[assembly: Dependency(typeof(AndroidExternalStorage))]
namespace ecg_ble_app.Droid
{
    public class AndroidExternalStorage : IExternalStorage
    {
        public string GetPath()
        {
            return Android.OS.Environment.ExternalStorageDirectory.Path;
        }
    }
}