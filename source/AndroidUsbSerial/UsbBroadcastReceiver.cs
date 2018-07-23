using System;
using System.Threading.Tasks;
using Android.Content;

namespace AndroidUsbSerial
{
    public class UsbBroadcastReceiver : BroadcastReceiver
    {
        private readonly Action<Context, Intent> _onReceiveAction;
        private bool _withUnregisterOnReceive;

        public UsbBroadcastReceiver(Action<Context, Intent> onReceiveAction)
        {
            _onReceiveAction = onReceiveAction;
        }

        public void Register(Context context, IntentFilter filter, bool withUnregisterOnReceive = false)
        {
            if (context == null) { throw new ArgumentNullException(nameof(context));}

            _withUnregisterOnReceive = withUnregisterOnReceive;
            context.RegisterReceiver(this, filter);
        }

        public void Register(Context context, string intentFilter, bool withUnregisterOnReceive = false) 
            => Register(context, new IntentFilter(intentFilter), withUnregisterOnReceive);

        public override void OnReceive(Context context, Intent intent)
        {
            _onReceiveAction?.Invoke(context, intent);
            if (_withUnregisterOnReceive)
            {
                context.UnregisterReceiver(this);
            }
        }
    }

    public class UsbBroadcastReceiver<T> : BroadcastReceiver
    {
        private readonly Func<Context, Intent, T> _onReceiveFunction;
        private bool _withUnregisterOnReceive;
        private readonly TaskCompletionSource<T> _tcs;

        public Task<T> ReceiveTask => _tcs.Task;

        public UsbBroadcastReceiver(Func<Context, Intent, T> onReceiveFunction)
        {
            _onReceiveFunction = onReceiveFunction;
            _tcs = new TaskCompletionSource<T>();
        }

        public void Register(Context context, IntentFilter filter, bool withUnregisterOnReceive = false)
        {
            if (context == null) { throw new ArgumentNullException(nameof(context)); }

            _withUnregisterOnReceive = withUnregisterOnReceive;
            context.RegisterReceiver(this, filter);
        }

        public void Register(Context context, string intentFilter, bool withUnregisterOnReceive = false)
            => Register(context, new IntentFilter(intentFilter), withUnregisterOnReceive);

        public override void OnReceive(Context context, Intent intent)
        {
            var result = default(T);

            if (_onReceiveFunction != null)
            {
                result = _onReceiveFunction.Invoke(context, intent);
            }

            if (_withUnregisterOnReceive)
            {
                context.UnregisterReceiver(this);
            }

            _tcs.SetResult(result);
        }
    }
}
