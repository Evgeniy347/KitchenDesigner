using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.Construction;

namespace KitchenDesigner.Core
{
    public static class WallSpecItems
    {
        public const string Section = SpecSections.Walls;
        public const string MortarName = "Раствор";
        public const string StudsName = "Каркас: стойки";
        public const string FrameRunName = "Каркас: обвязка и стойки";

        public static IEnumerable<SpecItem> Of(MasonryTechnology technology,
            float lengthMm, float heightMm, float thicknessMm,
            IReadOnlyList<WallOpening>? openings, float jointMm, float wastePct)
        {
            var unit = MasonryUnit.Of(technology);
            var quantities = WallQuantities.Of(technology, lengthMm, heightMm, thicknessMm,
                openings, jointMm, wastePct);

            switch (unit.Counting)
            {
                case MasonryCounting.Pieces:
                    if (quantities.Pieces > 0)
                        yield return new SpecItem(Section, unit.Title, "", SpecUnit.Pieces,
                            quantities.Pieces, FormatDims(unit), hasDims: true);
                    if (quantities.MortarM3 > 0d)
                        yield return new SpecItem(Section, MortarName, "", SpecUnit.VolumeM3,
                            (float)quantities.MortarM3);
                    break;

                case MasonryCounting.Volume:
                    if (quantities.TimberM3 > 0d)
                        yield return new SpecItem(Section, unit.Title, "", SpecUnit.VolumeM3,
                            (float)quantities.TimberM3);
                    break;

                case MasonryCounting.Studs:
                    if (quantities.Studs > 0)
                        yield return new SpecItem(Section, StudsName, "", SpecUnit.Pieces,
                            quantities.Studs);
                    if (quantities.RunningMetres > 0d)
                        yield return new SpecItem(Section, FrameRunName, "",
                            SpecUnit.LinearMeters, (float)quantities.RunningMetres);
                    break;
            }
        }

        private static Vector3Int FormatDims(in MasonryUnit unit) => new Vector3Int(
            Mathf.RoundToInt(unit.LengthMm),
            Mathf.RoundToInt(unit.HeightMm),
            Mathf.RoundToInt(unit.WidthMm));
    }
}
