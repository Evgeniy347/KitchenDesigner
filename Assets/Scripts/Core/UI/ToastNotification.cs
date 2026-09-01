using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public class ToastNotification : MonoBehaviour
    {
        public static ToastNotification? Instance { get; private set; }

        internal const float LabelWidthGivenUpToTheActionButton = 120f;

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
            if (ReferenceEquals(Instance, this)) Instance = null;
        }

        public static void ShowIfAvailable(string message, float duration = 2f,
            string? actionLabel = null, System.Action? action = null)
        {
            var toast = Instance;
            if (toast == null) return;
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

            _actionButton = UIFactory.CreateButton("ToastAction", panel.transform, "",
                Vector2.zero, new Vector2(110, 36), () =>
                {
                    var a = _action;
                    Hide();
                    a?.Invoke();
                });
            PinTheActionButtonToTheRightEdge(_actionButton.GetComponent<RectTransform>());
            _actionLabel = _actionButton.GetComponentInChildren<TMP_Text>();
            _actionButton.gameObject.SetActive(false);

            panel.gameObject.SetActive(false);
        }

        private static void PinTheActionButtonToTheRightEdge(RectTransform button)
        {
            button.anchorMin = button.anchorMax = button.pivot = new Vector2(1, 0.5f);
            button.anchoredPosition = new Vector2(-6, 0);
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
            LayOutLabelBesideTheActionButton(hasAction);

            _activeRoutine = StartCoroutine(ShowRoutine(message, duration));
        }

        internal void LayOutLabelBesideTheActionButton(bool hasAction)
        {
            if (_label == null) return;

            _label.rectTransform.offsetMax =
                new Vector2(hasAction ? -LabelWidthGivenUpToTheActionButton : 0f, 0f);
            _label.alignment = hasAction ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
            _label.margin = hasAction ? new Vector4(12, 0, 0, 0) : Vector4.zero;
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
