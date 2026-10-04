using System.Collections.Generic;
using System.Linq;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsGeneralTab
    {
        internal const string LanguageRowId = "Language";

        internal const string UiScaleRowId = "UiScale";

        private readonly SettingsPage _page;

        public SettingsGeneralTab(SettingsPage page) => _page = page;

        public void Build()
        {
            _page.Section(Loc.T("settings.general.section.interface"));
            BuildLanguageRow();
            BuildUiScaleRow();
        }

        private void BuildLanguageRow()
        {
            var languages = Loc.Languages;
            _page.AddDropdown(Loc.T("settings.project.language"),
                languages.Select(l => l.NativeName).ToList(),
                IndexOfCurrent(languages),
                index =>
                {
                    if (index >= 0 && index < languages.Count) LanguageStartup.Choose(languages[index].Code);
                },
                id: LanguageRowId,
                read: () => IndexOfCurrent(Loc.Languages),
                optionLanguages: languages.Select(l => (string?)l.Code).ToList());
        }

        private void BuildUiScaleRow()
        {
            _page.AddDropdown(Loc.T("settings.project.uiScale"),
                UiScale.ChoicePercents.Select(UiScaleChoiceLabel).ToList(),
                UiScale.ChoiceIndex(UiScalePreference.Percent),
                index =>
                {
                    if (index >= 0 && index < UiScale.ChoicePercents.Count)
                        UiScalePreference.Choose(UiScale.ChoicePercents[index]);
                },
                id: UiScaleRowId,
                read: () => UiScale.ChoiceIndex(UiScalePreference.Percent));
        }

        internal static string UiScaleChoiceLabel(int percent) =>
            percent == UiScale.AutoPercent
                ? Loc.T("settings.project.uiScaleAuto")
                : NumberFormat.WithUnit(NumberFormat.Integer(percent), "%");

        private static int IndexOfCurrent(IReadOnlyList<LanguageInfo> languages)
        {
            for (int i = 0; i < languages.Count; i++)
                if (languages[i].Code == Loc.Language) return i;
            return 0;
        }
    }
}
