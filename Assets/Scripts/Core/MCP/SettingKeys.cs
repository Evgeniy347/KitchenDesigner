using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP
{
    internal sealed class SettingKey
    {
        private SettingKey(string wire, string field, Func<object> read,
            Action<bool>? writeFlag, Action<float>? writeNumber)
        {
            Wire = wire;
            Field = field;
            Read = read;
            WriteFlag = writeFlag;
            WriteNumber = writeNumber;
        }

        public string Wire { get; }

        public string Field { get; }

        public Func<object> Read { get; }

        public Action<bool>? WriteFlag { get; }

        public Action<float>? WriteNumber { get; }

        public bool IsNumber => WriteNumber != null;

        public static SettingKey Flag(string wire, string field, Func<bool> read, Action<bool> write)
            => new SettingKey(wire, field, () => read(), write, null);

        public static SettingKey Number(string wire, string field, Func<object> read, Action<float> write)
            => new SettingKey(wire, field, read, null, write);
    }

    internal static class SettingKeys
    {
        private static KitchenSettings S => KitchenSettings.Instance;

        public static readonly IReadOnlyList<SettingKey> All = new[]
        {
            SettingKey.Flag("snap_enabled", "snapEnabled",
                () => S.SnapEnabled, v => S.SnapEnabled = v),
            SettingKey.Number("snap_threshold", "snapThresholdMM",
                () => S.SnapThreshold, v => S.SnapThreshold = v),
            SettingKey.Flag("grid_enabled", "gridEnabled",
                () => S.GridEnabled, v => S.GridEnabled = v),
            SettingKey.Number("grid_step", "gridStepMM",
                () => S.GridStep, v => S.GridStep = (int)v),
            SettingKey.Flag("block_on_violation", "blockOnViolation",
                () => S.BlockOnViolation, v => S.BlockOnViolation = v),
            SettingKey.Flag("auto_save", "autoSave",
                () => S.AutoSave, v => S.AutoSave = v),
            SettingKey.Number("auto_save_interval", "autoSaveIntervalSec",
                () => S.AutoSaveInterval, v => S.AutoSaveInterval = (int)v),
            SettingKey.Flag("snap_verbose_log", "snapVerboseLog",
                () => SnapSystem.VerboseLog, v => SnapSystem.VerboseLog = v),
            SettingKey.Flag("camera_pan_free", "cameraPanFree",
                () => S.CameraPanFree, v => S.CameraPanFree = v),
            SettingKey.Number("edge_partial_threshold", "edgePartialThresholdPct",
                () => S.EdgePartialThresholdPct, v => S.EdgePartialThresholdPct = (int)v),
            SettingKey.Number("mouse_sensitivity", "mouseSensitivity",
                () => S.MouseSensitivity, v => S.MouseSensitivity = v),
            SettingKey.Number("wasd_speed", "wasdSpeed",
                () => S.WasdSpeed, v => S.WasdSpeed = v),
            SettingKey.Number("arrow_speed", "arrowSpeed",
                () => S.ArrowSpeed, v => S.ArrowSpeed = v),
        };

        public static SettingKey? Find(string wire)
        {
            foreach (var key in All)
                if (string.Equals(key.Wire, wire, StringComparison.Ordinal)) return key;
            return null;
        }
    }
}
