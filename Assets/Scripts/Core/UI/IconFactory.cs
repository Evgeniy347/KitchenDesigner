using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public static class IconFactory
    {
        private const int S = 64;
        private static readonly Color Ink = new Color(0.90f, 0.91f, 0.95f, 1f);
        private static readonly Color Ink2 = new Color(0.62f, 0.66f, 0.78f, 1f);
        private static readonly Color Accent = new Color(0.45f, 0.85f, 0.5f, 1f);
        private static readonly Color Clear = new Color(0, 0, 0, 0);

        private static Sprite? _gear, _floppy, _floppyPlus, _folder, _undo, _redo, _pin, _warning, _pencil;
        private static Sprite? _caretUp, _caretDown;
        private static Sprite? _ruler, _bulb, _sun, _eyedropper, _crosshair;
        private static Sprite? _note, _play, _pause, _trackNext, _trackPrev;

        public static Sprite Gear => _gear ??= BuildGear();
        public static Sprite Floppy => _floppy ??= BuildFloppy(false);
        public static Sprite FloppyPlus => _floppyPlus ??= BuildFloppy(true);
        public static Sprite Folder => _folder ??= BuildFolder();
        public static Sprite Undo => _undo ??= BuildCircularArrow(redo: false);
        public static Sprite Redo => _redo ??= BuildCircularArrow(redo: true);
        public static Sprite Pin => _pin ??= BuildPin();
        public static Sprite Warning => _warning ??= BuildWarning();
        public static Sprite Pencil => _pencil ??= BuildPencil();
        public static Sprite CaretUp => _caretUp ??= BuildCaret(true);
        public static Sprite CaretDown => _caretDown ??= BuildCaret(false);
        public static Sprite Ruler => _ruler ??= BuildRuler();
        public static Sprite Bulb => _bulb ??= BuildBulb();
        public static Sprite Sun => _sun ??= BuildSun();
        public static Sprite Eyedropper => _eyedropper ??= BuildEyedropper();
        public static Sprite Crosshair => _crosshair ??= BuildCrosshair();
        public static Sprite Note => _note ??= BuildNote();
        public static Sprite Play => _play ??= BuildPlay();
        public static Sprite Pause => _pause ??= BuildPause();
        public static Sprite TrackNext => _trackNext ??= BuildSkip(true);
        public static Sprite TrackPrev => _trackPrev ??= BuildSkip(false);

        private static Sprite BuildGear()
        {
            var px = NewCanvas();
            int c = S / 2;
            Disc(px, c, c, 22, Ink);
            DrawGearTeeth(px, c);
            DrawGearBore(px, c);
            return Finish(px);
        }

        private static void DrawGearTeeth(Color32[] px, int c)
        {
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f;
                int tx = c + Mathf.RoundToInt(Mathf.Cos(a) * 24);
                int ty = c + Mathf.RoundToInt(Mathf.Sin(a) * 24);
                Rect(px, tx - 7, ty - 7, tx + 7, ty + 7, Ink);
            }
        }

        private static void DrawGearBore(Color32[] px, int c) => Disc(px, c, c, 9, Clear);

        private static Sprite BuildFloppy(bool saveAs)
        {
            var px = NewCanvas();
            DrawFloppyCase(px);
            DrawFloppyShutter(px);
            DrawFloppyLabel(px);
            if (saveAs) DrawSaveAsBadge(px);
            return Finish(px);
        }

        private static void DrawFloppyCase(Color32[] px) => Rect(px, 10, 8, 54, 56, Ink2);

        private static void DrawFloppyShutter(Color32[] px)
        {
            Rect(px, 16, 30, 48, 52, Ink);
            Rect(px, 30, 34, 44, 50, Ink2);
        }

        private static void DrawFloppyLabel(Color32[] px)
        {
            Rect(px, 16, 10, 48, 26, Ink);
            Rect(px, 20, 13, 44, 23, Ink2);
        }

        private static void DrawSaveAsBadge(Color32[] px)
        {
            Disc(px, 50, 50, 11, Accent);
            Rect(px, 48, 44, 52, 56, Ink);
            Rect(px, 44, 48, 56, 52, Ink);
        }

        private static Sprite BuildFolder()
        {
            var px = NewCanvas();
            DrawFolderTab(px);
            DrawFolderBody(px);
            DrawFolderFoldLine(px);
            return Finish(px);
        }

        private static void DrawFolderTab(Color32[] px) => Rect(px, 8, 40, 30, 50, Ink2);

        private static void DrawFolderBody(Color32[] px) => Rect(px, 7, 12, 57, 44, Ink);

        private static void DrawFolderFoldLine(Color32[] px) => Rect(px, 7, 40, 57, 43, Ink2);

        private static Sprite BuildCircularArrow(bool redo)
        {
            const int arrowheadOnTheLeft = 17, arrowheadOnTheRight = 47;
            var px = NewCanvas();
            Arc(px, 32, 28, 15, 10f, 170f, 3, Ink);
            ArrowDown(px, redo ? arrowheadOnTheRight : arrowheadOnTheLeft, 32, 11, Ink);
            return Finish(px);
        }

        private static Sprite BuildPin()
        {
            var px = NewCanvas();
            DrawPinHead(px);
            DrawPinNeedle(px);
            return Finish(px);
        }

        private static void DrawPinHead(Color32[] px)
        {
            Disc(px, 32, 44, 12, Ink);
            Disc(px, 32, 46, 5, Ink2);
        }

        private static void DrawPinNeedle(Color32[] px) => Rect(px, 30, 12, 35, 44, Ink);

        private static Sprite BuildWarning()
        {
            var tintedByTheCallerThroughImageColor = Color.white;
            var px = NewCanvas();
            TriangleUp(px, 32, 8, 48, 27, tintedByTheCallerThroughImageColor);
            PunchExclamationMark(px);
            return Finish(px);
        }

        private static void PunchExclamationMark(Color32[] px)
        {
            Rect(px, 30, 24, 35, 40, Clear);
            Disc(px, 32, 19, 3, Clear);
        }

        private static Sprite BuildPencil()
        {
            var px = NewCanvas();
            DrawPencilBodyAlongTheDiagonal(px);
            DrawPencilFerrule(px);
            DrawPencilTip(px);
            return Finish(px);
        }

        private static void DrawPencilBodyAlongTheDiagonal(Color32[] px) =>
            Line(px, 20, 20, 46, 46, 5, Ink);

        private static void DrawPencilFerrule(Color32[] px) => Line(px, 41, 41, 47, 47, 5, Ink2);

        private static void DrawPencilTip(Color32[] px)
        {
            Line(px, 15, 15, 19, 19, 3, Ink2);
            Disc(px, 14, 14, 3, Ink);
        }

        private static Sprite BuildCaret(bool up)
        {
            const int halfW = 26;
            const int height = 40;
            const int marginFromTheShortEdge = 12;
            var px = NewCanvas();
            int baseY = up ? marginFromTheShortEdge : S - marginFromTheShortEdge;
            int step = up ? 1 : -1;
            for (int i = 0; i <= height; i++)
            {
                int y = baseY + i * step;
                int half = Mathf.RoundToInt(halfW * (1f - i / (float)height));
                Rect(px, 32 - half, y, 32 + half + 1, y + 1, Ink);
            }
            return Finish(px);
        }

        private static Sprite BuildRuler()
        {
            var px = NewCanvas();
            DrawRulerBody(px);
            DrawRulerNotches(px);
            return Finish(px);
        }

        private static void DrawRulerBody(Color32[] px) => Rect(px, 6, 23, 58, 41, Ink);

        private static void DrawRulerNotches(Color32[] px)
        {
            for (int x = 13; x <= 51; x += 8)
                Rect(px, x, 33, x + 3, 41, Ink2);
        }

        private static Sprite BuildBulb()
        {
            var px = NewCanvas();
            DrawBulbGlass(px);
            DrawBulbNeck(px);
            DrawBulbSocket(px);
            return Finish(px);
        }

        private static void DrawBulbGlass(Color32[] px) => Disc(px, 32, 41, 15, Ink);

        private static void DrawBulbNeck(Color32[] px) => Rect(px, 25, 26, 40, 42, Ink);

        private static void DrawBulbSocket(Color32[] px)
        {
            Rect(px, 24, 13, 41, 26, Ink2);
            Rect(px, 24, 22, 41, 24, Ink);
            Rect(px, 24, 17, 41, 19, Ink);
        }

        private static Sprite BuildSun()
        {
            var px = NewCanvas();
            DrawSunDisc(px);
            DrawSunRays(px);
            return Finish(px);
        }

        private static void DrawSunDisc(Color32[] px) => Disc(px, 32, 32, 13, Ink);

        private static void DrawSunRays(Color32[] px)
        {
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f;
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                Line(px,
                    32 + Mathf.RoundToInt(cos * 19), 32 + Mathf.RoundToInt(sin * 19),
                    32 + Mathf.RoundToInt(cos * 27), 32 + Mathf.RoundToInt(sin * 27),
                    2, Ink);
            }
        }

        private static Sprite BuildCrosshair()
        {
            var px = NewCanvas();
            DrawCrosshairRing(px);
            DrawCrosshairArms(px);
            Disc(px, 32, 32, 3, Accent);
            return Finish(px);
        }

        private static void DrawCrosshairRing(Color32[] px)
        {
            Disc(px, 32, 32, 20, Ink);
            Disc(px, 32, 32, 16, Clear);
        }

        private static void DrawCrosshairArms(Color32[] px)
        {
            Line(px, 32, 5, 32, 27, 3, Ink);
            Line(px, 32, 37, 32, 59, 3, Ink);
            Line(px, 5, 32, 27, 32, 3, Ink);
            Line(px, 37, 32, 59, 32, 3, Ink);
        }

        private static Sprite BuildEyedropper()
        {
            var px = NewCanvas();
            DrawEyedropperBulb(px);
            DrawEyedropperUprightBarrel(px);
            DrawEyedropperCollar(px);
            DrawEyedropperTip(px);
            return Finish(px);
        }

        private static void DrawEyedropperBulb(Color32[] px) => Disc(px, 32, 49, 12, Ink2);

        private static void DrawEyedropperUprightBarrel(Color32[] px) =>
            Rect(px, 26, 20, 39, 48, Ink);

        private static void DrawEyedropperCollar(Color32[] px) => Rect(px, 23, 36, 42, 40, Ink2);

        private static void DrawEyedropperTip(Color32[] px) => ArrowDown(px, 32, 21, 13, Ink);

        private static Sprite BuildNote()
        {
            var px = NewCanvas();
            DrawNoteHead(px);
            DrawNoteStem(px);
            DrawNoteFlag(px);
            return Finish(px);
        }

        private static void DrawNoteHead(Color32[] px) => Disc(px, 24, 18, 11, Ink);

        private static void DrawNoteStem(Color32[] px) => Rect(px, 32, 18, 38, 54, Ink);

        private static void DrawNoteFlag(Color32[] px)
        {
            Line(px, 36, 52, 50, 40, 3, Ink2);
            Line(px, 36, 44, 48, 34, 3, Ink2);
        }

        private static Sprite BuildPlay()
        {
            var px = NewCanvas();
            TriangleRight(px, 20, 48, 32, 20, Ink);
            return Finish(px);
        }

        private static Sprite BuildPause()
        {
            var px = NewCanvas();
            Rect(px, 20, 14, 29, 50, Ink);
            Rect(px, 35, 14, 44, 50, Ink);
            return Finish(px);
        }

        private static Sprite BuildSkip(bool forward)
        {
            var px = NewCanvas();
            if (forward)
            {
                TriangleRight(px, 14, 40, 32, 18, Ink);
                Rect(px, 43, 14, 50, 50, Ink2);
            }
            else
            {
                TriangleLeft(px, 24, 50, 32, 18, Ink);
                Rect(px, 14, 14, 21, 50, Ink2);
            }
            return Finish(px);
        }

        private static void TriangleRight(Color32[] px, int x0, int x1, int cy, int halfH, Color col)
        {
            int w = x1 - x0;
            if (w <= 0) return;
            for (int x = x0; x <= x1; x++)
            {
                int half = Mathf.RoundToInt(halfH * (1f - (x - x0) / (float)w));
                Rect(px, x, cy - half, x + 1, cy + half + 1, col);
            }
        }

        private static void TriangleLeft(Color32[] px, int x0, int x1, int cy, int halfH, Color col)
        {
            int w = x1 - x0;
            if (w <= 0) return;
            for (int x = x0; x <= x1; x++)
            {
                int half = Mathf.RoundToInt(halfH * ((x - x0) / (float)w));
                Rect(px, x, cy - half, x + 1, cy + half + 1, col);
            }
        }

        private static void Line(Color32[] px, int x0, int y0, int x1, int y1, int thick, Color col)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            for (int i = 0; i <= steps; i++)
            {
                float t = steps == 0 ? 0f : i / (float)steps;
                Disc(px, Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)),
                    Mathf.RoundToInt(Mathf.Lerp(y0, y1, t)), thick, col);
            }
        }

        private static void Arc(Color32[] px, int cx, int cy, int r, float fromDeg, float toDeg, int thick, Color col)
        {
            for (float a = fromDeg; a <= toDeg; a += 1.2f)
            {
                float rad = a * Mathf.Deg2Rad;
                int x = cx + Mathf.RoundToInt(Mathf.Cos(rad) * r);
                int y = cy + Mathf.RoundToInt(Mathf.Sin(rad) * r);
                Disc(px, x, y, thick, col);
            }
        }

        private static void ArrowDown(Color32[] px, int tx, int baseY, int h, Color col)
        {
            for (int i = 0; i <= h; i++)
            {
                int yy = baseY - i;
                int half = Mathf.RoundToInt((h - i) * 0.55f);
                Rect(px, tx - half, yy, tx + half + 1, yy + 1, col);
            }
        }

        private static void TriangleUp(Color32[] px, int cx, int baseY, int apexY, int halfW, Color col)
        {
            int h = apexY - baseY;
            if (h <= 0) return;
            for (int y = baseY; y <= apexY; y++)
            {
                int half = Mathf.RoundToInt(halfW * (1f - (y - baseY) / (float)h));
                Rect(px, cx - half, y, cx + half + 1, y + 1, col);
            }
        }

        private static Color32[] NewCanvas()
        {
            var px = new Color32[S * S];
            for (int i = 0; i < px.Length; i++) px[i] = Clear;
            return px;
        }

        private static void Rect(Color32[] px, int x0, int y0, int x1, int y1, Color col)
        {
            x0 = Mathf.Clamp(x0, 0, S); y0 = Mathf.Clamp(y0, 0, S);
            x1 = Mathf.Clamp(x1, 0, S); y1 = Mathf.Clamp(y1, 0, S);
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    px[y * S + x] = col;
        }

        private static void Disc(Color32[] px, int cx, int cy, int r, Color col)
        {
            int r2 = r * r;
            for (int y = cy - r; y <= cy + r; y++)
            {
                if (y < 0 || y >= S) continue;
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || x >= S) continue;
                    int dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy <= r2) px[y * S + x] = col;
                }
            }
        }

        private static Sprite Finish(Color32[] px)
        {
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
