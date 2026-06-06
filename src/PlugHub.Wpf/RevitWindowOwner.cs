using System;
using System.Diagnostics;
using System.Windows.Interop;

namespace PlugHub.Wpf
{
    public static class RevitWindowOwner
    {
        public static bool? ShowDialog(System.Windows.Window window)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));

            var handle = Process.GetCurrentProcess().MainWindowHandle;
            if (handle != IntPtr.Zero)
            {
                new WindowInteropHelper(window).Owner = handle;
            }

            return window.ShowDialog();
        }
    }
}
