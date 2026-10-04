using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public static class OutlineIcons
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static int TextureSide =>
            (int)(OutlineIconPaths.GridSize * OutlineIconPaths.TexelsPerGridUnit);

        public static Sprite Get(string name)
        {
            if (Cache.TryGetValue(name, out var cached) && cached != null) return cached;
            var sprite = Build(name);
            Cache[name] = sprite;
            return sprite;
        }

        private static Sprite Build(string name)
        {
            if (!OutlineIconPaths.Table.TryGetValue(name, out var path))
                throw new KeyNotFoundException("no outline icon named '" + name + "'");

            int side = TextureSide;
            var alpha = StrokeRaster.Alpha(StrokePath.Parse(path), OutlineIconPaths.GridSize, side,
                OutlineIconPaths.StrokeWidth);

            var pixels = new Color32[side * side];
            Color32 ink = UIStyle.NoTint;
            for (int row = 0; row < side; row++)
            for (int col = 0; col < side; col++)
            {
                ink.a = alpha[row * side + col];
                pixels[(side - 1 - row) * side + col] = ink;
            }

            var texture = new Texture2D(side, side, TextureFormat.RGBA32, false)
            {
                name = "OutlineIcon_" + name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, side, side), new Vector2(0.5f, 0.5f),
                RoundedRectSprites.ReferencePixelsPerUnit);
            sprite.name = texture.name;
            return sprite;
        }
    }
}
