using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KitchenDesigner.Core
{
    internal sealed class RegistryPublisherStore : IPublisherStore
    {
        private readonly string _oldCompanyPath;
        private readonly string _oldPath;
        private readonly string _newPath;

        public RegistryPublisherStore(string softwarePath, string oldCompany, string newCompany, string product)
        {
            _oldCompanyPath = softwarePath + "\\" + oldCompany;
            _oldPath = _oldCompanyPath + "\\" + product;
            _newPath = softwarePath + "\\" + newCompany + "\\" + product;
        }

        public string Describe => "registry HKCU\\" + _oldPath + " → HKCU\\" + _newPath;

        public bool OldHoldsData()
        {
            using var key = CurrentUserRegistryKey.Open(_oldPath, writable: false);
            return key != null && (key.SubKeyNames().Count > 0 || key.Values().Count > 0);
        }

        public bool NewHoldsData()
        {
            using var key = CurrentUserRegistryKey.Open(_newPath, writable: false);
            return key != null
                   && (key.SubKeyNames().Count > 0
                       || key.Values().Any(v => !EngineOwnedEntries.IsEnginePreference(v.Name)));
        }

        public void Move()
        {
            using (var source = CurrentUserRegistryKey.Open(_oldPath, writable: false)
                                ?? throw new IOException("no key HKCU\\" + _oldPath))
            using (var target = CurrentUserRegistryKey.Create(_newPath))
            {
                var before = target.Values();
                try
                {
                    target.CopyTreeFrom(source);
                    RequireSameTree(source, target);
                }
                catch (Exception copyFailure)
                {
                    try { RollBack(target, before); }
                    catch (Exception rollBackFailure)
                    {
                        throw new IOException(copyFailure.Message + "; rolling the new key back failed too: " + rollBackFailure.Message, copyFailure);
                    }
                    throw;
                }
            }
            CurrentUserRegistryKey.DeleteTree(_oldPath);
            CurrentUserRegistryKey.DeleteIfEmpty(_oldCompanyPath);
        }

        private static void RequireSameTree(CurrentUserRegistryKey source, CurrentUserRegistryKey target)
        {
            var copied = target.Values().ToDictionary(v => v.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var value in source.Values())
                if (!copied.TryGetValue(value.Name, out var twin) || !value.SameAs(twin))
                    throw new IOException("value " + value.Name + " did not copy byte for byte");
            foreach (var name in source.SubKeyNames())
            {
                using var sourceChild = CurrentUserRegistryKey.Open(source.Path + "\\" + name, writable: false)
                                        ?? throw new IOException("subkey " + name + " vanished during the move");
                using var targetChild = CurrentUserRegistryKey.Open(target.Path + "\\" + name, writable: false)
                                        ?? throw new IOException("subkey " + name + " was not copied");
                RequireSameTree(sourceChild, targetChild);
            }
        }

        private static void RollBack(CurrentUserRegistryKey target, IReadOnlyList<RegistryValue> before)
        {
            foreach (var name in target.SubKeyNames())
                CurrentUserRegistryKey.DeleteTree(target.Path + "\\" + name);
            var kept = new HashSet<string>(before.Select(v => v.Name), StringComparer.OrdinalIgnoreCase);
            foreach (var value in target.Values())
                if (!kept.Contains(value.Name)) target.DeleteValue(value.Name);
            foreach (var value in before)
                target.Set(value);
        }
    }
}
