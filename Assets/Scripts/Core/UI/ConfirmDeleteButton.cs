using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    /// <summary>Удаление в два клика (правило 3 UI-GUIDELINES): первый клик
    /// «взводит» кнопку — глиф «×» меняется на <see cref="UIStyle.GlyphConfirm"/>,
    /// второй удаляет. Любой следующий клик мимо этой кнопки снимает взвод, и «?!»
    /// возвращается в «×».
    ///
    /// Почему не модальное окно: строк списка (пазы, накладки текстур) на панели
    /// много, они узкие, и диалог «Вы уверены?» на каждую строку читался бы
    /// дольше, чем само действие. Два клика по одной и той же кнопке — то же
    /// подтверждение, но без смены фокуса.
    ///
    /// Взвод снимается в LateUpdate, а не в Update: к этому моменту EventSystem
    /// уже разослал клик текущего кадра, поэтому подтверждающее нажатие по самой
    /// кнопке успевает сработать и не гасится собственным сторожем. Кадр взвода
    /// пропускается по той же причине — нажатие, которое кнопку и взвело, ещё
    /// висит в <c>Input.GetMouseButtonDown</c>.</summary>
    [RequireComponent(typeof(Button))]
    public class ConfirmDeleteButton : MonoBehaviour
    {
        /// <summary>Взведена всегда не больше одной кнопки: вопрос «удалять?»
        /// должен висеть в одном месте, а не в трёх строках сразу.</summary>
        private static ConfirmDeleteButton? _armedOne;

        private System.Action? _onConfirm;
        private TMP_Text? _label;
        private string _idleText = UIStyle.GlyphClose;
        private int _armedFrame = -1;

        /// <summary>Кнопка взведена — следующий клик по ней удаляет.</summary>
        public bool Armed { get; private set; }

        /// <summary>Повесить подтверждение на готовую кнопку. Собственный
        /// обработчик кнопки при этом НЕ ставится: удаление зовётся отсюда,
        /// только вторым кликом.</summary>
        public static ConfirmDeleteButton Attach(Button button, System.Action onConfirm)
        {
            var confirm = button.gameObject.AddComponent<ConfirmDeleteButton>();
            confirm._onConfirm = onConfirm;
            confirm._label = button.GetComponentInChildren<TMP_Text>();
            if (confirm._label != null) confirm._idleText = confirm._label.text;
            button.onClick.AddListener(confirm.OnClick);
            return confirm;
        }

        private void OnClick()
        {
            if (!Armed) { Arm(); return; }
            Disarm();
            _onConfirm?.Invoke();
        }

        private void Arm()
        {
            if (_armedOne != null && _armedOne != this) _armedOne.Disarm();
            _armedOne = this;
            Armed = true;
            _armedFrame = Time.frameCount;
            if (_label != null) _label.text = UIStyle.GlyphConfirm;
        }

        /// <summary>Снять взвод где бы он ни был. Зовётся, когда набор под
        /// кнопками поменялся мимо неё (undo, MCP, смена элемента): взведённая
        /// кнопка спрашивала про строку, которой уже нет.</summary>
        public static void DisarmAll() => _armedOne?.Disarm();

        /// <summary>Снять взвод и вернуть исходный глиф. Публичный: закрытие
        /// панели и смена элемента обязаны сбрасывать взвод, иначе следующий
        /// показ строки начался бы уже с «?!».</summary>
        public void Disarm()
        {
            if (_armedOne == this) _armedOne = null;
            if (!Armed) return;
            Armed = false;
            _armedFrame = -1;
            if (_label != null) _label.text = _idleText;
        }

        private void LateUpdate()
        {
            if (!Armed || Time.frameCount == _armedFrame) return;
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)
                || Input.GetKeyDown(KeyCode.Escape))
                Disarm();
        }

        private void OnDisable() => Disarm();
    }
}
