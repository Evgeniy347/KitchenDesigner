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
            var switchOnly = RowVisibility.When(() => Host.Target is ILightSwitch);

            var widthRow = Rows.NumberField(PlateWidthLabel, anyDevice, "мм", PlateWidthNode);
            var heightRow = Rows.NumberField(PlateHeightLabel, anyDevice, "мм", PlateHeightNode);
            var protrusionRow = Rows.NumberField(ProtrusionLabel, anyDevice, "мм", ProtrusionNode);

            Bind<IWallDevice>(widthRow, device => device.PlateWidthMM,
                (device, value) => device.PlateWidthMM = value,
                WallDeviceLayout.DefaultPlateWidthMM.ToString());
            Bind<IWallDevice>(heightRow, device => device.PlateHeightMM,
                (device, value) => device.PlateHeightMM = value,
                WallDeviceLayout.DefaultPlateHeightMM.ToString());
            Bind<IWallDevice>(protrusionRow, device => device.ProtrusionMM,
                (device, value) => device.ProtrusionMM = value,
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
            if (element is ILightSwitch source)
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
            if (Host.Target is ILightSwitch source) Lighting.SwitchPower.Set(source, on);
        }
    }
}
