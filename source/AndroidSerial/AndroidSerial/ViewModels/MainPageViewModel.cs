using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Acr.UserDialogs;
using AndroidSerial.Messages;
using AndroidSerial.Models;
using AndroidSerial.Services;
using AndroidSerial.Views;
using Prism.Commands;
using Prism.Navigation;
using SharedSerial.Models;
using SharedSerial.Services;
using Xamarin.Forms;

namespace AndroidSerial.ViewModels
{
    public class MainPageViewModel : ViewModelBase
    {
        private readonly int _deviceScanIntervalMilliseconds = 1000;
        private readonly IUsbSerialService _serialService;
        private readonly IDataStoreService<Item> _dataStoreService;
        private CancellationTokenSource _scanTokenSource;
        private TaskCompletionSource<byte[]> _waitForReceivedData;
        private bool _isTryingToConnect;
        private bool _isScanningForDevices;
        private bool _isFirstLoad = true;
        private readonly List<byte[]> _incomingDatabaseBytes = new List<byte[]>();

        private static readonly byte NewLine = 0x0a;
        private readonly Dictionary<string, ISerialDevice> _foundDevicesList = new Dictionary<string, ISerialDevice>();
        private readonly object _deviceListLocker = new object();
        private readonly SemaphoreSlim _messageReceivedLocker = new SemaphoreSlim(1, 1);

        #region Bindable properties

        public ObservableCollection<Item> Items { get; } = new ObservableCollection<Item>();

        private Item _selectedItem;
        public Item SelectedItem
        {
            get => _selectedItem;
            set
            {
                SetProperty(ref _selectedItem, value);
                if (value != null)
                {
                    Task.Run(async () => {
                        //When the user returns to the Items list, the most recently visited item
                        //  will no longer be selected.
                        await Task.Delay(500);
                        SelectedItem = null;
                    });
                    NavigationService.NavigateAsync(nameof(ItemDetailPage), new NavigationParameters { { NavParamKeys.DetailItem, value } });
                }
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set => SetProperty(ref _isBusy, value);
        }

        #endregion

        #region Commands and their implementations

        #region AddItemCommand

        private DelegateCommand _addItemCommand;
        public DelegateCommand AddItemCommand => _addItemCommand
            ?? (_addItemCommand = new DelegateCommand(async () => await NavigationService.NavigateAsync(nameof(NewItemPage))));

        #endregion

        #region LoadItemsCommand

        private DelegateCommand _loadItemsCommand;
        public DelegateCommand LoadItemsCommand => _loadItemsCommand
            ?? (_loadItemsCommand = new DelegateCommand(async () => { await ReloadItemList(); }));

        #endregion

        #endregion

        #region Private methods

        private byte[] JoinByteArrays(byte[] array1, byte[] array2) => array1.Concat(array2).ToArray();

        private void DatabaseDataReceived(byte[] data)
        {
            _incomingDatabaseBytes.Add(data);
            if (data.Contains(NewLine))
            {
                //Should have gotten all of the database bytes at this point
                _serialService.SetReceivedDataAction(MessageDataReceived);

                Task.Run(async () =>
                {
                    try
                    {
                        byte[] databaseBytes = { };

                        foreach (byte[] array in _incomingDatabaseBytes)
                        {
                            databaseBytes = JoinByteArrays(databaseBytes, array);
                        }
                        Debug.WriteLine($"Received database byte array length: {databaseBytes.Length}");

                        string base64 = Encoding.ASCII.GetString(databaseBytes);
                        base64 = base64.Substring(0, base64.Length - 1); //must trim off new line at the end
                        await _dataStoreService.ResetDatabaseFromBytes(Convert.FromBase64String(base64));
                        _incomingDatabaseBytes.Clear();
                        MessagingCenter.Send(new DeviceMessage(), DeviceMessage.DatabaseReceived);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e.ToString());
                        Debugger.Break();
                        throw;
                    }
                });
            }
        }

