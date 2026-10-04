using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class VectorField
    {
        public const string PrefixNode = "Axis";
        public const string RotateNode = "Rotate90";
        public static readonly string[] AxisNames = { "X", "Y", "Z" };

        private readonly List<TMP_InputField> _fields = new();
        private readonly List<Button> _rotateButtons = new();

        private VectorField(RectTransform root) => Root = root;

        public RectTransform Root { get; }

        public IReadOnlyList<TMP_InputField> Fields => _fields;

        public IReadOnlyList<Button> RotateButtons => _rotateButtons;

        public static VectorField Create(string name, Transform parent, float width, float height,
            IReadOnlyList<string>? rotateTooltips = null, Action<int>? onRotate90 = null)
        {
            var root = UIFactory.CreateRect(name, parent);
            root.sizeDelta = new Vector2(width, height);
            var self = new VectorField(root);
            float fieldW = (width - UIStyle.Space1 * (AxisNames.Length - 1)) / AxisNames.Length;
            for (int axis = 0; axis < AxisNames.Length; axis++)
            {
                var field = UIFactory.CreateInputField(name + "_" + AxisNames[axis], root, "", Vector2.zero,
                    new Vector2(fieldW, height));
                var rt = (RectTransform)field.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(axis * (fieldW + UIStyle.Space1), 0f);
                field.textComponent!.alignment = TextAlignmentOptions.Right;

                var prefix = UIFactory.CreateLabel(PrefixNode, rt, AxisNames[axis], UIStyle.FontCaption, Vector2.zero,
                    new Vector2(UIStyle.Space3, height), TextAnchor.MiddleLeft);
                prefix.color = UIStyle.TextSecondary;
                prefix.raycastTarget = false;
                var prt = prefix.rectTransform;
                prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0f, 0.5f);
                prt.anchoredPosition = new Vector2(UIStyle.Space2, 0f);

                float left = UIStyle.Space2 + UIStyle.Space3 + UIStyle.Space1;
                float right = UIStyle.Space2;
                if (onRotate90 != null)
                {
                    int captured = axis;
                    var rotate = UIFactory.CreateIconButton(RotateNode, rt, IconFactory.Redo, Vector2.zero,
                        new Vector2(height - UIStyle.Space1, height - UIStyle.Space1), () => onRotate90(captured),
                        height - UIStyle.Space1 - UIStyle.IconSizeSmall);
                    QuietButton.Apply(rotate);
                    var rrt = (RectTransform)rotate.transform;
                    rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(1f, 0.5f);
                    rrt.anchoredPosition = new Vector2(-UIStyle.SegmentPad, 0f);
                    if (rotateTooltips != null && captured < rotateTooltips.Count)
                        TooltipUI.Attach(rotate.gameObject, rotateTooltips[captured]);
                    right = rrt.sizeDelta.x + UIStyle.Space1;
                    self._rotateButtons.Add(rotate);
                }

                var viewport = field.textViewport;
                if (viewport != null)
                {
                    viewport.offsetMin = new Vector2(left, viewport.offsetMin.y);
                    viewport.offsetMax = new Vector2(-right, viewport.offsetMax.y);
                }
                field.contentType = TMP_InputField.ContentType.Custom;
                field.onValidateInput = DimensionFieldValidation.Char(allowDecimal: true);
                self._fields.Add(field);
            }
            return self;
        }
    }
}
