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
        private Coroutine? _activeRoutine;

        private void Awake()
        {
            Instance = this;
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

            panel.gameObject.SetActive(false);
        }

        public void Show(string message, float duration = 2f)
        {
            if (_activeRoutine != null)
                StopCoroutine(_activeRoutine);
            _activeRoutine = StartCoroutine(ShowRoutine(message, duration));
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
