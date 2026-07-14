using System;
using System.IO;
using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace KitchenDesigner.Core
{
    /// <summary>
    /// Системный диалог открытия/сохранения файла.
    ///   • В редакторе — UnityEditor.EditorUtility (надёжно).
    ///   • В Windows-плеере — comdlg32 (GetOpenFileName/GetSaveFileName).
    ///   • На прочих платформах / при сбое — null (вызывающий код делает fallback).
    /// Возвращает полный путь к файлу или null, если отменено.
    /// </summary>
    public static class NativeFileDialog
    {
        private const string JsonFilter = "Проект кухни (*.json)\0*.json\0Все файлы (*.*)\0*.*\0\0";

        public static string? OpenDialog(string title, string initialDir)
        {
#if UNITY_EDITOR
            return UnityEditor.EditorUtility.OpenFilePanel(title, SafeDir(initialDir), "json");
#elif UNITY_STANDALONE_WIN
            return WinDialog(title, "", initialDir, false);
#else
            Debug.LogWarning("[FileDialog] Системный диалог недоступен на этой платформе");
            return null;
#endif
        }

        public static string? SaveDialog(string title, string defaultName, string initialDir)
        {
#if UNITY_EDITOR
            string dir = SafeDir(initialDir);
            string name = string.IsNullOrEmpty(defaultName) ? "project" : Path.GetFileNameWithoutExtension(defaultName);
            return UnityEditor.EditorUtility.SaveFilePanel(title, dir, name, "json");
#elif UNITY_STANDALONE_WIN
            string? path = WinDialog(title, defaultName, initialDir, true);
            if (!string.IsNullOrEmpty(path) && !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                path += ".json";
            return path;
#else
            Debug.LogWarning("[FileDialog] Системный диалог недоступен на этой платформе");
            return null;
#endif
        }

        private static string SafeDir(string dir)
        {
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) return dir;
            return Application.persistentDataPath;
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Класс маршалится как указатель; строковые поля с CharSet.Auto сохраняют
        // встроенные \0 фильтра (длина управляемой строки учитывает их).
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class OpenFileName
        {
            public int structSize = 0;
            public IntPtr dlgOwner = IntPtr.Zero;
            public IntPtr instance = IntPtr.Zero;
            public string? filter = null;
            public string? customFilter = null;
            public int maxCustFilter = 0;
            public int filterIndex = 0;
            public string? file = null;
            public int maxFile = 0;
            public string? fileTitle = null;
            public int maxFileTitle = 0;
            public string? initialDir = null;
            public string? title = null;
            public int flags = 0;
            public short fileOffset = 0;
            public short fileExtension = 0;
            public string? defExt = null;
            public IntPtr custData = IntPtr.Zero;
            public IntPtr hook = IntPtr.Zero;
            public string? templateName = null;
            public IntPtr reservedPtr = IntPtr.Zero;
            public int reservedInt = 0;
            public int flagsEx = 0;
        }

        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool GetSaveFileName([In, Out] OpenFileName ofn);

        private const int OFN_FILEMUSTEXIST   = 0x00001000;
        private const int OFN_PATHMUSTEXIST   = 0x00000800;
        private const int OFN_OVERWRITEPROMPT = 0x00000002;
        private const int OFN_NOCHANGEDIR     = 0x00000008;

        private static string? WinDialog(string title, string defaultName, string initialDir, bool save)
        {
            try
            {
                var ofn = new OpenFileName();
                ofn.structSize = Marshal.SizeOf(ofn);
                ofn.filter = JsonFilter;
                ofn.file = new string(new char[2048]);
                if (!string.IsNullOrEmpty(defaultName))
                {
                    string n = Path.GetFileName(defaultName);
                    ofn.file = n + new string('\0', 2048 - n.Length);
                }
                ofn.maxFile = 2048;
                ofn.fileTitle = new string(new char[512]);
                ofn.maxFileTitle = 512;
                ofn.initialDir = SafeDir(initialDir);
                ofn.title = title;
                ofn.defExt = "json";
                ofn.flags = save
                    ? (OFN_PATHMUSTEXIST | OFN_OVERWRITEPROMPT | OFN_NOCHANGEDIR)
                    : (OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR);

                bool ok = save ? GetSaveFileName(ofn) : GetOpenFileName(ofn);
                if (!ok) return null; // отмена или ошибка
                return string.IsNullOrEmpty(ofn.file) ? null : ofn.file.TrimEnd('\0');
            }
            catch (Exception ex)
            {
                Debug.LogError("[FileDialog] Сбой системного диалога: " + ex.Message);
                return null;
            }
        }
#endif
    }
}
