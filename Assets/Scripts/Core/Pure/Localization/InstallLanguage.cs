using System;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace KitchenDesigner.Core
{
    public static class InstallLanguage
    {
        public const string RegistryKey = @"Software\KitchenDesigner";
        public const string ValueName = "InstallLanguage";

        public static string? Read()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return null;
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKey);
                return key?.GetValue(ValueName) as string;
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException
                || e is IOException || e is PlatformNotSupportedException)
            {
                return null;
            }
        }
    }
}
