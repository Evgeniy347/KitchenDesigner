using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    /// <summary>Индикатор автосохранения в левом нижнем углу. Показывает состояние
    /// (вкл/выкл), подсвечивается «Сохранено ЧЧ:ММ:СС» в момент автосохранения и
    /// на несколько секунд отдаётся под сообщение (например, описание проблемы
    /// сцены — см. UIManager.GotoFirstIssue). Ширина плашки — по длине текста.</summary>
    public class AutoSaveIndicator : MonoBehaviour
    {
        public static AutoSaveIndicator? Instance { get; private set; }

        private RectTransform? _chip;
        private TMP_Text? _label;
        private float _flashUntil;
        private bool _resized;

        private const float DefaultW = 230f;
        private const float MaxW = 900f;
        private const float ChipH = 26f;
        /// <summary>Левый отступ текста + такой же запас справа.</summary>
        private const float PadX = 20f;

        private static readonly Color IdleOn = new Color(0.55f, 0.58f, 0.62f, 1f);
        private static readonly Color IdleOff = new Color(0.85f, 0.25f, 0.25f, 1f);
        private static readonly Color Saved = new Color(0.45f, 0.85f, 0.45f, 1f);

        private void Awake() => Instance = this;

        public void Build(Transform canvas)
        {
            // Плашка статус-бара, а не «голый» текст поверх вьюпорта.
            var chip = UIFactory.CreatePanel("AutoSaveIndicator", canvas,
                Vector2.zero, new Vector2(DefaultW, ChipH), UIStyle.Panel);
            UIFactory.AnchorBottomLeft(chip.rectTransform);
            chip.rectTransform.anchoredPosition = new Vector2(8, 8);
            chip.raycastTarget = false;
            _chip = chip.rectTransform;

            _label = UIFactory.CreateLabel("AutoSaveLabel", chip.transform, "Автосохранение", 14,
                Vector2.zero, new Vector2(DefaultW, ChipH), TextAnchor.MiddleLeft);
            _label.raycastTarget = false;
            // Сообщение не переносим и не даём растянуть плашку сверх MaxW —
            // хвост длинного текста уходит в многоточие.
            _label.enableWordWrapping = false;
            _label.overflowMode = TextOverflowModes.Ellipsis;
            var lRt = _label.rectTransform;
            lRt.anchorMin = Vector2.zero; lRt.anchorMax = Vector2.one;
            lRt.offsetMin = new Vector2(10, 0); lRt.offsetMax = Vector2.zero;
        }

        /// <summary>Вызывается AutoSaveManager при успешном автосохранении.</summary>
        public void NotifySaved()
        {
            _flashUntil = Time.unscaledTime + 2.5f;
            Show("Сохранено " + System.DateTime.Now.ToString("HH:mm:ss"), Saved);
        }

        /// <summary>Показать произвольное сообщение вместо статуса автосохранения.
        /// По истечении времени плашка сама возвращается к обычному тексту.</summary>
        public void ShowMessage(string text, Color color, float seconds = 3f)
        {
            _flashUntil = Time.unscaledTime + seconds;
            Show(text, color);
        }

        private void Update()
        {
            if (_label == null) return;
            if (Time.unscaledTime < _flashUntil) return; // показываем сообщение

            var s = KitchenSettings.Instance;
            bool on = s != null && s.AutoSave;
            string text = on ? "Автосохранение вкл" : "Автосохранение выкл";
            // Ширину возвращаем один раз — не трогаем sizeDelta каждый кадр.
            if (_resized) { Show(text, on ? IdleOn : IdleOff); return; }
            _label.text = text;
            _label.color = on ? IdleOn : IdleOff;
        }

        private void Show(string text, Color color)
        {
            if (_label == null) return;
            _label.text = text;
            _label.color = color;

            if (_chip == null) return;
            float w = Mathf.Clamp(_label.GetPreferredValues(text).x + PadX, DefaultW, MaxW);
            _chip.sizeDelta = new Vector2(w, ChipH);
            _resized = w > DefaultW;
        }
    }
}
