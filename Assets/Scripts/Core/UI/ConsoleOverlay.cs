using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public class ConsoleOverlay : MonoBehaviour
    {
        private const int KeptLines = 300;
        private const int TailLinesShown = 32;
        internal const int AboveEveryOtherCanvas = 200;

        private GameObject? _root;
        private TMP_Text? _text;
        private readonly Queue<string> _lines = new Queue<string>();
        private bool _dirty;

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

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            string prefix = type switch
            {
                LogType.Error or LogType.Exception => "<!> ",
                LogType.Warning => "<w> ",
                _ => ""
            };
            _lines.Enqueue(prefix + condition);
            while (_lines.Count > KeptLines) _lines.Dequeue();
            _dirty = true;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.BackQuote) && !CameraController.IsTypingInInputField())
            {
                _root!.SetActive(!_root.activeSelf);
                _dirty = true;
            }

            if (_dirty && _root!.activeSelf)
            {
                _text!.text = BuildTail();
                _dirty = false;
            }
        }

        private string BuildTail()
        {
            var arr = _lines.ToArray();
            int start = Mathf.Max(0, arr.Length - TailLinesShown);
            var sb = new StringBuilder();
            for (int i = start; i < arr.Length; i++)
                sb.AppendLine(arr[i]);
            return sb.ToString();
        }
    }
}
