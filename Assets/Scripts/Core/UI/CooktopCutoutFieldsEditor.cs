using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    internal sealed class CooktopCutoutFieldsEditor : ElementFieldsEditor
    {
        private readonly DimensionFields _style = new();

        private TMP_InputField? _width, _depth;

        public CooktopCutoutFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is CooktopElement;

        public override void Build()
        {
            var cooktopOnly = RowVisibility.When(() => Host.Target is CooktopElement);
            _width = Rows.NumberField("Ширина выреза", cooktopOnly);
            _depth = Rows.NumberField("Глубина выреза", cooktopOnly);
        }

        public override IEnumerable<TMP_InputField?> ArithmeticFields()
        {
            yield return _width;
            yield return _depth;
        }

        public override void Show(KitchenElement element)
        {
            if (_width == null || _depth == null) return;
            var cooktop = element as CooktopElement;
            _width.text = (cooktop != null
                ? cooktop.CutoutWidthMM : CooktopElement.DEFAULT_CUTOUT_WIDTH_MM).ToString();
            _depth.text = (cooktop != null
                ? cooktop.CutoutDepthMM : CooktopElement.DEFAULT_CUTOUT_DEPTH_MM).ToString();

            bool editable = !FixedSize.IsFixed(element);
            _style.SetEditable(_width, editable);
            _style.SetEditable(_depth, editable);
        }

        public override void Refresh(KitchenElement element)
        {
            if (!(element is CooktopElement cooktop)) return;
            Fields.RefreshUnfocused(_width, cooktop.CutoutWidthMM.ToString());
            Fields.RefreshUnfocused(_depth, cooktop.CutoutDepthMM.ToString());
        }

        public override void Apply(KitchenElement element)
        {
            if (!(element is CooktopElement cooktop) || _width == null || _depth == null) return;

            var before = SetCooktopCutoutCommand.Snapshot(cooktop);
            var after = new Vector2Int(
                Fields.ParseInt(_width, before.x),
                Fields.ParseInt(_depth, before.y));
            if (after != before)
                CommandStack.Execute(new SetCooktopCutoutCommand(cooktop, before, after));
        }

        public override void Track(KitchenElement element)
        {
            var cooktop = element as CooktopElement;
            Fields.Track(_width, (cooktop != null
                ? cooktop.CutoutWidthMM : CooktopElement.DEFAULT_CUTOUT_WIDTH_MM).ToString());
            Fields.Track(_depth, (cooktop != null
                ? cooktop.CutoutDepthMM : CooktopElement.DEFAULT_CUTOUT_DEPTH_MM).ToString());
        }

        public override void AfterApply(KitchenElement element)
        {
            if (!(element is CooktopElement cooktop) || _width == null || _depth == null) return;
            _width.text = cooktop.CutoutWidthMM.ToString();
            _depth.text = cooktop.CutoutDepthMM.ToString();
        }
    }
}
