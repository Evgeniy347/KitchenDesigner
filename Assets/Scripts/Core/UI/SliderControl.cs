using System;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public static class SliderControl
    {
        public const string TrackNode = "Track";
        public const string FillNode = "Fill";
        public const string ThumbNode = "Thumb";

        public static Slider Create(string name, Transform parent, float min, float max, float value,
            float width, float height, Action<float>? onChanged)
        {
            var rect = UIFactory.CreateRect(name, parent);
            rect.sizeDelta = new Vector2(width, height);
            var slider = rect.gameObject.AddComponent<Slider>();

            float half = UIStyle.SliderThumb * 0.5f;
            var track = Bar(TrackNode, rect, UIStyle.Separator);
            track.offsetMin = new Vector2(half, -UIStyle.SliderTrackH * 0.5f);
            track.offsetMax = new Vector2(-half, UIStyle.SliderTrackH * 0.5f);

            var fillArea = UIFactory.CreateRect(name + "_FillArea", rect);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.offsetMin = new Vector2(half, -UIStyle.SliderTrackH * 0.5f);
            fillArea.offsetMax = new Vector2(-half, UIStyle.SliderTrackH * 0.5f);
            var fill = Bar(FillNode, fillArea, UIStyle.Accent);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;

            var handleArea = UIFactory.CreateRect(name + "_HandleArea", rect);
            handleArea.anchorMin = new Vector2(0f, 0.5f);
            handleArea.anchorMax = new Vector2(1f, 0.5f);
            handleArea.offsetMin = new Vector2(half, 0f);
            handleArea.offsetMax = new Vector2(-half, 0f);
            var thumb = UIFactory.CreateRect(ThumbNode, handleArea);
            var thumbImg = thumb.gameObject.AddComponent<Image>();
            RoundedRectSprites.Apply(thumbImg, RoundedRectSprites.Fill(half));
            thumbImg.color = UIStyle.Text;
            thumb.sizeDelta = new Vector2(UIStyle.SliderThumb, UIStyle.SliderThumb);

            slider.fillRect = fill;
            slider.handleRect = thumb;
            slider.targetGraphic = thumbImg;
            slider.colors = UIFactory.InteractiveColors();
            slider.direction = LayoutDirection.IsRtl ? Slider.Direction.RightToLeft : Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            if (onChanged != null) slider.onValueChanged.AddListener(v => onChanged(v));
            return slider;
        }

        private static RectTransform Bar(string name, RectTransform parent, Color color)
        {
            var bar = UIFactory.CreateRect(name, parent);
            bar.anchorMin = new Vector2(0f, 0.5f);
            bar.anchorMax = new Vector2(1f, 0.5f);
            var img = bar.gameObject.AddComponent<Image>();
            RoundedRectSprites.Apply(img, RoundedRectSprites.Fill(UIStyle.SliderTrackH * 0.5f));
            img.color = color;
            img.raycastTarget = false;
            return bar;
        }
    }
}
