using UnityEngine;

namespace KitchenDesigner.Core.Ventilation
{
    public static class DuctSpecItems
    {
        public static string Section => SpecSections.Structures;
        public static string DuctSheetName => Loc.T("spec.vent.ductSheet");
        public static string GrilleName => Loc.T("spec.vent.grille");

        public static float SheetMetalAreaM2(in DuctProfile profile, float lengthMm)
        {
            if (lengthMm <= 0f) return 0f;

            if (profile.Kind == DuctProfileKind.Round)
            {
                float circumferenceMm = (float)System.Math.PI * profile.DiameterMm;
                return circumferenceMm * lengthMm * 1e-6f;
            }

            var from = new Vector3(0f, 0f, 0f);
            var to = new Vector3(0f, lengthMm, 0f);
            return BoxRunMesh.UnfoldedAreaM2(from, to, profile.WidthMm, profile.HeightMm);
        }

        public static SpecItem DuctLine(in DuctProfile profile, float lengthMm) =>
            new SpecItem(Section, DuctSheetName, profile.ProfileId, SpecUnit.AreaM2,
                SheetMetalAreaM2(profile, lengthMm));

        public static SpecItem GrilleLine(int widthMm, int heightMm) =>
            new SpecItem(Section, GrilleName, widthMm + "x" + heightMm, SpecUnit.Pieces, 1f);
    }
}
