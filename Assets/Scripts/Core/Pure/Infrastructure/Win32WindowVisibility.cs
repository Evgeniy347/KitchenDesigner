using System;
using System.Runtime.InteropServices;

namespace KitchenDesigner.Core
{
    internal static class Win32WindowVisibility
    {
        private const int SW_HIDE = 0;

#pragma warning disable SYSLIB1054
        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
#pragma warning restore SYSLIB1054

        public static void HideActiveWindow()
        {
            var handle = GetActiveWindow();
            if (handle != IntPtr.Zero) ShowWindow(handle, SW_HIDE);
        }
    }
}
