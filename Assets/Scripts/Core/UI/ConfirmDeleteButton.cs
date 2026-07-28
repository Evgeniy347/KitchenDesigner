using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    /// Взвод снимает сторож в LateUpdate: к этому моменту EventSystem уже разослал
    /// события кадра. Сторож смотрит на НАЖАТИЕ мыши, а <see cref="Button"/>
    /// присылает onClick только на ОТПУСКАНИИ — то есть на кадр-другой позже.
    /// Поэтому одного «кадр взвода пропускаем» мало: подтверждающее нажатие тоже
    /// придёт отдельным кадром и погасило бы взвод раньше, чем сработал бы клик
    /// (кнопка молча взводилась бы снова и снова, ничего не удаляя). Отсюда
    /// <see cref="IPointerDownHandler"/>: нажатие ПО САМОЙ кнопке сторож
    /// пропускает, любое другое — гасит.</summary>
    [RequireComponent(typeof(Button))]
    public class ConfirmDeleteButton : MonoBehaviour, IPointerDownHandler
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

        /// <summary>Кадр, в котором нажали по самой кнопке (ставит EventSystem).</summary>
        private int _pointerDownFrame = -1;

        public void OnPointerDown(PointerEventData eventData) => _pointerDownFrame = Time.frameCount;

        /// <summary>Пора ли снимать взвод. Вынесено отдельно и чисто, потому что
        /// вся суть бага «второй клик не удаляет» — в сравнении кадров, а
        /// смоделировать мышь в EditMode-тесте нельзя.</summary>
        public static bool ShouldDisarm(int frame, int armedFrame, int pointerDownFrame,
            bool mouseDown, bool cancelled) =>
            frame != armedFrame
            && (cancelled || (mouseDown && frame != pointerDownFrame));

        private void LateUpdate()
        {
            if (!Armed) return;
            bool cancelled = Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape);
            if (ShouldDisarm(Time.frameCount, _armedFrame, _pointerDownFrame,
                    Input.GetMouseButtonDown(0), cancelled))
                Disarm();
        }

        private void OnDisable() => Disarm();
    }
}
