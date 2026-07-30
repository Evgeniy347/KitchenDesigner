using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    /// <summary>Единая статус-полоса в левом нижнем углу. Показывает временные
    /// сообщения вызывающих в порядке очереди (FIFO), либо «Автосохранение
    /// выкл» когда автосохранение отключено. Ничего не знает про ошибки, snap
    /// или сохранение — только текст, цвет и время показа.</summary>
    public class StatusBarUI : MonoBehaviour
    {
        public static StatusBarUI? Instance { get; private set; }

        // ── Состояние (отделено от UI, чтобы тесты не зависели от Canvas) ──

        private struct Message
        {
            public string Text;
            public Color Color;
            public float ExpiresAt;
        }

        private Message? _active;
        private readonly Queue<Message> _queue = new Queue<Message>();

        /// <summary>Минимальное время показа одного сообщения. Меньше — слишком
        /// быстро мигает и мерцает между «пусто» и «следующее». Все вызовы
        /// ShowTransient с seconds &lt; MinSeconds клампятся к этому значению.</summary>
        public const float MinSeconds = 3f;

        /// <summary>Дефолтное время, если вызывающий не указал явно. Чуть больше
        /// минимума, чтобы строго &gt; 3 секунды по умолчанию.</summary>
        public const float DefaultSeconds = 4f;

        // Абстракция времени — тесты подменяют провайдер и мотают время сами.
        private static Func<float> _nowProvider = () => Time.unscaledTime;
        public static void SetTimeProvider(Func<float> provider) => _nowProvider = provider;
        public static void ResetTimeProvider() => _nowProvider = () => Time.unscaledTime;

        // ── UI ──

        private RectTransform? _chip;
        private TMP_Text? _label;

        private const float DefaultW = 230f;
        private const float MaxW = 900f;
        private const float ChipH = 26f;
        /// <summary>Левый отступ текста + такой же запас справа.</summary>
        private const float PadX = 20f;

        private static readonly Color AutoSaveOff = new Color(0.85f, 0.25f, 0.25f, 1f);

        // ── Тестовые геттеры ──

        public bool HasActive => _active.HasValue;
        public int QueuedCount => _queue.Count;
        public string? ActiveText => _active?.Text;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            // Без этого статик навсегда держит ссылку на уничтоженный объект —
            // после смены сцены или загрузки другого проекта первый же ShowTransient
            // падает с MissingReferenceException. ReferenceEquals, а не ==:
            // новый экземпляр не должен обнулять себя, когда умирает старый.
            if (ReferenceEquals(Instance, this)) Instance = null;
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

        // ── API ──

        /// <summary>Показать текст на seconds секунд. Меньше <see cref="MinSeconds"/>
        /// клампится. seconds == float.PositiveInfinity — постоянно до следующего
        /// ShowTransient. text == "" — немедленно очистить active+queue.
        /// Повторный вызов с тем же text+color — только продлевает таймер.
        /// Если уже есть активное сообщение — новое встаёт в очередь FIFO и
        /// показывается, когда active истечёт.</summary>
        public void ShowTransient(string text, Color color, float seconds = DefaultSeconds)
        {
            if (string.IsNullOrEmpty(text))
            {
                _active = null;
                _queue.Clear();
                Render();
                return;
            }

            if (seconds < MinSeconds && !float.IsPositiveInfinity(seconds)) seconds = MinSeconds;

            var msg = new Message
            {
                Text = text,
                Color = color,
                ExpiresAt = float.IsPositiveInfinity(seconds)
                    ? float.PositiveInfinity
                    : _nowProvider() + seconds,
            };

            if (_active.HasValue)
            {
                // Тот же текст активного — продлеваем, не дублируем в очередь.
                if (_active.Value.Text == text && _active.Value.Color == color)
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

        /// <summary>Один шаг логики (active → следующий из очереди по таймауту).
        /// В рантайме это вызывает Update(), в тестах — руками после перемотки
        /// времени через SetTimeProvider.</summary>
        public void Tick()
        {
            if (_active.HasValue && _nowProvider() >= _active.Value.ExpiresAt)
            {
                _active = _queue.Count > 0 ? _queue.Dequeue() : (Message?)null;
            }
            Render();
        }

        // ── Рендер ──

        private void Update() => Tick();

        private void Render()
        {
            if (_chip == null || _label == null) return;

            if (_active.HasValue)
            {
                ShowChip(true);
                var msg = _active.Value;
                if (_label.text != msg.Text) _label.text = msg.Text;
                if (_label.color != msg.Color) _label.color = msg.Color;
                FitChip(msg.Text);
                return;
            }

            var s = KitchenSettings.Instance;
            if (s == null || s.AutoSave)
            {
                // Нет активного сообщения и автосохранение ВКЛ — спрятать плашку.
                ShowChip(false);
                return;
            }

            // Автосохранение ВЫКЛ — постоянная красная надпись.
            ShowChip(true);
            const string label = "Автосохранение выкл";
            if (_label.text != label) _label.text = label;
            _label.color = AutoSaveOff;
            FitChip(label);
        }

        private void ShowChip(bool visible)
        {
            if (_chip == null) return;
            if (_chip.gameObject.activeSelf != visible) _chip.gameObject.SetActive(visible);
        }

        private void FitChip(string text)
        {
            if (_chip == null || _label == null) return;
            float w = Mathf.Clamp(_label.GetPreferredValues(text).x + PadX, DefaultW, MaxW);
            // sizeDelta.x для центрированной панели меняется в обе стороны, поэтому
            // обновляем только при реальном изменении — иначе layout дёргается каждый кадр.
            if (Mathf.Abs(_chip.sizeDelta.x - w) > 0.5f) _chip.sizeDelta = new Vector2(w, ChipH);
        }
    }
}