using System;

using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Android.OS;
using System.Collections.Generic;
using Android;
using AndroidX.Core.App;

namespace EcgBLEApp.Droid;

[Activity(Label = "EcgBLEApp", Icon = "@mipmap/icon", Theme = "@style/MainTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        Platform.Init(this, savedInstanceState);
        ActivityCompat.RequestPermissions(this, new[] { Manifest.Permission.Bluetooth, Manifest.Permission.BluetoothConnect }, 0);
        //await Permissions.RequestAsync<BLEPermission>();
    }

    //public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
    //{
    //    Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

    //    base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
    //}
}

//public class BLEPermission : Xamarin.Essentials.Permissions.BasePlatformPermission
//{
//    public override (string androidPermission, bool isRuntime)[] RequiredPermissions => new List<(string androidPermission, bool isRuntime)>
//{
//(Android.Manifest.Permission.BluetoothScan, true),
//(Android.Manifest.Permission.BluetoothConnect, true)
//}.ToArray();
//}