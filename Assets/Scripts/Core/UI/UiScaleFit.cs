using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class UiScaleFit : MonoBehaviour
    {
        private CanvasScaler? _scaler;

        public Vector2? EmulatedScreen { get; set; }

        public float AppliedScaleFactor => _scaler != null ? _scaler.scaleFactor : 1f;

        public static UiScaleFit Attach(CanvasScaler scaler)
        {
            var fit = scaler.gameObject.AddComponent<UiScaleFit>();
            fit._scaler = scaler;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            fit.Apply();
            return fit;
        }

        public void Apply()
        {
            if (_scaler == null) _scaler = GetComponent<CanvasScaler>();
            if (_scaler == null) return;
            float realHeight = Screen.height;
            var emulated = EmulatedScreen ?? new Vector2(Screen.width, realHeight);
            float factor = UiScale.ScaleFactorFor(realHeight, emulated.x, emulated.y,
                UiScalePreference.Percent);
            if (!Mathf.Approximately(_scaler.scaleFactor, factor)) _scaler.scaleFactor = factor;
        }

        private void LateUpdate() => Apply();
    }
}
