using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharedSerial.Models;
using SharedSerial.Services;

namespace WinDotNetUsbSerial.ViewModels
{
    public class MainViewModel : SimpleViewModel
    {
        // ReSharper disable InconsistentNaming

        private static readonly string DefaultPortName = "COM8";
        private static readonly int DefaultBaudRate = 115200;
        private static readonly Parity DefaultParity = Parity.None;
        private static readonly int DefaultDataBits = 8;
        private static readonly StopBits DefaultStopBits = StopBits.One;

        private static readonly byte NewLine = 0x0a;

        private static readonly string ConnectMsg = "hello";
        private static readonly string ConnectAckMsg = "yes";

        private static readonly string _libraryPath = @"C:\\Temp";

        // ReSharper restore InconsistentNaming

        private SerialPort _port;
        private string _portName;
        private readonly SemaphoreSlim _incomingDataLocker = new SemaphoreSlim(1, 1);
        private readonly IDataStoreService<Item> _dataStoreService;
        private bool _isAwaitingDatabase;

        #region Bindable properties

        public bool IsSearching { get; set; }
        public bool IsConnected { get; set; }
        public bool IsSendingMessages { get; set; }

        private bool _isInDatabaseOperation;
        public bool IsInDatabaseOperation
        {
            get => _isInDatabaseOperation;
            set
            {
                _isInDatabaseOperation = value;
                NotifyPropertyChanged(nameof(IsInDatabaseOperation));
                InvokeOnMainThread(() =>
                {
                    GetDatabaseCommand.RaiseCanExecuteChanged();
                    SendDatabaseCommand.RaiseCanExecuteChanged();
                });
            }
        }

        public ObservableCollection<Item> Items { get; } = new ObservableCollection<Item>();

        private string _newItemText;
        public string NewItemText
        {
            get => _newItemText;
            set
            {
                _newItemText = value;
                NotifyPropertyChanged(nameof(NewItemText));
                AddItemCommand.RaiseCanExecuteChanged();
            }
        }

        private string _newItemDescription;
        public string NewItemDescription
        {
            get => _newItemDescription;
            set
            {
                _newItemDescription = value;
                NotifyPropertyChanged(nameof(NewItemDescription));
                AddItemCommand.RaiseCanExecuteChanged();
            }
        }

        #endregion

        #region Commands and their implementations

        #region SearchForSerialCommand

        private SimpleCommand _searchForSerialCommand;
        public SimpleCommand SearchForSerialCommand => _searchForSerialCommand
            ?? (_searchForSerialCommand = new SimpleCommand(CanSearchForSerial, DoSearchForSerial));

        public async void DoSearchForSerial()
        {
            try
            {
                IsSearching = true;
                NotifyPropertyChanged(nameof(IsSearching));
                SearchForSerialCommand.RaiseCanExecuteChanged();

                string[] portNames = SerialPort.GetPortNames() ?? new string[] { };

                _portName = null;

                if (portNames.Length > 1)
                {
                    _portName = portNames.FirstOrDefault(a => a.Equals(DefaultPortName, StringComparison.CurrentCultureIgnoreCase));
                    if (_portName == null)
                    {
                        throw new Exception($"Could not figure out which of these ports to open: {String.Join(",", portNames)}");
                    }
                }
                else if (portNames.Length == 1)
                {
                    _portName = portNames[0];
                }

                if (_portName == null)
                {
                    throw new Exception("An available serial port could not be found.");
                }

                _port = new SerialPort(_portName, DefaultBaudRate, DefaultParity, DefaultDataBits,
                    DefaultStopBits);

                _port.DataReceived += SerialPortDataReceived;

                _port.Open();

                await ShowInfo($"Waiting for incoming connections on port: {_portName}");
                //_port.WriteLine(ConnectMsg);
                //_port.Close();
            }
            catch (Exception e)
            {
                await ShowError(e, "The serial port search operation failed.");
            }
        }

