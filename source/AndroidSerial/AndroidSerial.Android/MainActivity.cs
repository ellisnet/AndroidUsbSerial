using System;
using Android.App;
using Android.Content.PM;
using Android.Hardware.Usb;
using Android.OS;
using AndroidSerial.Droid.UsbSerial;
using AndroidSerial.Messages;
using AndroidSerial.Services;
using AndroidUsbSerial;
using Prism;
using Prism.Ioc;
using Xamarin.Forms;
using SharedSerial.Services;
using SharedSerial.Models;

namespace AndroidSerial.Droid
{
    [Activity(
        Label = "AndroidSerial", 
        Icon = "@mipmap/icon", 
        Theme = "@style/MainTheme", 
        MainLauncher = true, 
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
    [IntentFilter(new[] { UsbManager.ActionUsbDeviceAttached })]
    [MetaData(UsbManager.ActionUsbDeviceAttached, Resource = "@xml/device_filter")]
    public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity
    {
        private bool _isReceiverRegistered;
        private readonly object _registrationLocker = new object();
        private UsbBroadcastReceiver _detachedReceiver;
        private UsbSerialService _serialService;

        protected override void OnCreate(Bundle bundle)
        {
            TabLayoutResource = Resource.Layout.Tabbar;
            ToolbarResource = Resource.Layout.Toolbar;

            base.OnCreate(bundle);

            Acr.UserDialogs.UserDialogs.Init(this);

            global::Xamarin.Forms.Forms.Init(this, bundle);

            _serialService = new UsbSerialService(this);

            LoadApplication(new AndroidSerial.App(new AndroidInitializer(_serialService)));
        }

        protected override void OnResume()
        {
            base.OnResume();

            lock (_registrationLocker)
            {
                if (!_isReceiverRegistered)
                {
                    _detachedReceiver = new UsbBroadcastReceiver((c, i) =>
                    {
                        if (i.GetParcelableExtra(UsbManager.ExtraDevice) is UsbDevice device)
                        {
                            _serialService.SetDeviceAsDisconnected();
                            MessagingCenter.Send(new DeviceMessage(device.DeviceName), DeviceMessage.DeviceDetached);
                        }
                    });
                    _detachedReceiver.Register(this, UsbManager.ActionUsbDeviceDetached);
                    _isReceiverRegistered = true;
                }
            }
        }

        protected override void OnPause()
        {
            base.OnPause();

            lock (_registrationLocker)
            {
                if (_detachedReceiver != null && _isReceiverRegistered)
                {
                    UnregisterReceiver(_detachedReceiver);
                    _isReceiverRegistered = false;
                }
            }
        }
    }

    public class AndroidInitializer : IPlatformInitializer
    {
        private readonly UsbSerialService _serialService;

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterInstance(typeof(IUsbSerialService), _serialService);
            containerRegistry.RegisterInstance(typeof(IDataStoreService<Item>), 
                new SqliteDataStoreService(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal)));
        }

        public AndroidInitializer( UsbSerialService serialService)
        {
            _serialService = serialService ?? throw new ArgumentNullException(nameof(serialService));
        }
    }
}

