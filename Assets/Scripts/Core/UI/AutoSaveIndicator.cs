using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    /// <summary>Индикатор автосохранения в левом нижнем углу. Показывает состояние
    /// (вкл/выкл) и подсвечивается «💾 Сохранено ЧЧ:ММ:СС» в момент автосохранения.</summary>
    public class AutoSaveIndicator : MonoBehaviour
    {
        public static AutoSaveIndicator? Instance { get; private set; }

        private TMP_Text? _label;
        private float _flashUntil;

        private static readonly Color IdleOn = new Color(0.55f, 0.58f, 0.62f, 1f);
        private static readonly Color IdleOff = new Color(0.85f, 0.25f, 0.25f, 1f);
        private static readonly Color Saved = new Color(0.45f, 0.85f, 0.45f, 1f);

        private void Awake() => Instance = this;

        public void Build(Transform canvas)
        {
            // Плашка статус-бара, а не «голый» текст поверх вьюпорта.
            var chip = UIFactory.CreatePanel("AutoSaveIndicator", canvas,
                Vector2.zero, new Vector2(230, 26), UIStyle.Panel);
            UIFactory.AnchorBottomLeft(chip.rectTransform);
            chip.rectTransform.anchoredPosition = new Vector2(8, 8);
            chip.raycastTarget = false;

            _label = UIFactory.CreateLabel("AutoSaveLabel", chip.transform, "Автосохранение", 14,
                Vector2.zero, new Vector2(230, 26), TextAnchor.MiddleLeft);
            _label.raycastTarget = false;
            var lRt = _label.rectTransform;
            lRt.anchorMin = Vector2.zero; lRt.anchorMax = Vector2.one;
            lRt.offsetMin = new Vector2(10, 0); lRt.offsetMax = Vector2.zero;
        }

        /// <summary>Вызывается AutoSaveManager при успешном автосохранении.</summary>
        public void NotifySaved()
        {
            _flashUntil = Time.unscaledTime + 2.5f;
            if (_label != null)
            {
                _label.text = "Сохранено " + System.DateTime.Now.ToString("HH:mm:ss");
                _label.color = Saved;
            }
        }

        private void Update()
        {
            if (_label == null) return;
            if (Time.unscaledTime < _flashUntil) return; // показываем «Сохранено»

            var s = KitchenSettings.Instance;
            bool on = s != null && s.AutoSave;
            _label.text = on ? "Автосохранение вкл" : "Автосохранение выкл";
            _label.color = on ? IdleOn : IdleOff;
        }
    }
}