        private async void SerialPortDataReceived(object sender, SerialDataReceivedEventArgs args)
        {
            string incoming = null;

            try
            {
                if (args != null && args.EventType == SerialData.Chars)
                {
                    incoming = _port.ReadLine();
                    incoming = String.IsNullOrWhiteSpace(incoming) ? null : incoming.Trim();

                    if (incoming != null)
                    {
                        await _incomingDataLocker.WaitAsync();

                        if (IsSearching && (!IsConnected) && incoming.Equals(ConnectMsg, StringComparison.CurrentCultureIgnoreCase))
                        {
                            _port.WriteLine(ConnectAckMsg);
                            await ShowInfo($"Device detected on port: {_portName}");

                            IsConnected = true;
                            IsSearching = false;
                            NotifyPropertyChanged(nameof(IsSearching));
                            NotifyPropertyChanged(nameof(IsConnected));

                            //Need to run on main/UI thread:
                            InvokeOnMainThread(() =>
                            {
                                SearchForSerialCommand.RaiseCanExecuteChanged();
                                SendTestMessagesCommand.RaiseCanExecuteChanged();
                                GetDatabaseCommand.RaiseCanExecuteChanged();
                                SendDatabaseCommand.RaiseCanExecuteChanged();
                            });                            
                        }
                        else if (_isAwaitingDatabase)
                        {
                            await _dataStoreService.ResetDatabaseFromBytes(Convert.FromBase64String(incoming));
                            _isAwaitingDatabase = false;
                            IsInDatabaseOperation = false;
                            InvokeOnMainThread(async () => { await ReloadItemList();});
                        }
                        else
                        {
                            //TODO: Will want to do something with this other than write it to the output
                            Debug.WriteLine($"Incoming message: {incoming}");
                        }
                    }
                }
                else if (args != null)
                {
                    Debugger.Break(); //take a look at what I got
                }
            }
            catch (Exception e)
            {
                await ShowError(e, "An error occurred while receiving data.");
            }
            finally
            {
                if (incoming != null)
                {
                    _incomingDataLocker.Release();
                }                
            }
        }

        public bool CanSearchForSerial() => (!IsSearching) && (!IsConnected);

        #endregion

        #region SendTestMessagesCommand

        private SimpleCommand _sendTestMessagesCommand;
        public SimpleCommand SendTestMessagesCommand => _sendTestMessagesCommand
            ?? (_sendTestMessagesCommand = new SimpleCommand(CanSendTestMessages, DoSendTestMessages));

        public async void DoSendTestMessages()
        {
            try
            {
                IsSendingMessages = true;
                NotifyPropertyChanged(nameof(IsSendingMessages));
                SendTestMessagesCommand.RaiseCanExecuteChanged();

                //_port.Open();

                for (int i = 0; i < 5; i++)
                {
                    if (i > 0) { await Task.Delay(3000);}
                    _port.WriteLine($"Test message: {i + 1}");
                }

                //_port.Close();
            }
            catch (Exception e)
            {
                await ShowError(e, "The serial port search operation failed.");
            }

            IsSendingMessages = false;
            NotifyPropertyChanged(nameof(IsSendingMessages));
            SendTestMessagesCommand.RaiseCanExecuteChanged();
        }

        public bool CanSendTestMessages() => IsConnected && _port != null && (!IsSendingMessages);

        #endregion

        #region GetDatabaseCommand

        private SimpleCommand _getDatabaseCommand;
        public SimpleCommand GetDatabaseCommand => _getDatabaseCommand
            ?? (_getDatabaseCommand = new SimpleCommand(CanGetDatabase, DoGetDatabase));

        public void DoGetDatabase()
        {
            if (CanGetDatabase())
            {
                _isAwaitingDatabase = true;
                IsInDatabaseOperation = true;
                _port.Write("g");
            }
        }

        public bool CanGetDatabase() => _port != null
            && IsConnected
            && (!IsInDatabaseOperation);

