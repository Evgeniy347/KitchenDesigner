using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace KitchenDesigner.Core
{
    internal sealed class CurrentUserRegistryKey : IDisposable
    {
        private const int KeyRead = 0x20019;
        private const int KeyAllAccess = 0xF003F;
        private const int Success = 0;
        private const int NotFound = 2;
        private const int NoMoreItems = 259;
        private static readonly IntPtr CurrentUser = new IntPtr(unchecked((int)0x80000001));

#pragma warning disable SYSLIB1054
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegOpenKeyExW(IntPtr hKey, string subKey, int options, int sam, out IntPtr result);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegCreateKeyExW(IntPtr hKey, string subKey, int reserved, string? keyClass,
            int options, int sam, IntPtr security, out IntPtr result, out int disposition);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegQueryInfoKeyW(IntPtr hKey, IntPtr keyClass, IntPtr classLength, IntPtr reserved,
            out int subKeys, out int maxSubKeyLength, IntPtr maxClassLength, out int values,
            out int maxValueNameLength, out int maxValueLength, IntPtr security, IntPtr lastWrite);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegEnumValueW(IntPtr hKey, int index, StringBuilder name, ref int nameLength,
            IntPtr reserved, out int kind, byte[] data, ref int dataLength);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegEnumKeyExW(IntPtr hKey, int index, StringBuilder name, ref int nameLength,
            IntPtr reserved, IntPtr keyClass, IntPtr classLength, IntPtr lastWrite);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegSetValueExW(IntPtr hKey, string name, int reserved, int kind, byte[] data, int size);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegDeleteValueW(IntPtr hKey, string name);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegCopyTreeW(IntPtr source, string? subKey, IntPtr destination);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegDeleteTreeW(IntPtr hKey, string? subKey);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegDeleteKeyW(IntPtr hKey, string subKey);

        [DllImport("advapi32.dll")]
        private static extern int RegCloseKey(IntPtr hKey);
#pragma warning restore SYSLIB1054

        private IntPtr _handle;

        private CurrentUserRegistryKey(IntPtr handle, string path)
        {
            _handle = handle;
            Path = path;
        }

        public string Path { get; }

        public static CurrentUserRegistryKey? Open(string path, bool writable)
        {
            int status = RegOpenKeyExW(CurrentUser, path, 0, writable ? KeyAllAccess : KeyRead, out var handle);
            if (status == NotFound) return null;
            Check(status, "open", path);
            return new CurrentUserRegistryKey(handle, path);
        }

        public static CurrentUserRegistryKey Create(string path)
        {
            Check(RegCreateKeyExW(CurrentUser, path, 0, null, 0, KeyAllAccess, IntPtr.Zero, out var handle, out _),
                "create", path);
            return new CurrentUserRegistryKey(handle, path);
        }

        public static void DeleteTree(string path)
        {
            using var parent = Open(ParentOf(path), writable: true);
            if (parent == null) return;
            int status = RegDeleteTreeW(parent._handle, LeafOf(path));
            if (status != NotFound) Check(status, "delete", path);
        }

        public static bool DeleteIfEmpty(string path)
        {
            using (var key = Open(path, writable: false))
            {
                if (key == null) return false;
                var (subKeys, values, _, _) = key.Info();
                if (subKeys > 0 || values > 0) return false;
            }
            using var parent = Open(ParentOf(path), writable: true);
            if (parent == null) return false;
            Check(RegDeleteKeyW(parent._handle, LeafOf(path)), "delete", path);
            return true;
        }

        public IReadOnlyList<string> SubKeyNames()
        {
            var (subKeys, _, _, _) = Info();
            var names = new List<string>(subKeys);
            var buffer = new StringBuilder(256);
            for (int i = 0; ; i++)
            {
                int length = buffer.Capacity;
                int status = RegEnumKeyExW(_handle, i, buffer, ref length, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (status == NoMoreItems) return names;
                Check(status, "enumerate subkeys of", Path);
                names.Add(buffer.ToString(0, length));
            }
        }

        public IReadOnlyList<RegistryValue> Values()
        {
            var (_, count, maxName, maxData) = Info();
            var values = new List<RegistryValue>(count);
            var name = new StringBuilder(maxName + 1);
            var data = new byte[Math.Max(maxData, 1)];
            for (int i = 0; ; i++)
            {
                int nameLength = name.Capacity;
                int dataLength = data.Length;
                int status = RegEnumValueW(_handle, i, name, ref nameLength, IntPtr.Zero, out var kind, data, ref dataLength);
                if (status == NoMoreItems) return values;
                Check(status, "read values of", Path);
                var copy = new byte[dataLength];
                Array.Copy(data, copy, dataLength);
                values.Add(new RegistryValue(name.ToString(0, nameLength), kind, copy));
            }
        }

        public void Set(RegistryValue value) =>
            Check(RegSetValueExW(_handle, value.Name, 0, value.Kind, value.Data, value.Data.Length),
                "write", Path + "\\" + value.Name);

        public void DeleteValue(string name) => Check(RegDeleteValueW(_handle, name), "delete", Path + "\\" + name);

        public void CopyTreeFrom(CurrentUserRegistryKey source) =>
            Check(RegCopyTreeW(source._handle, null, _handle), "copy", source.Path + " → " + Path);

        public void Dispose()
        {
            if (_handle == IntPtr.Zero) return;
            RegCloseKey(_handle);
            _handle = IntPtr.Zero;
        }

        private (int subKeys, int values, int maxValueName, int maxValueData) Info()
        {
            Check(RegQueryInfoKeyW(_handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, out var subKeys, out _, IntPtr.Zero,
                out var values, out var maxValueName, out var maxValueData, IntPtr.Zero, IntPtr.Zero), "query", Path);
            return (subKeys, values, maxValueName, maxValueData);
        }

        private static string ParentOf(string path) => path.Substring(0, path.LastIndexOf('\\'));

        private static string LeafOf(string path) => path.Substring(path.LastIndexOf('\\') + 1);

        private static void Check(int status, string action, string path)
        {
            if (status != Success)
                throw new IOException("registry: cannot " + action + " HKCU\\" + path + " (error " + status + ")");
        }
    }
}
