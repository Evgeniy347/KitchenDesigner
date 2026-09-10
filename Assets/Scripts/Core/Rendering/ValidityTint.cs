using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public enum ValidityPaint
    {
        Own,
        Violation,
        Dimmed,
        SeeThrough,
        SeeThroughViolation,
    }

    public static class ValidityTint
    {
        public const string Name = ElementTint.ValidityName;

        public const float ViolationStrength = 0.5f;
        public const float DimmedStrength = 0.75f;
        public const float SeeThroughAlpha = 0.12f;

        private static readonly Color DimTarget = new Color(0.35f, 0.35f, 0.38f, 1f);

        private static readonly Dictionary<(int own, ValidityPaint paint), Material> Derived =
            new Dictionary<(int own, ValidityPaint paint), Material>();

        private static readonly Dictionary<int, Material> Sources = new Dictionary<int, Material>();

        private static readonly Dictionary<string, Material> SourcesByName =
            new Dictionary<string, Material>(StringComparer.Ordinal);

        public static Color ViolationColor => UI.UIStyle.HighlightError;

        private static string Canonical(string? materialName) =>
            materialName == null ? string.Empty : materialName.Replace(" (Instance)", string.Empty);

        public static Material? OwnOf(Material? material)
        {
            if (material == null) return null;

            int id = material.GetInstanceID();
            if (Sources.TryGetValue(id, out var own))
            {
                if (own != null) return own;
                Sources.Remove(id);
            }

            if (!material.name.StartsWith(Name, StringComparison.Ordinal)) return null;

            var key = Canonical(material.name);
            if (!SourcesByName.TryGetValue(key, out var byName)) return null;
            if (byName != null) return byName;
            SourcesByName.Remove(key);
            return null;
        }

        public static Material? Of(ValidityPaint paint, Material? current)
        {
            var own = OwnOf(current) ?? current;
            if (own == null) return null;
            if (paint == ValidityPaint.Own) return own;

            var key = (own.GetInstanceID(), paint);
            if (Derived.TryGetValue(key, out var cached) && cached != null) return cached;

            var made = Make(paint, own);
            made.name = Name + " " + paint + " · " + own.name;
            Derived[key] = made;
            Sources[made.GetInstanceID()] = own;
            SourcesByName[Canonical(made.name)] = own;
            return made;
        }

        public static void Clear()
        {
            Derived.Clear();
            Sources.Clear();
            SourcesByName.Clear();
        }

        private static Color Blend(Color own, Color target, float strength) =>
            new Color(
                Mathf.Lerp(own.r, target.r, strength),
                Mathf.Lerp(own.g, target.g, strength),
                Mathf.Lerp(own.b, target.b, strength),
                own.a);

        public static Color BaseColorOf(Material? material)
        {
            if (material == null) return Color.white;
            if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
            if (material.HasProperty("_Color")) return material.GetColor("_Color");
            return Color.white;
        }

        private static Material Make(ValidityPaint paint, Material own)
        {
            var color = BaseColorOf(own);
            switch (paint)
            {
                case ValidityPaint.Violation:
                    return Opaque(own, Blend(color, ViolationColor, ViolationStrength));
                case ValidityPaint.Dimmed:
                    return Opaque(own, Blend(color, DimTarget, DimmedStrength));
                case ValidityPaint.SeeThroughViolation:
                    return TransparentMaterial.Make(own,
                        Faded(Blend(color, ViolationColor, ViolationStrength)));
                default:
                    return TransparentMaterial.Make(own, Faded(color));
            }
        }

        private static Color Faded(Color color) =>
            new Color(color.r, color.g, color.b, SeeThroughAlpha);

        private static Material Opaque(Material own, Color color)
        {
            var material = new Material(own);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            return material;
        }
    }
}