        #endregion

        #region SendDatabaseCommand

        private SimpleCommand _sendDatabaseCommand;
        public SimpleCommand SendDatabaseCommand => _sendDatabaseCommand
            ?? (_sendDatabaseCommand = new SimpleCommand(CanSendDatabase, DoSendDatabase));

        public async void DoSendDatabase()
        {
            if (CanGetDatabase())
            {
                IsInDatabaseOperation = true;
                _port.Write("s");
                await Task.Delay(500);
                string base64 = Convert.ToBase64String(await _dataStoreService.GetDatabaseAsBytes());
                byte[] databaseBytes = Encoding.ASCII.GetBytes(base64);
                //_port.WriteLine(Convert.ToBase64String(databaseBytes));
                await SendAsChunks(_port, databaseBytes, 100, 50, NewLine);
                IsInDatabaseOperation = false;
            }
        }

        public bool CanSendDatabase() => _port != null 
            && IsConnected 
            && (!IsInDatabaseOperation);

        #endregion

        #region AddItemCommand

        private SimpleCommand _addItemCommand;
        public SimpleCommand AddItemCommand => _addItemCommand
            ?? (_addItemCommand = new SimpleCommand(CanAddItem, DoAddItem));

        public async void DoAddItem()
        {
            if (CanAddItem())
            {
                await _dataStoreService.AddItemAsync(new Item { Text = NewItemText, Description = NewItemDescription });

                //After saving, clear the fields
                NewItemText = "";
                NewItemDescription = "";

                await ReloadItemList();
            }
        }

        public bool CanAddItem() => (!String.IsNullOrWhiteSpace(NewItemText)) && (!String.IsNullOrWhiteSpace(NewItemDescription));

        #endregion

        #endregion

        private byte[] JoinByteArrays(byte[] array1, byte[] array2) => array1.Concat(array2).ToArray();

        //TODO: This is my crappy attempt to do flow control, need to replace it with real serial port buffering/flow control
        private async Task SendAsChunks(
            SerialPort port, 
            byte[] data, 
            int chunkSize, 
            int nextChunkDelayMilliseconds,
            byte? lastCharacter = null)
        {
            if (port == null) { throw new ArgumentNullException(nameof(port));}
            if (data == null) { throw new ArgumentNullException(nameof(data));}
            if (chunkSize < 10) { throw new ArgumentOutOfRangeException(nameof(chunkSize));}
            if (nextChunkDelayMilliseconds < 1) { throw new ArgumentOutOfRangeException(nameof(nextChunkDelayMilliseconds)); }

            if (data.Length > 0)
            {
                byte[] bytesToSend = (lastCharacter.HasValue)
                    ? JoinByteArrays(data, new[] { lastCharacter.Value })
                    : data;

                int offset = 0;

                while (offset < bytesToSend.Length)
                {
                    int currentChunkLength = (bytesToSend.Length > (offset + chunkSize))
                        ? chunkSize
                        : bytesToSend.Length - offset;

                    port.Write(bytesToSend, offset, currentChunkLength);

                    offset += chunkSize;
                    if (offset < bytesToSend.Length)
                    {
                        await Task.Delay(nextChunkDelayMilliseconds);
                    }
                }
            }
        }

        private async Task ReloadItemList()
        {
            Items.Clear();
            foreach (Item item in await _dataStoreService.GetItemsAsync())
            {
                Items.Add(item);
            }
        }

        private async void AutoSerialCheck()
        {
            await Task.Delay(5000); //Waiting for app startup to complete
            DoSearchForSerial();
        }
       
        public MainViewModel()
        {
            //TODO: Turn these back on when I am done with editing the XAML file
            _dataStoreService = new SqliteDataStoreService(_libraryPath);
            AutoSerialCheck();
#pragma warning disable 4014
            ReloadItemList();
#pragma warning restore 4014
        }
    }
}
