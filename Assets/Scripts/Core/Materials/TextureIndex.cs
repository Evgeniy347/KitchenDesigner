using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class TextureIndex
    {
        private const int DefaultTileMM = 800;
        private const string DefaultKind = "ЛДСП";

        private static readonly Color TexturedDefaultColor = Color.white;
        private static readonly Color PlainDefaultColor = new Color(0.80f, 0.80f, 0.80f);

        [Serializable]
        private class Entry
        {
            public string id = string.Empty;
            public string name = string.Empty;
            public string kind = string.Empty;
            public string file = string.Empty;
            public int tileWidthMM = DefaultTileMM;
            public int tileHeightMM = 0;
            public string color = string.Empty;
            public float metallic = 0f;
            public float smoothness = 0.2f;
        }

        [Serializable]
        private class IndexFile
        {
            public int version = 1;
            public List<Entry> textures = new List<Entry>();
        }

        public static List<MaterialDef> Parse(string? json, out List<string> errors)
        {
            errors = new List<string>();
            var defs = new List<MaterialDef>();

            if (string.IsNullOrWhiteSpace(json))
            {
                errors.Add("индекс пуст");
                return defs;
            }

            IndexFile? file;
            try
            {
                file = JsonUtility.FromJson<IndexFile>(json.TrimStart('﻿', ' ', '\t', '\r', '\n'));
            }
            catch (Exception e)
            {
                errors.Add($"не разобрать JSON: {e.Message}");
                return defs;
            }

            if (file == null || file.textures == null)
            {
                errors.Add("нет массива textures");
                return defs;
            }

            var seen = new HashSet<string>();
            for (int i = 0; i < file.textures.Count; i++)
            {
                var def = ParseEntry(file.textures[i], i, seen, errors);
                if (def != null) defs.Add(def);
            }

            return defs;
        }

        private static MaterialDef? ParseEntry(Entry? e, int index, HashSet<string> seen,
            List<string> errors)
        {
            if (e == null)
            {
                errors.Add($"запись #{index}: пустой объект");
                return null;
            }

            var id = (e.id ?? string.Empty).Trim();
            if (id.Length == 0)
            {
                errors.Add($"запись #{index}: пустой id — декор пропущен");
                return null;
            }

            if (!seen.Add(id))
            {
                errors.Add($"запись #{index}: id '{id}' уже встречался — декор пропущен");
                return null;
            }

            if (e.tileWidthMM <= 0)
            {
                errors.Add($"'{id}': tileWidthMM={e.tileWidthMM} — декор пропущен");
                return null;
            }

            var fileName = (e.file ?? string.Empty).Trim();
            bool hasFile = fileName.Length > 0;

            var color = hasFile ? TexturedDefaultColor : PlainDefaultColor;
            var raw = (e.color ?? string.Empty).Trim();
            if (raw.Length > 0 && !ColorUtility.TryParseHtmlString(raw, out color))
            {
                errors.Add($"'{id}': цвет '{raw}' не разобран — взят цвет по умолчанию");
                color = hasFile ? TexturedDefaultColor : PlainDefaultColor;
            }

            var name = (e.name ?? string.Empty).Trim();
            if (name.Length == 0) name = id.Replace('_', ' ');

            var kind = (e.kind ?? string.Empty).Trim();
            if (kind.Length == 0) kind = DefaultKind;

            return new MaterialDef(id, name, kind, color,
                hasFile ? fileName : null, e.tileWidthMM,
                Mathf.Max(0f, e.metallic), Mathf.Max(0f, e.smoothness))
            {
                tileHeightMM = Mathf.Max(0, e.tileHeightMM),
            };
        }
    }
}
