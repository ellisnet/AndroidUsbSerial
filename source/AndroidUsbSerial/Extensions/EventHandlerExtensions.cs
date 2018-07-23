using System;
using System.Threading;

namespace AndroidUsbSerial.Extensions
{
    static class EventHandlerExtensions
    {
        public static void RaiseEvent(this EventHandler handler, object sender, EventArgs e)
            => Volatile.Read(ref handler)?.Invoke(sender, e);

        public static void RaiseEvent<T>(this EventHandler<T> handler, object sender, T e) where T : EventArgs
            => Volatile.Read(ref handler)?.Invoke(sender, e);
    }
}
