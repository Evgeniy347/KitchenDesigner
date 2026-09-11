using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal interface IPreferenceStore
    {
        bool Has(string key);
        int GetInt(string key, int fallback);
        string GetString(string key, string fallback);
        void SetInt(string key, int value);
        void SetString(string key, string value);
        void Delete(string key);
    }

    internal static class PreferenceStore
    {
        private static readonly IPreferenceStore Stored = new StoredPreferences();
        private static readonly IPreferenceStore SessionOnly = new SessionOnlyPreferences();

        internal static IPreferenceStore For(string[]? commandLineArgs) =>
            EphemeralSessionArgument.Parse(commandLineArgs) ? SessionOnly : Stored;

        internal static IPreferenceStore Current => For(Environment.GetCommandLineArgs());
    }

    internal sealed class StoredPreferences : IPreferenceStore
    {
        public bool Has(string key) => PlayerPrefs.HasKey(key);

        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);

        public string GetString(string key, string fallback) => PlayerPrefs.GetString(key, fallback);

        public void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }

        public void SetString(string key, string value)
        {
            PlayerPrefs.SetString(key, value ?? "");
            PlayerPrefs.Save();
        }

        public void Delete(string key)
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }

    internal sealed class SessionOnlyPreferences : IPreferenceStore
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>();

        public bool Has(string key) => _values.ContainsKey(key);

        public int GetInt(string key, int fallback) =>
            _values.TryGetValue(key, out var raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : fallback;

        public string GetString(string key, string fallback) =>
            _values.TryGetValue(key, out var raw) ? raw : fallback;

        public void SetInt(string key, int value) =>
            _values[key] = value.ToString(CultureInfo.InvariantCulture);

        public void SetString(string key, string value) => _values[key] = value ?? "";

        public void Delete(string key) => _values.Remove(key);
    }
}
