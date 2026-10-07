using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    internal sealed class ContextMenuSizeSection
    {
        private readonly IContextMenuHost _host;
        private readonly DimensionFields _dimensions = new();

        private ComputedNumberRow? _height;

        public ContextMenuSizeSection(IContextMenuHost host) => _host = host;

        public DimensionFields Dimensions => _dimensions;

        public TMP_InputField? Width => _dimensions.Width;

        public TMP_InputField? Height => _dimensions.Height;

        public TMP_InputField? Depth => _dimensions.Depth;

        public void Build()
        {
            _host.Rows.BeginSection("Dimensions", Loc.T("element.common.dimensions"), true);
            _dimensions.Width = _host.Rows.NumberOrComputed(Loc.T("element.common.width"), RowVisibility.Always).Field;
            _height = _host.Rows.NumberOrComputed(ElementFieldsEditor.DefaultHeightLabel, RowVisibility.Always);
            _dimensions.Height = _height.Field;
            _dimensions.Depth = _host.Rows.NumberOrComputed(Loc.T("element.common.depth"), RowVisibility.Always).Field;
        }

        public void CollectArithmeticFields(List<TMP_InputField?> fields)
        {
            fields.Add(Width);
            fields.Add(Height);
            fields.Add(Depth);
        }

        public void WriteFrom(Vector3Int dimensions)
        {
            Width!.text = dimensions.x.ToString();
            Height!.text = dimensions.y.ToString();
            Depth!.text = dimensions.z.ToString();
        }

        public void RefreshFrom(Vector3Int dimensions)
        {
            _host.Fields.RefreshUnfocused(Width, dimensions.x.ToString());
            _host.Fields.RefreshUnfocused(Height, dimensions.y.ToString());
            _host.Fields.RefreshUnfocused(Depth, dimensions.z.ToString());
        }

        public void Track(Vector3Int dimensions)
        {
            _host.Fields.Track(Width, dimensions.x.ToString());
            _host.Fields.Track(Height, dimensions.y.ToString());
            _host.Fields.Track(Depth, dimensions.z.ToString());
        }

        public void ApplyTo(KitchenElement target, ElementFieldsEditor? editor, Vector3Int oldDimensions)
        {
            var policy = editor?.Dimensions ?? DimensionPolicy.FromFields;
            if (policy == DimensionPolicy.Computed) return;

            target.DimensionsMM = new Vector3Int(
                _host.Fields.ParseInt(Width, oldDimensions.x),
                _host.Fields.ParseInt(Height, oldDimensions.y),
                policy == DimensionPolicy.KeepDepth
                    ? oldDimensions.z
                    : _host.Fields.ParseInt(Depth, oldDimensions.z));
        }

        public void WriteAfterApply(Vector3Int newDimensions, ElementFieldsEditor? editor)
        {
            Width!.text = newDimensions.x.ToString();
            if (editor?.HeightShownFromDimensions ?? true)
                Height!.text = newDimensions.y.ToString();
            Depth!.text = newDimensions.z.ToString();
        }

        public void ShowLocks(KitchenElement element, ElementFieldsEditor? editor)
        {
            bool unlocked = !FixedSize.IsFixed(element);
            _dimensions.SetEditable(Width, unlocked && (editor?.WidthEditable ?? true));
            _dimensions.SetEditable(Height, unlocked && !FixedSize.IsHeightFixed(element) && (editor?.HeightEditable ?? true));
            _dimensions.SetEditable(Depth, unlocked && (editor?.DepthEditable ?? true));
            _height?.SetCaption(editor?.HeightLabel ?? ElementFieldsEditor.DefaultHeightLabel);
        }
    }
}
