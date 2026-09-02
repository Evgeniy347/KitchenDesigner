using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using static KitchenDesigner.Core.UI.ContextMenuMetrics;

namespace KitchenDesigner.Core.UI
{
    internal sealed class WallDeviceFieldsEditor : NumberFieldsEditor
    {
        public const string PlateWidthNode = "ШиринаРамки";
        public const string PlateHeightNode = "ВысотаРамки";
        public const string ProtrusionNode = "ВыносОтСтены";
        public const string PostCountNode = "CtxWallDevicePosts";
        public const string PoweredNode = "CtxWallSwitchOn";

        public const string PlateWidthLabel = "Ширина рамки";
        public const string PlateHeightLabel = "Высота рамки";
        public const string ProtrusionLabel = "Вынос от стены";
        public const string PostCountLabel = "Постов";
        public const string PoweredLabel = "Включён";

        private TMP_Dropdown? _posts;
        private Toggle? _powered;

        public WallDeviceFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is IWallDevice;

        public override DimensionPolicy Dimensions => DimensionPolicy.Computed;

        public override bool WidthEditable => false;

        public override bool HeightEditable => false;

        public override bool DepthEditable => false;

        public override void Build()
        {
            var anyDevice = RowVisibility.When(() => Host.Target is IWallDevice);
            var switchOnly = RowVisibility.When(() => Host.Target is LightSwitchElement);

            var widthRow = Rows.NumberField(PlateWidthLabel, anyDevice, "мм", PlateWidthNode);
            var heightRow = Rows.NumberField(PlateHeightLabel, anyDevice, "мм", PlateHeightNode);
            var protrusionRow = Rows.NumberField(ProtrusionLabel, anyDevice, "мм", ProtrusionNode);

            Bind(widthRow, PlateWidthOf, SetPlateWidth,
                WallDeviceLayout.DefaultPlateWidthMM.ToString());
            Bind(heightRow, PlateHeightOf, SetPlateHeight,
                WallDeviceLayout.DefaultPlateHeightMM.ToString());
            Bind(protrusionRow, ProtrusionOf, SetProtrusion,
                WallDeviceLayout.DefaultProtrusionMM.ToString());

            _posts = Rows.Dropdown(PostCountLabel, PostOptions(), OnPostCountSelected,
                anyDevice, PostCountNode);
            _powered = Rows.Toggle(PoweredNode, PoweredLabel, true, OnPoweredToggled,
                switchOnly, RowGap);
        }

        public override void Show(KitchenElement element)
        {
            base.Show(element);
            WriteWidgets(element);
        }

        public override void Refresh(KitchenElement element)
        {
            base.Refresh(element);
            WriteWidgets(element);
        }

        public override void AfterApply(KitchenElement element)
        {
            base.AfterApply(element);
            WriteWidgets(element);
        }

        private void WriteWidgets(KitchenElement element)
        {
            if (element is IWallDevice device)
            {
                _posts?.SetValueWithoutNotify(
                    WallDeviceLayout.ClampPostCount(device.PostCount) - WallDeviceLayout.MinPostCount);
                _posts?.RefreshShownValue();
            }
            if (element is LightSwitchElement source)
                _powered?.SetIsOnWithoutNotify(source.IsOn);
        }

        private static List<string> PostOptions()
        {
            var options = new List<string>();
            for (int i = WallDeviceLayout.MinPostCount; i <= WallDeviceLayout.MaxPostCount; i++)
                options.Add(i.ToString());
            return options;
        }

        private void OnPostCountSelected(int index)
        {
            if (!(Host.Target is IWallDevice device)) return;
            device.PostCount = index + WallDeviceLayout.MinPostCount;
        }

        private void OnPoweredToggled(bool on)
        {
            if (!(Host.Target is LightSwitchElement source)) return;
            if (source.IsOn == on) return;

            var before = UndoableProperties.Capture(source);
            source.IsOn = on;
            var after = UndoableProperties.Capture(source);
            source.IsOn = !on;

            var command = SetPropertiesCommand.TryCreate(source, before, after);
            if (command == null) source.IsOn = on;
            else CommandStack.Execute(command);
        }

        private static int PlateWidthOf(KitchenElement element)
            => element is IWallDevice device
                ? device.PlateWidthMM
                : WallDeviceLayout.DefaultPlateWidthMM;

        private static void SetPlateWidth(KitchenElement element, int value)
        {
            if (element is IWallDevice device) device.PlateWidthMM = value;
        }

        private static int PlateHeightOf(KitchenElement element)
            => element is IWallDevice device
                ? device.PlateHeightMM
                : WallDeviceLayout.DefaultPlateHeightMM;

        private static void SetPlateHeight(KitchenElement element, int value)
        {
            if (element is IWallDevice device) device.PlateHeightMM = value;
        }

        private static int ProtrusionOf(KitchenElement element)
            => element is IWallDevice device
                ? device.ProtrusionMM
                : WallDeviceLayout.DefaultProtrusionMM;

        private static void SetProtrusion(KitchenElement element, int value)
        {
            if (element is IWallDevice device) device.ProtrusionMM = value;
        }
    }
}
