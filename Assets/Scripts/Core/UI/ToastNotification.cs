using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public class ToastNotification : MonoBehaviour
    {
        public static ToastNotification? Instance { get; private set; }

        private CanvasGroup? _group;
        private TMP_Text? _label;
        private Button? _actionButton;
        private TMP_Text? _actionLabel;
        private System.Action? _action;
        private Coroutine? _activeRoutine;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            // Без этого статик навсегда держит ссылку на уничтоженный объект —
            // после смены сцены или загрузки другого проекта первый же тост
            // падает с MissingReferenceException. ReferenceEquals, а не ==:
            // новый экземпляр не должен обнулять себя, когда умирает старый.
            if (ReferenceEquals(Instance, this)) Instance = null;
        }

        /// <summary>Единственный безопасный способ показать тост из чужого кода.
        /// Писать `Instance?.Show(...)` нельзя: Unity перегружает `==` так, что
        /// уничтоженный объект равен null, но `?.` эту перегрузку обходит и
        /// проверяет настоящую C#-ссылку — она не нулевая, и вызов летит на
        /// мёртвый объект.</summary>
        public static void ShowIfAvailable(string message, float duration = 2f,
            string? actionLabel = null, System.Action? action = null)
        {
            var toast = Instance;
            if (toast == null) return; // перегруженный Unity `==` ловит и уничтоженный объект
            toast.Show(message, duration, actionLabel, action);
        }

        public void Build(Transform canvasTransform)
        {
            var panel = UIFactory.CreatePanel("ToastPanel", canvasTransform,
                Vector2.zero, new Vector2(360, 48));
            UIFactory.AnchorCenter(panel.rectTransform);
            panel.rectTransform.anchoredPosition = new Vector2(0, -250);

            _group = panel.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            _label = UIFactory.CreateLabel("ToastLabel", panel.transform, "", 18,
                Vector2.zero, new Vector2(340, 44), TextAnchor.MiddleCenter);
            _label.rectTransform.anchorMin = Vector2.zero;
            _label.rectTransform.anchorMax = Vector2.one;
            _label.rectTransform.offsetMin = Vector2.zero;
            _label.rectTransform.offsetMax = Vector2.zero;

            // Кнопка действия («Отменить» после удаления): прижата к правому
            // краю, видна только когда у тоста есть действие.
            _actionButton = UIFactory.CreateButton("ToastAction", panel.transform, "",
                Vector2.zero, new Vector2(110, 36), () =>
                {
                    var a = _action;
                    Hide();
                    a?.Invoke();
                });
            var abRt = _actionButton.GetComponent<RectTransform>();
            abRt.anchorMin = abRt.anchorMax = abRt.pivot = new Vector2(1, 0.5f);
            abRt.anchoredPosition = new Vector2(-6, 0);
            _actionLabel = _actionButton.GetComponentInChildren<TMP_Text>();
            _actionButton.gameObject.SetActive(false);

            panel.gameObject.SetActive(false);
        }

        public void Show(string message, float duration = 2f,
            string? actionLabel = null, System.Action? action = null)
        {
            if (_activeRoutine != null)
                StopCoroutine(_activeRoutine);

            _action = action;
            bool hasAction = action != null && !string.IsNullOrEmpty(actionLabel);
            if (_actionButton != null) _actionButton.gameObject.SetActive(hasAction);
            if (_actionLabel != null) _actionLabel.text = actionLabel ?? "";
            if (_label != null)
            {
                // С кнопкой текст уступает ей правую часть панели.
                _label.rectTransform.offsetMax = new Vector2(hasAction ? -120f : 0f, 0f);
                _label.alignment = hasAction ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
                _label.margin = hasAction ? new Vector4(12, 0, 0, 0) : Vector4.zero;
            }

            _activeRoutine = StartCoroutine(ShowRoutine(message, duration));
        }

        private void Hide()
        {
            if (_activeRoutine != null)
            {
                StopCoroutine(_activeRoutine);
                _activeRoutine = null;
            }
            _action = null;
            if (_group != null)
            {
                _group.alpha = 0f;
                _group.gameObject.SetActive(false);
            }
        }

        private IEnumerator ShowRoutine(string message, float duration)
        {
            _label!.text = message;
            _group!.alpha = 0f;
            _group!.gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < 0.15f)
            {
                elapsed += Time.deltaTime;
                _group!.alpha = Mathf.Lerp(0f, 1f, elapsed / 0.15f);
                yield return null;
            }
            _group!.alpha = 1f;

            yield return new WaitForSeconds(duration);

            elapsed = 0f;
            while (elapsed < 0.3f)
            {
                elapsed += Time.deltaTime;
                _group!.alpha = Mathf.Lerp(1f, 0f, elapsed / 0.3f);
                yield return null;
            }
            _group!.alpha = 0f;
            _group!.gameObject.SetActive(false);
            _activeRoutine = null;
        }
    }
}
