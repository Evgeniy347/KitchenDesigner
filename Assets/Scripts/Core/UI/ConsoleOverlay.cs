using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public class ConsoleOverlay : MonoBehaviour
    {
        private const int TailLinesShown = 32;
        internal const int AboveEveryOtherCanvas = 200;
        private const int NothingShownYet = -1;

        private GameObject? _root;
        private TMP_Text? _text;
        private int _shownRevision = NothingShownYet;

        private void Start()
        {
            var canvas = UIFactory.CreateCanvas("ConsoleCanvas");
            canvas.sortingOrder = AboveEveryOtherCanvas;

            var rect = UIFactory.CreateRect("ConsolePanel", canvas.transform);
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var bg = rect.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.8f);
            bg.raycastTarget = false;
            _root = rect.gameObject;

            var textRect = UIFactory.CreateRect("ConsoleText", rect);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 8);
            textRect.offsetMax = new Vector2(-10, -8);
            _text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            _text.font = UIFactory.FontAsset;
            _text.fontSize = 14;
            _text.color = new Color(0.85f, 0.9f, 0.85f);
            _text.alignment = TextAlignmentOptions.BottomLeft;
            _text.enableWordWrapping = true;
            _text.overflowMode = TextOverflowModes.Truncate;
            _text.raycastTarget = false;

            _root!.SetActive(false);
        }

        private void OnEnable() => Application.logMessageReceived += OnLog;
        private void OnDisable() => Application.logMessageReceived -= OnLog;

        internal static ConsoleLineKind KindOf(LogType type) => type switch
        {
            LogType.Error or LogType.Exception or LogType.Assert => ConsoleLineKind.Error,
            LogType.Warning => ConsoleLineKind.Warning,
            _ => ConsoleLineKind.Log,
        };

        private void OnLog(string condition, string stackTrace, LogType type) =>
            ConsoleLog.Shared.Append(KindOf(type), condition, DateTime.Now);

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.BackQuote) && !CameraController.IsTypingInInputField())
            {
                _root!.SetActive(!_root.activeSelf);
                _shownRevision = NothingShownYet;
            }

            if (!_root!.activeSelf) return;
            if (_shownRevision == ConsoleLog.Shared.Revision) return;

            _shownRevision = ConsoleLog.Shared.Revision;
            _text!.text = ConsoleLog.Shared.Tail(TailLinesShown);
        }
    }
}
