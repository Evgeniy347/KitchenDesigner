using System;
using System.Runtime.InteropServices;
using System.Text;

namespace KitchenDesigner.Core
{
    public static class InstallLanguage
    {
        public const string RegistryKey = @"Software\KitchenDesigner";
        public const string ValueName = "InstallLanguage";

        private const int MaxChars = 64;
        private const uint RegSzOnly = 0x2;
        private static readonly IntPtr CurrentUser = new IntPtr(unchecked((int)0x80000001));

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegGetValueW")]
        private static extern int RegGetValue(IntPtr hKey, string subKey, string value, uint flags,
            out uint type, StringBuilder data, ref uint size);

        public static string? Read()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return null;
            try
            {
                var data = new StringBuilder(MaxChars);
                uint size = (uint)(MaxChars * sizeof(char));
                int status = RegGetValue(CurrentUser, RegistryKey, ValueName, RegSzOnly, out _, data, ref size);
                return status == 0 ? data.ToString() : null;
            }
            catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException)
            {
                return null;
            }
        }
    }
}
