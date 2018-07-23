using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.Content;
using Android.Hardware.Usb;
using Android.Util;
using AndroidUsbSerial.Extensions;

namespace AndroidUsbSerial
{
    public enum ManagerState
    {
        Stopped,
        Running,
        Stopping,
    }

    public class PortManager : IDisposable
    {
        private static readonly int DefaultBufferSize = 4096;
        private static readonly int ReadWaitMilliseconds = 200;
        private static readonly int DefaultBaudRate = 9600;
        private static readonly DataBits DefaultDataBits = DataBits.Eight;
        private static readonly Parity DefaultParity = Parity.None;
        private static readonly StopBits DefaultStopBits = StopBits.One;
        private static readonly int DefaultWriteTimeoutMilliseconds = 1000;

        private readonly object _stateLocker = new object();
        private readonly object _writeBufferLocker = new object();
        private readonly object _writePortLocker = new object();

        private readonly IUsbSerialPort _port;
        private CancellationTokenSource _runCancellationSource;
        private byte[] _portBuffer;

        private ManagerState _managerState = ManagerState.Stopped;
        public ManagerState ManagerState
        {
            get
            {
                lock (_stateLocker)
                {
                    return _managerState;
                }
            }
        }

        public bool IsRunning => (ManagerState == ManagerState.Running);
        public bool IsStopping => (ManagerState == ManagerState.Stopping);
        public bool IsStopped => (ManagerState == ManagerState.Stopped);
        public bool IsDisposed => _isDisposed;

        public int BaudRate { get; set; }

        public Parity Parity { get; set; }

        public DataBits DataBits { get; set; }

        public StopBits StopBits { get; set; }

        public int BufferSize { get; set; }

        public int WriteTimeoutMilliseconds { get; set; }

        public event EventHandler<SerialDataEventArgs> DataReceived;

        public event EventHandler<UnhandledExceptionEventArgs> ErrorReceived;

        public static UsbManager GetUsbManager(Context context)
        {
            if (context == null) { throw new ArgumentNullException(nameof(context));}
            return context.GetSystemService(Context.UsbService) as UsbManager;
        }

        public PortManager(IUsbSerialPort port, int? bufferSize = null, int? writeTimeoutMilliseconds = null)
        {
            _port = port ?? throw new ArgumentNullException(nameof(port));
            BufferSize = bufferSize ?? DefaultBufferSize;
            WriteTimeoutMilliseconds = writeTimeoutMilliseconds ?? DefaultWriteTimeoutMilliseconds;
            BaudRate = DefaultBaudRate;
            Parity = DefaultParity;
            DataBits = DefaultDataBits;
            StopBits = DefaultStopBits;
        }

        private ManagerState GetCurrentState() => _managerState; //doesn't use a lock

        private void Step()
        {
            lock (_writeBufferLocker)
            {
                int dataLength = _port.Read(_portBuffer, ReadWaitMilliseconds);
                if (dataLength > 0)
                {
                    Log.Debug(nameof(PortManager), $"Read data len={dataLength}");
                    var data = new byte[dataLength];
                    Array.Copy(_portBuffer, data, dataLength);
                    // ReSharper disable once PossibleInvalidCastException
                    DataReceived.RaiseEvent(this, new SerialDataEventArgs(data));
                }
            }
        }

