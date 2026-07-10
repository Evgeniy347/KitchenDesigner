using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    /// <summary>Индикатор автосохранения в левом нижнем углу. Показывает состояние
    /// (вкл/выкл) и подсвечивается «💾 Сохранено ЧЧ:ММ:СС» в момент автосохранения.</summary>
    public class AutoSaveIndicator : MonoBehaviour
    {
        public static AutoSaveIndicator Instance { get; private set; }

        private Text _label;
        private float _flashUntil;

        private static readonly Color IdleOn = new Color(0.55f, 0.58f, 0.62f, 1f);
        private static readonly Color IdleOff = new Color(0.45f, 0.45f, 0.48f, 1f);
        private static readonly Color Saved = new Color(0.45f, 0.85f, 0.45f, 1f);

        private void Awake() => Instance = this;

        public void Build(Transform canvas)
        {
            _label = UIFactory.CreateLabel("AutoSaveIndicator", canvas, "Автосохранение", 15,
                new Vector2(14, 12), new Vector2(260, 24), TextAnchor.MiddleLeft);
            UIFactory.AnchorBottomLeft(_label.rectTransform);
            _label.rectTransform.anchoredPosition = new Vector2(14, 12);
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
