using System.Collections;
using KitchenDesigner.Core.Update;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public class ToastNotification : MonoBehaviour
    {
        public static ToastNotification? Instance { get; private set; }

        private const float FadeInSeconds = 0.15f;
        private const float FadeOutSeconds = 0.3f;

        private ToastView? _view;
        private System.Action? _action;
        private Coroutine? _activeRoutine;

        internal ToastView? View => _view;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(Instance, this)) Instance = null;
        }

        public static void ShowIfAvailable(string message, float duration = 2f,
            string? actionLabel = null, System.Action? action = null,
            StatusLevel level = StatusLevel.Info)
        {
            var toast = Instance;
            if (toast == null) return;
            toast.Show(message, duration, actionLabel, action, level);
        }

        public void Build(Transform canvasTransform)
        {
            _view = ToastView.Create(canvasTransform, RunAction, Hide);
        }

        public void Show(string message, float duration = 2f,
            string? actionLabel = null, System.Action? action = null,
            StatusLevel level = StatusLevel.Info)
        {
            if (_view == null) return;
            if (_activeRoutine != null)
                StopCoroutine(_activeRoutine);

            _action = action;
            bool hasAction = action != null && !string.IsNullOrEmpty(actionLabel);
            _view.Apply(message, level, actionLabel, hasAction);
            _activeRoutine = StartCoroutine(ShowRoutine(duration));
        }

        private void RunAction()
        {
            var action = _action;
            Hide();
            action?.Invoke();
        }

        private void Hide()
        {
            if (_activeRoutine != null)
            {
                StopCoroutine(_activeRoutine);
                _activeRoutine = null;
            }
            _action = null;
            if (_view == null) return;
            _view.Group.alpha = 0f;
            _view.Panel.gameObject.SetActive(false);
        }

        private IEnumerator ShowRoutine(float duration)
        {
            var group = _view!.Group;
            group.alpha = 0f;
            _view.Panel.gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < FadeInSeconds)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Lerp(0f, 1f, elapsed / FadeInSeconds);
                yield return null;
            }
            group.alpha = 1f;

            yield return new WaitForSeconds(duration);

            elapsed = 0f;
            while (elapsed < FadeOutSeconds)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Lerp(1f, 0f, elapsed / FadeOutSeconds);
                yield return null;
            }
            group.alpha = 0f;
            _view.Panel.gameObject.SetActive(false);
            _activeRoutine = null;
        }
    }
}
