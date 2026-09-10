using System;
using System.Collections.Generic;
using KitchenDesigner.Core.Update;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public class StatusBarUI : MonoBehaviour
    {
        private static StatusBarUI? _instance;

        public static StatusBarUI? Instance => _instance != null ? _instance : null;

        private struct Message
        {
            public string Text;
            public StatusLevel Level;
            public float ExpiresAt;
        }

        private Message? _active;
        private readonly Queue<Message> _queue = new Queue<Message>();

        public const float MinSeconds = 3f;
        public const float DefaultSeconds = 4f;

        private static Func<float> _nowProvider = () => Time.unscaledTime;
        public static void SetTimeProvider(Func<float> provider) => _nowProvider = provider;
        public static void ResetTimeProvider() => _nowProvider = () => Time.unscaledTime;

        private RectTransform? _chip;
        private TMP_Text? _label;

        private const float DefaultW = 230f;
        private const float MaxW = 900f;
        private const float ChipH = 26f;
        private const float TextPadBothSides = 20f;
        internal const float NoticeableWidthChange = 0.5f;

        private static readonly Color AutoSaveOff = new Color(0.85f, 0.25f, 0.25f, 1f);

        public bool HasActive => _active.HasValue;
        public int QueuedCount => _queue.Count;
        public string? ActiveText => _active?.Text;

        private void Awake() => _instance = this;

        private void OnDestroy()
        {
            if (ReferenceEquals(_instance, this)) _instance = null;
        }

        internal void SimulateAwakeForTests() => Awake();

        internal static StatusBarUI? SwapInstanceForTests(StatusBarUI? next)
        {
            var previous = _instance;
            _instance = next;
            return previous;
        }

        public void Build(Transform canvas)
        {
            var chip = UIFactory.CreatePanel("StatusBar", canvas,
                Vector2.zero, new Vector2(DefaultW, ChipH), UIStyle.Panel);
            UIFactory.AnchorBottomLeft(chip.rectTransform);
            chip.rectTransform.anchoredPosition = new Vector2(8, 8);
            chip.raycastTarget = false;
            _chip = chip.rectTransform;

            _label = UIFactory.CreateLabel("StatusBarLabel", chip.transform, "", 14,
                Vector2.zero, new Vector2(DefaultW, ChipH), TextAnchor.MiddleLeft);
            _label.raycastTarget = false;
            _label.enableWordWrapping = false;
            _label.overflowMode = TextOverflowModes.Ellipsis;
            var lRt = _label.rectTransform;
            lRt.anchorMin = Vector2.zero; lRt.anchorMax = Vector2.one;
            lRt.offsetMin = new Vector2(10, 0); lRt.offsetMax = Vector2.zero;
        }

        public static Color ColorFor(StatusLevel level) => level switch
        {
            StatusLevel.Success => UIStyle.HighlightOk,
            StatusLevel.Warning => UIStyle.HighlightWarning,
            StatusLevel.Error => UIStyle.HighlightError,
            _ => UIStyle.TextSecondary,
        };

        internal static ConsoleLineKind ConsoleKindOf(StatusLevel level) => level switch
        {
            StatusLevel.Success => ConsoleLineKind.StatusSuccess,
            StatusLevel.Warning => ConsoleLineKind.StatusWarning,
            StatusLevel.Error => ConsoleLineKind.StatusError,
            _ => ConsoleLineKind.Status,
        };

        public void ShowTransient(string text, StatusLevel level,
            float seconds = DefaultSeconds)
        {
            if (string.IsNullOrEmpty(text))
            {
                _active = null;
                _queue.Clear();
                Render();
                return;
            }

            ConsoleLog.Shared.Append(ConsoleKindOf(level), text, DateTime.Now);

            if (seconds < MinSeconds && !float.IsPositiveInfinity(seconds)) seconds = MinSeconds;

            var msg = new Message
            {
                Text = text,
                Level = level,
                ExpiresAt = float.IsPositiveInfinity(seconds)
                    ? float.PositiveInfinity
                    : _nowProvider() + seconds,
            };

            if (_active.HasValue)
            {
                if (SameAsActive(msg))
                {
                    _active = msg;
                    return;
                }
                _queue.Enqueue(msg);
                return;
            }

            _active = msg;
            Render();
        }

        private bool SameAsActive(Message msg) =>
            _active!.Value.Text == msg.Text && _active!.Value.Level == msg.Level;

        public void Tick()
        {
            if (_active.HasValue && _nowProvider() >= _active.Value.ExpiresAt)
            {
                _active = _queue.Count > 0 ? _queue.Dequeue() : (Message?)null;
            }
            Render();
        }

        private void Update() => Tick();

        private void Render()
        {
            if (_chip == null || _label == null) return;

            if (_active.HasValue)
            {
                ShowChip(true);
                var msg = _active.Value;
                var color = ColorFor(msg.Level);
                if (_label.text != msg.Text) _label.text = msg.Text;
                if (_label.color != color) _label.color = color;
                FitChip(msg.Text);
                return;
            }

            var s = KitchenSettings.Instance;
            if (s == null || s.AutoSave)
            {
                ShowChip(false);
                return;
            }

            ShowTheStandingAutoSaveOffWarning();
        }

        private void ShowTheStandingAutoSaveOffWarning()
        {
            ShowChip(true);
            const string label = "Автосохранение выкл";
            if (_label!.text != label) _label!.text = label;
            _label!.color = AutoSaveOff;
            FitChip(label);
        }

        private void ShowChip(bool visible)
        {
            if (_chip == null) return;
            if (_chip.gameObject.activeSelf != visible) _chip.gameObject.SetActive(visible);
        }

        internal static bool WidthChangedNoticeably(float current, float wanted) =>
            Mathf.Abs(current - wanted) > NoticeableWidthChange;

        private void FitChip(string text)
        {
            if (_chip == null || _label == null) return;
            float w = Mathf.Clamp(_label.GetPreferredValues(text).x + TextPadBothSides,
                DefaultW, MaxW);
            if (WidthChangedNoticeably(_chip.sizeDelta.x, w)) _chip.sizeDelta = new Vector2(w, ChipH);
        }
    }
}
