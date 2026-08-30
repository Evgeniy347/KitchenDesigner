using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class ProjectFileActions
    {
        public const string QuickSaveName = "quicksave";

#if UNITY_WEBGL
        private readonly Transform _canvas;
#endif

        public ProjectFileActions(Transform canvas)
        {
#if UNITY_WEBGL
            _canvas = canvas;
#endif
        }

        public void SaveCurrent()
        {
#if UNITY_WEBGL
            ServerSave(forceNew: false);
#else
            if (SaveLoadManager.HasLastPath)
            {
                if (SaveLoadManager.SaveToLastPath())
                    ShowSaved(System.IO.Path.GetFileName(SaveLoadManager.LastPath));
            }
            else if (SaveLoadManager.SaveProject(QuickSaveName))
            {
                SaveLoadManager.LastPath = SaveLoadManager.PathForName(QuickSaveName);
                ShowSaved(QuickSaveName);
            }
#endif
        }

        public void SaveAs()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string suggested = WebFileDialog.SuggestedFileName(SaveLoadManager.LastPath);
            WebFileDialog.Save(SaveLoadManager.CaptureCurrentJson(), suggested,
                name => ShowSaved(name));
#else
            string suggested = SaveLoadManager.HasLastPath
                ? System.IO.Path.GetFileName(SaveLoadManager.LastPath)
                : "kitchen.json";
            string? path = NativeFileDialog.SaveDialog("Сохранить проект кухни",
                suggested, SaveLoadManager.LastDirectory);
            if (string.IsNullOrEmpty(path)) return;
            if (SaveLoadManager.SaveToPath(path))
                ShowSaved(System.IO.Path.GetFileName(path));
#endif
        }

        public void LoadDialog()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebFileDialog.Open((fileName, content) =>
            {
                var data = SaveLoadManager.Deserialize(content);
                if (data == null)
                {
                    Toast("Не удалось прочитать файл");
                    return;
                }
                SaveLoadManager.ClearBoards(PartRegistry.GetAll());
                SaveLoadManager.RestoreScene(data);
                if (ElementHighlighter.Instance != null)
                    ElementHighlighter.Instance.RefreshHighlights();
                Toast("Загружено: " + fileName);
            });
#else
            string? path = NativeFileDialog.OpenDialog("Открыть проект кухни",
                SaveLoadManager.LastDirectory);
            if (string.IsNullOrEmpty(path)) return;
            if (SaveLoadManager.LoadFromPath(path))
                Toast("Загружено: " + System.IO.Path.GetFileName(path));
#endif
        }

        private static void Toast(string msg) => ToastNotification.ShowIfAvailable(msg);

        private static void ShowSaved(string name) =>
            StatusBarUI.Instance?.ShowTransient("Сохранено: " + name, UIStyle.HighlightOk, 3f);

#if UNITY_WEBGL
        private GameObject? _namePromptPanel;
        private TMP_InputField? _nameInputField;

        private void ServerSave(bool forceNew)
        {
            if (!Networking.ProjectApiClient.Enabled)
            {
                LocalSave(forceNew);
                return;
            }

            string json = SaveLoadManager.CaptureCurrentJson();
            var api = Networking.ProjectApiClient.Instance;

            if (!forceNew && api!.HasCurrentProject)
            {
                api.SaveCurrent(json,
                    () => Toast("Сохранено: " + api.CurrentProjectName),
                    err => Toast("Ошибка сохранения: " + err));
            }
            else
            {
                string defaultName = api!.HasCurrentProject
                    ? api.CurrentProjectName
                    : "Новый проект";
                ShowNamePrompt(defaultName, name =>
                {
                    api.CreateAndSave(name, json, id =>
                    {
                        SaveLoadManager.LastPath = id;
                        Toast("Сохранено: " + name);
                    }, err => Toast("Ошибка: " + err));
                });
            }
        }

        private void LocalSave(bool forceNew)
        {
            string json = SaveLoadManager.CaptureCurrentJson();
            if (!forceNew && SaveLoadManager.HasLastPath)
            {
                if (SaveLoadManager.SaveToLastPath())
                {
                    Toast("Сохранено");
                    return;
                }
            }

            string defaultName = SaveLoadManager.HasLastPath
                ? System.IO.Path.GetFileNameWithoutExtension(SaveLoadManager.LastPath)
                : QuickSaveName;
            ShowNamePrompt(defaultName, name =>
            {
                string path = SaveLoadManager.PathForName(name);
                SaveLoadManager.LastPath = path;
                if (SaveLoadManager.SaveToPath(path))
                    Toast("Сохранено: " + name);
            });
        }

        private void ShowNamePrompt(string defaultName, System.Action<string> onConfirm)
        {
            BuildNamePromptPanel();
            _nameInputField!.text = defaultName;
            _namePromptPanel!.SetActive(true);

            void confirmAction()
            {
                string name = _nameInputField.text.Trim();
                if (string.IsNullOrEmpty(name)) return;
                _namePromptPanel.SetActive(false);
                onConfirm(name);
            }

            var okBtn = _namePromptPanel.transform.Find("OkBtn");
            var cancelBtn = _namePromptPanel.transform.Find("CancelBtn");

            if (okBtn != null)
            {
                var btn = okBtn.GetComponent<Button>();
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(confirmAction);
            }

            if (cancelBtn != null)
            {
                var btn = cancelBtn.GetComponent<Button>();
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => _namePromptPanel.SetActive(false));
            }

            _nameInputField.onEndEdit.RemoveAllListeners();
            _nameInputField.onEndEdit.AddListener(text =>
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    confirmAction();
            });
        }

        private void BuildNamePromptPanel()
        {
            if (_namePromptPanel != null) return;

            _namePromptPanel = new GameObject("NamePromptPanel");
            _namePromptPanel.transform.SetParent(_canvas, false);
            var rt = _namePromptPanel.AddComponent<RectTransform>();
            UIFactory.AnchorCenter(rt);
            rt.sizeDelta = new Vector2(380, 160);
            rt.anchoredPosition = Vector2.zero;

            var bg = _namePromptPanel.AddComponent<Image>();
            bg.color = UIFactory.PanelColor;

            UIFactory.CreateLabel("PromptTitle", _namePromptPanel.transform,
                "Название проекта", 20,
                new Vector2(0, -14), new Vector2(340, 32),
                TextAnchor.MiddleCenter);

            _nameInputField = UIFactory.CreateInputField("NameInput",
                _namePromptPanel.transform, "",
                new Vector2(0, 28), new Vector2(340, 34));

            var okBtn = UIFactory.CreateButton("OkBtn", _namePromptPanel.transform,
                "OK", new Vector2(-70, 68), new Vector2(110, 32), null);
            UIFactory.CreateButton("CancelBtn", _namePromptPanel.transform,
                "Отмена", new Vector2(70, 68), new Vector2(110, 32), null);

            _namePromptPanel.SetActive(false);
        }
#endif
    }
}