        public virtual void Run(UsbManager usbManager)
        {
            if (usbManager == null) { throw new ArgumentNullException(nameof(usbManager));}

            if (_isDisposed) { throw new ObjectDisposedException(nameof(PortManager));}

            // ReSharper disable once RedundantAssignment
            bool startRunning = false;

            lock (_stateLocker)
            {
                if (GetCurrentState() != ManagerState.Stopped)
                {
                    throw new InvalidOperationException("Already running.");
                }
                else
                {
                    _managerState = ManagerState.Running;
                    startRunning = true;
                }
            }

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (startRunning)
            {
                try
                {
                    UsbDeviceConnection connection = usbManager.OpenDevice(_port.Driver.Device);
                    if (connection == null)
                    {
                        throw new UsbSerialException("Failed to connect to device.");
                    }

                    _portBuffer = new byte[BufferSize];
                    _port.Open(connection);
                    _port.SetParameters(BaudRate, DataBits, StopBits, Parity);

                    _runCancellationSource = new CancellationTokenSource();
                    CancellationToken runCancellationToken = _runCancellationSource.Token;
                    runCancellationToken.Register(() =>
                        Log.Info(nameof(PortManager), "Cancellation Requested"));

                    Task.Run(() =>
                    {
                        Log.Info(nameof(PortManager), "Running ..");

                        try
                        {
                            bool isRunning;
                            do
                            {
                                ManagerState currentState;
                                lock (_stateLocker)
                                {
                                    isRunning = ((currentState = GetCurrentState()) == ManagerState.Running);
                                }

                                if (isRunning)
                                {
                                    runCancellationToken.ThrowIfCancellationRequested();
                                    Step();
                                }
                                else
                                {
                                    Log.Info(nameof(PortManager), $"Stopping mState={currentState}");
                                }
                            } while (isRunning);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception e)
                        {
                            Log.Warn(nameof(PortManager), $"Task ending due to exception: {e.Message}", e);
                            ErrorReceived.RaiseEvent(this, new UnhandledExceptionEventArgs(e, false));
                        }
                        finally
                        {
                            _port.Close();
                            _portBuffer = null;
                            lock (_stateLocker)
                            {
                                _managerState = ManagerState.Stopped;
                                Log.Info(nameof(PortManager), "Stopped.");
                            }
                        }
                    }, runCancellationToken);
                }
                catch (Exception e)
                {
                    lock (_stateLocker)
                    {
                        _managerState = ManagerState.Stopped;
                        Log.Info(nameof(PortManager), "Stopped.");
                    }
                    Log.Warn(nameof(PortManager), $"Run ending due to exception: {e.Message}", e);
                    ErrorReceived.RaiseEvent(this, new UnhandledExceptionEventArgs(e, false));
                }
            }
        }

        public virtual void Run(Context context)
        {
            if (context == null) { throw new ArgumentNullException(nameof(context)); }
            Run(GetUsbManager(context));
        }

        public virtual void Stop()
        {
            if (_isDisposed) { throw new ObjectDisposedException(nameof(PortManager));}

            lock (_stateLocker)
            {
                if (GetCurrentState() == ManagerState.Running)
                {
                    Log.Info(nameof(PortManager), "Stop requested");
                    _managerState = ManagerState.Stopping;
                }
            }

            if (_runCancellationSource != null && !_runCancellationSource.IsCancellationRequested)
            {
                _runCancellationSource.Cancel();
            }
        }

        public virtual int Send(byte[] data, int? writeTimeoutMilliseconds = null)
        {
            if (data == null) { throw new ArgumentNullException(nameof(data));}
            if (!IsRunning) { throw new InvalidOperationException($"Cannot send data when manager state is {ManagerState}.");}

            int result = 0;

            lock (_writePortLocker)
            {
                if (data.Any())
                {
                    result = _port.Write(data, writeTimeoutMilliseconds ?? WriteTimeoutMilliseconds);
                }
            }

            return result;
        }

        public virtual Task<int> SendAsync(byte[] data, int? writeTimeoutMilliseconds = null)
        {
            if (data == null) { throw new ArgumentNullException(nameof(data)); }
            if (!IsRunning) { throw new InvalidOperationException($"Cannot send data when manager state is {ManagerState}."); }

            int writeTimeout = writeTimeoutMilliseconds ?? WriteTimeoutMilliseconds;

            var tcs = new TaskCompletionSource<int>();

            Task.Run(() =>
            {
                lock (_writePortLocker)
                {
                    int written = 0;
                    Exception exception = null;

                    if (data.Any())
                    {
                        try
                        {
                            written = _port.Write(data, writeTimeoutMilliseconds ?? WriteTimeoutMilliseconds);
                        }
                        catch (Exception e)
                        {
                            exception = e;
                        }
                    }

                    if (exception == null)
                    {
                        tcs.SetResult(written);
                    }
                    else
                    {
                        tcs.SetException(exception);
                    }
                }
            });

            return tcs.Task;
        }

        #region IDisposable implementation

        private bool _isDisposed;

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    Stop();
                }

                _isDisposed = true;
            }
        }

        ~PortManager()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
