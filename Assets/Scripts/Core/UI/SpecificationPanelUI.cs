using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public class SpecificationPanelUI : MonoBehaviour, IProjectWindow
    {
        public const string TableNode = "SpecTable";
        public const string ExportNode = "SpecExport";
        public const string CopyNode = "SpecCopy";
        public const string CountNode = "SpecCount";

        private WindowChrome? _chrome;
        private DataTable? _table;
        private TMP_Text? _count;
        private Button? _export;
        private Button? _copy;

        internal DataTable Table => _table!;
        internal TMP_Text Count => _count!;

        public string WindowId => "specification";
        public RectTransform? WindowRect => _chrome?.Panel;
        public bool HeightAdjustable => false;

        public bool IsVisible => _chrome != null && _chrome.Panel.gameObject.activeSelf;

        public void Build(Transform canvas)
        {
            _chrome = WindowChrome.Create(canvas, "SpecPanel", Loc.T("spec.title"), UIStyle.SpecificationSize,
                new WindowChromeOptions
                {
                    OnClose = () => SetVisible(false),
                    HasFooter = true,
                    RuledHeader = true,
                });
            ProjectWindows.Register(this);

            BuildTable();
            var footer = _chrome.Footer!;
            _count = footer.AddLeftText(CountNode, "");
            _copy = footer.AddSecondary(CopyNode, Loc.T("spec.copy"), CopyCsv);
            _export = footer.AddPrimary(ExportNode, Loc.T("spec.exportCsv"), ExportCsv);

            _chrome.Panel.gameObject.SetActive(false);
        }

        private void BuildTable()
        {
            float height = _chrome!.Panel.sizeDelta.y - UIStyle.TitleBarH - UIStyle.FooterH;
            _table = DataTable.Create(_chrome.Panel, TableNode, new Vector2(_chrome.BodyWidth, height),
                SpecificationRows.Columns());
            var rt = _table.Root;
            UIFactory.AnchorTopLeft(rt);
            rt.anchoredPosition = new Vector2(_chrome.BodyPad, -UIStyle.TitleBarH);
            _table.SetEmptyState(Loc.T("spec.empty.title"), Loc.T("spec.empty.hint"));
        }

        private void OnDestroy() => ProjectWindows.Unregister(this);

        public void Toggle() => SetVisible(!IsVisible);

        public void SetVisible(bool visible)
        {
            if (_chrome == null) return;
            _chrome.Panel.gameObject.SetActive(visible);
            if (visible) Refresh();
        }

        private static SpecResult CurrentResult() => SpecificationManager.Build(PartRegistry.All);

        private void Refresh()
        {
            var result = CurrentResult();
            var model = SpecificationRows.Build(result);
            _table!.SetRows(model.Rows);
            _table.Body.Scroll.verticalNormalizedPosition = 1f;

            bool any = result.lines.Count > 0;
            _export!.interactable = any;
            _copy!.interactable = any;
            SetCount(Loc.F("spec.footerCount", model.Positions, model.Sections));
        }

        private void SetCount(string text)
        {
            _count!.text = text;
            var rt = _count.rectTransform;
            rt.sizeDelta = new Vector2(_count.GetPreferredValues(text).x + 1f, rt.sizeDelta.y);
        }

        private void CopyCsv()
        {
            GUIUtility.systemCopyBuffer = SpecificationExport.ToCsv(CurrentResult());
            ToastNotification.ShowIfAvailable(Loc.T("spec.copied"), 2f);
        }

        private void ExportCsv()
        {
            var result = CurrentResult();
            string defaultName = $"KitchenSpec_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string? path = NativeFileDialog.SaveCSVDialog(Loc.T("spec.exportDialogTitle"), defaultName,
                Application.persistentDataPath);
            bool userCancelledTheDialog = string.IsNullOrEmpty(path);
            if (userCancelledTheDialog)
                return;

            if (SpecificationExport.SaveToFile(result, path!))
            {
                ToastNotification.ShowIfAvailable(Loc.T("spec.csvSaved"), 2f);
            }
            else
            {
                Debug.LogError("[Spec] CSV export failed");
            }
        }
    }
}
