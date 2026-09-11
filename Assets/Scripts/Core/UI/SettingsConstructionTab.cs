using System.Collections.Generic;
using TMPro;
using UnityEngine;
using KitchenDesigner.Core.Construction;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsConstructionTab
    {
        public const string RegionId = "Регион";
        public const string FrostDepthId = "Глубина промерзания";
        public const string MasonryId = "Технология";
        public const string SoilId = "Грунт";
        public const string ConcreteId = "Класс бетона";
        public const string FrostDepthUnknown = "—";

        private readonly SettingsRowFactory _rows;
        private TextMeshProUGUI? _frostDepthValue;

        public SettingsConstructionTab(SettingsRowFactory rows) => _rows = rows;

        public static string FrostDepthText(KitchenSettings s) =>
            FrostDepth.TryNormativeMm(s.ConstructionRegion, s.ConstructionSoil, out float mm)
                ? Mathf.RoundToInt(mm).ToString(System.Globalization.CultureInfo.InvariantCulture)
                  + " мм"
                : FrostDepthUnknown;

        public static List<string> MasonryTitles()
        {
            var titles = new List<string>(MasonryUnit.Table.Count);
            foreach (var unit in MasonryUnit.Table) titles.Add(unit.Title);
            return titles;
        }

        public void Build(Transform page, KitchenSettings s, float topY)
        {
            float y = topY;

            _rows.AddHeader(page, ref y, "Участок");

            _rows.AddDropdown(page, ref y, RegionId,
                new List<string>(ConstructionRegionTitles.All), (int)s.ConstructionRegion,
                v => { s.ConstructionRegion = (ConstructionRegion)v; ShowFrostDepth(s); },
                read: () => (int)s.ConstructionRegion);
            Hint(RegionId, hint: "settings.construction.region");

            _frostDepthValue = _rows.AddReadOnlyValue(page, ref y, FrostDepthId, FrostDepthText(s),
                read: () => FrostDepthText(s));
            Hint(FrostDepthId, hint: "settings.construction.frostDepth");

            _rows.AddDropdown(page, ref y, SoilId,
                new List<string>(SoilKindTitles.All), (int)s.ConstructionSoil,
                v => { s.ConstructionSoil = (SoilKind)v; ShowFrostDepth(s); },
                read: () => (int)s.ConstructionSoil);
            Hint(SoilId, hint: "settings.construction.soil");

            y -= SettingsRowFactory.GapPx;
            _rows.AddHeader(page, ref y, "Дом");

            AddMillimetres(page, ref y, "Высота этажа",
                () => s.ConstructionFloorHeightMm, v => s.ConstructionFloorHeightMm = v);
            Hint("Высота этажа", hint: "settings.construction.floorHeight");

            y -= SettingsRowFactory.GapPx;
            _rows.AddHeader(page, ref y, "Кладка");

            _rows.AddDropdown(page, ref y, MasonryId, MasonryTitles(),
                (int)s.ConstructionMasonry,
                v => { s.ConstructionMasonry = (MasonryTechnology)v; },
                read: () => (int)s.ConstructionMasonry);
            Hint(MasonryId, hint: "settings.construction.masonry");

            AddMillimetres(page, ref y, "Шов",
                () => s.ConstructionJointMm, v => s.ConstructionJointMm = v);
            Hint("Шов", hint: "settings.construction.joint");

            AddWhole(page, ref y, "Запас", "%",
                () => s.ConstructionWastePct, v => s.ConstructionWastePct = v);
            Hint("Запас", hint: "settings.construction.waste");

            y -= SettingsRowFactory.GapPx;
            _rows.AddHeader(page, ref y, "Фундамент");

            _rows.AddDropdown(page, ref y, ConcreteId,
                new List<string>(ConcreteGradeTitles.All), (int)s.ConstructionConcrete,
                v => { s.ConstructionConcrete = (ConcreteGrade)v; },
                read: () => (int)s.ConstructionConcrete);
            Hint(ConcreteId, hint: "settings.construction.concrete");

            AddMillimetres(page, ref y, "Подушка: песок",
                () => s.ConstructionSandMm, v => s.ConstructionSandMm = v);
            Hint("Подушка: песок", hint: "settings.construction.sand");

            AddMillimetres(page, ref y, "Подушка: щебень",
                () => s.ConstructionGravelMm, v => s.ConstructionGravelMm = v);
            Hint("Подушка: щебень", hint: "settings.construction.gravel");

            _rows.AddToggle(page, ref y, "Трамбовка", s.ConstructionCompacted,
                v => { s.ConstructionCompacted = v; }, read: () => s.ConstructionCompacted);
            Hint("Трамбовка", hint: "settings.construction.compacted");
        }

        private void AddMillimetres(Transform page, ref float y, string label,
            System.Func<int> read, System.Action<int> write) =>
            AddWhole(page, ref y, label, "мм", read, write);

        private void AddWhole(Transform page, ref float y, string label, string unit,
            System.Func<int> read, System.Action<int> write)
        {
            _rows.AddInput(page, ref y, label, read().ToString(),
                TMP_InputField.ContentType.IntegerNumber,
                f =>
                {
                    int parsed = ExpressionParser.EvaluateInt(f.text)
                        ?? (int.TryParse(f.text, out int direct) ? direct : read());
                    write(parsed);
                    f.text = read().ToString();
                }, read().ToString(), unit: unit, read: () => read().ToString());
        }

        private void ShowFrostDepth(KitchenSettings s)
        {
            if (_frostDepthValue != null) _frostDepthValue.text = FrostDepthText(s);
        }

        private void Hint(string rowKey, string hint) =>
            HintBadge.AttachAfterLabel(_rows.RowLabel(rowKey), hint);
    }
}