        //It is a colossally bad idea to have this "ReceivedData" method be 'async void'
        // because when a bunch of data is incoming, bytes will be dropped.
        // But it is currently working for the short 1-character messages that are being
        // received, so leaving it alone for now.  Should be implemented as IObservable/IObserver pattern.
        private async void MessageDataReceived(byte[] data)
        {
            if (data != null && data.Any())
            {
                await _messageReceivedLocker.WaitAsync();

                try
                {
                    Debug.WriteLine($"Bytes received: {(data.Length > 10 ? $"(not displaying {data.Length} bytes)" : BitConverter.ToString(data))}");
                    if (_waitForReceivedData != null
                        && (!_waitForReceivedData.Task.IsCanceled)
                        && (!_waitForReceivedData.Task.IsCompleted))
                    {
                        _waitForReceivedData.SetResult(data);
                    }
                    //else if (_isAwaitingDatabase)
                    //{
                    //    //Debug.WriteLine($"Database byte array size so far: {(_databaseBytes == null ? "0" : _databaseBytes.Length.ToString())}");
                    //    //if (_databaseBytes != null || data.Length > 1 || data[0] != NewLine)
                    //    //{
                    //    //    _databaseBytes = (_databaseBytes == null)
                    //    //        ? data
                    //    //        : JoinByteArrays(_databaseBytes, data);

                    //    //    if (data.Contains(NewLine))
                    //    //    {
                    //    //        Debug.WriteLine("Incoming newline detected.");
                    //    //        string databaseString = Encoding.ASCII.GetString(_databaseBytes);
                    //    //        byte[] databaseFile = Convert.FromBase64String(databaseString.Trim());
                    //    //        await _dataStoreService.ResetDatabaseFromBytes(databaseFile);
                    //    //        _databaseBytes = null;
                    //    //        _isAwaitingDatabase = false;
                    //    //        Device.BeginInvokeOnMainThread(async () => { await ReloadItemList(); });                                
                    //    //    }
                    //    //}
                    //}
                    else if (data[0] == (byte) 'g') //Get database
                    {
                        string textToSend = Convert.ToBase64String((await _dataStoreService.GetDatabaseAsBytes()));
                        await _serialService.SendTextLine(textToSend);
                        MessagingCenter.Send(new DeviceMessage(), DeviceMessage.DatabaseSent);
                    }
                    else if (data[0] == (byte) 's') //Sending database
                    {
                        _incomingDatabaseBytes.Clear();
                        _serialService.SetReceivedDataAction(DatabaseDataReceived);
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                    Debugger.Break();
                    throw;
                }
                finally
                {
                    _messageReceivedLocker.Release();
                }
            }
        }

        private async void SetWaitForDataTimeout(int milliseconds)
        {
            await Task.Run(async () =>
            {
                await Task.Delay(milliseconds);
                if (_waitForReceivedData != null
                    && (!_waitForReceivedData.Task.IsCanceled)
                    && (!_waitForReceivedData.Task.IsCompleted))
                {
                    _waitForReceivedData.SetResult(new byte[] {});
                }
            });
        }

        private void StartScanningForDevices()
        {
            _scanTokenSource = new CancellationTokenSource();
            Task.Run(async () =>
            {
                _isScanningForDevices = true;
                string connectedDeviceName = null;
                while (_scanTokenSource != null && (!_scanTokenSource.IsCancellationRequested) && connectedDeviceName == null)
                {
                    await Task.Delay(_deviceScanIntervalMilliseconds);
                    IList<ISerialDevice> devices;

                    try
                    {
                        devices = await _serialService.FindSerialDevices();
                    }
                    catch (Exception e)
                    {
                        Debugger.Break();
                        throw;
                    }

                    if (devices.Any())
                    {
                        lock (_deviceListLocker)
                        {
                            string[] foundDevices = devices.Select(s => s.DeviceName).ToArray();
                            string[] devicesToRemove = _foundDevicesList.Keys.Where(w => !foundDevices.Contains(w)).ToArray();

                            foreach (ISerialDevice device in devices.Where(w => !_foundDevicesList.ContainsKey(w.DeviceName)))
                            {
                                Debug.WriteLine("New USB serial device found:");
                                Debug.WriteLine(device.ToString());
                                _foundDevicesList.Add(device.DeviceName, device);
                                MessagingCenter.Send(new DeviceMessage(device.DeviceName, device), DeviceMessage.DeviceFound);
                            }

                            foreach (string deviceName in devicesToRemove)
                            {
                                _foundDevicesList.Remove(deviceName);
                            }
                        }

                        if (!_isTryingToConnect)
                        {
                            _isTryingToConnect = true;

                            IList<ISerialDevice> foundDevices;
                            lock (_deviceListLocker)
                            {
                                foundDevices = _foundDevicesList.Values.ToArray();
                            }

                            foreach (ISerialDevice device in foundDevices)
                            {
                                bool connected = await _serialService.ConnectDevice(device, MessageDataReceived);
                                if (connected)
                                {
                                    Debug.WriteLine("Attempting handshake with computer...");
                                    _waitForReceivedData = new TaskCompletionSource<byte[]>();
                                    bool dataSent = await _serialService.SendTextLine("hello");

                                    if (dataSent)
                                    {
                                        //Setting up a timeout for waiting to receive data
                                        SetWaitForDataTimeout(2000);

                                        byte[] received = await _waitForReceivedData.Task;
                                        if (received != null && received.Length > 0 && received[0] == 0x79)
                                        {
                                            connectedDeviceName = device.DeviceName;
                                            Debug.WriteLine("Connected to computer!");
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        _waitForReceivedData = null;
                                    }

                                    Debug.WriteLine("No successful handshake, so disconnecting...");
                                    await _serialService.DisconnectDevice();
                                }
                            }

                            _isTryingToConnect = false;
                        }
                    }
                    else
                    {
                        Debug.WriteLine("No devices found during scan.");
                    }
                }

                if (connectedDeviceName != null)
                {
                    MessagingCenter.Send(new DeviceMessage(connectedDeviceName), DeviceMessage.ConnectedToComputer);
                }

                _isScanningForDevices = false;
            });
        }

        private async Task ReloadItemList()
        {
            IsBusy = true;
            Items.Clear();
            foreach (Item item in await _dataStoreService.GetItemsAsync())
            {
                Items.Add(item);
            }
            IsBusy = false;
        }

        #endregion

        public override async void OnNavigatedTo(NavigationParameters parameters)
        {
            base.OnNavigatedTo(parameters);

            bool itemAdded = false;

            if (parameters?[NavParamKeys.NewItem] is Item item)
            {
                await _dataStoreService.AddItemAsync(item);
                itemAdded = true;
            }

            if (itemAdded || _isFirstLoad)
            {
                await ReloadItemList();
                _isFirstLoad = false;
            }
            
            if ((!_serialService.IsDeviceConnected) && (!_isScanningForDevices))
            {
                StartScanningForDevices();
            }
        }

        public MainPageViewModel(
            INavigationService navigationService, 
            IUserDialogs userDialogs, 
            IUsbSerialService serialService,
            IDataStoreService<Item> dataStoreService) 
            : base(navigationService, userDialogs)
        {
            _serialService = serialService ?? throw new ArgumentNullException(nameof(serialService));
            _dataStoreService = dataStoreService ?? throw new ArgumentNullException(nameof(serialService));

            MessagingCenter.Subscribe<DeviceMessage>(this, DeviceMessage.ConnectedToComputer, async (message) =>
            {
                await Task.Delay(2000);
                Device.BeginInvokeOnMainThread(() => userDialogs.Toast($"Connected to computer!"));
            });

            MessagingCenter.Subscribe<DeviceMessage>(this, DeviceMessage.DeviceFound, message =>
            {
                Device.BeginInvokeOnMainThread(() => userDialogs.Toast($"Serial device found: {message.DeviceName}"));
            });

            MessagingCenter.Subscribe<DeviceMessage>(this, DeviceMessage.DeviceDetached, message =>
            {
                Device.BeginInvokeOnMainThread(() => userDialogs.Toast($"Serial device removed: {message.DeviceName}"));
                StartScanningForDevices();
            });

            MessagingCenter.Subscribe<DeviceMessage>(this, DeviceMessage.DatabaseSent, message =>
            {
                Device.BeginInvokeOnMainThread(() => userDialogs.Toast("Database sent to computer!"));
            });

            MessagingCenter.Subscribe<DeviceMessage>(this, DeviceMessage.DatabaseReceived, message =>
            {
                Device.BeginInvokeOnMainThread(async () =>
                    {
                        userDialogs.Toast("Database received from computer!");
                        await ReloadItemList();
                    });
            });
        }
    }
}
