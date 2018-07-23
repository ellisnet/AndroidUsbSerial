using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.Content;
using Android.Hardware.Usb;
using AndroidSerial.Models;
using AndroidSerial.Services;
using AndroidUsbSerial;
using AndroidUsbSerial.Extensions;
using Debug = System.Diagnostics.Debug;

namespace AndroidSerial.Droid.UsbSerial
{
    public class UsbSerialService : IUsbSerialService
    {
        private static readonly byte[] NewLine = { 0x0a };

        private readonly Context _context;
        private PortManager _connectedPortManager;
        private Action<byte[]> _receivedDataAction;
        private UsbManager _usbManager;

        private byte[] JoinByteArrays(byte[] array1, byte[] array2) => array1.Concat(array2).ToArray();

        public bool IsDeviceConnected => _connectedPortManager != null;

        public async Task<IList<ISerialDevice>> FindSerialDevices()
        {
            var result = new SerialDevice[] { };

            _usbManager = PortManager.GetUsbManager(_context);

            if (_usbManager != null)
            {
                result = (await _usbManager.FindAllDriversAsync())?
                    .Select(s => new SerialDevice(s))?
                    .ToArray() ?? new SerialDevice[] { };
            }

            // ReSharper disable once CoVariantArrayConversion
            return result;
        }

        public async Task<bool> ConnectDevice(ISerialDevice device, Action<byte[]> receivedDataAction)
        {
            _receivedDataAction = receivedDataAction ?? throw new ArgumentNullException(nameof(receivedDataAction));
            var connectDevice = device as SerialDevice;
            if (connectDevice == null) { throw new ArgumentNullException(nameof(device));}

            bool result = false;

            IUsbSerialDriver driver = connectDevice.Driver;

            if (await _usbManager.RequestPermissionAsync(driver.Device, _context))
            {
                _connectedPortManager = new PortManager(driver.Ports[0])
                {
                    BaudRate = 115200,
                    DataBits = DataBits.Eight,
                    StopBits = StopBits.One,
                    Parity = Parity.None,
                };

                _connectedPortManager.DataReceived += (sender, args) =>
                {
                    if (args?.Data?.Any() ?? false)
                    {
                        _receivedDataAction?.Invoke(args.Data);
                    }
                };

                _connectedPortManager.ErrorReceived += (sender, args) =>
                {
                    Debugger.Break(); //To look at args
                    Debug.WriteLine($"An error occurred on the serial device: {(args?.ExceptionObject as Exception)?.ToString() ?? "(unknown)"}");
                };

                _connectedPortManager.Run(_usbManager);
                result = true;
            }

            return result;
        }

        public async Task DisconnectDevice()
        {
            _connectedPortManager?.Stop();
            await Task.Delay(500); //To let things wrap up
            _receivedDataAction = null;
            _connectedPortManager?.Dispose();
            _connectedPortManager = null;
        }

        public async Task<bool> SendData(byte[] data)
        {
            bool result = false;

            if (IsDeviceConnected && data != null && data.Length > 0)
            {
                result = (await _connectedPortManager.SendAsync(data)) > 0;
            }

            return result;
        }

        public async Task<bool> SendTextLine(string text)
        {
            bool result = false;

            if (IsDeviceConnected && (!String.IsNullOrWhiteSpace(text)))
            {
                byte[] data = JoinByteArrays(Encoding.ASCII.GetBytes(text), NewLine);
                result = (await _connectedPortManager.SendAsync(data)) > 0;
            }

            return result;
        }

        public async void SetDeviceAsDisconnected()
        {
            _receivedDataAction = null;
            if (_connectedPortManager != null)
            {
                if (_connectedPortManager.IsRunning)
                {
                    _connectedPortManager.Stop();
                    await Task.Delay(500); //To let things wrap up                   
                }

                if (!_connectedPortManager.IsDisposed)
                {
                    _connectedPortManager.Dispose();
                }
                _connectedPortManager = null;
            }
        }

        public void SetReceivedDataAction(Action<byte[]> receivedDataAction) =>
            _receivedDataAction = receivedDataAction;

        public UsbSerialService(Context context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }
    }
}
