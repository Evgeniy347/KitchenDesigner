using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    /// <summary>
    /// Процедурные иконки для тулбара (без графических ассетов): рисуются в
    /// Texture2D и оборачиваются в Sprite. Кэшируются — создаются один раз.
    /// </summary>
    public static class IconFactory
    {
        private const int S = 64;
        private static readonly Color Ink = new Color(0.90f, 0.91f, 0.95f, 1f);
        private static readonly Color Ink2 = new Color(0.62f, 0.66f, 0.78f, 1f);
        private static readonly Color Accent = new Color(0.45f, 0.85f, 0.5f, 1f);
        private static readonly Color Clear = new Color(0, 0, 0, 0);

        private static Sprite? _gear, _floppy, _floppyPlus, _folder, _undo, _redo, _pin, _warning;

        public static Sprite Gear => _gear ??= BuildGear();
        public static Sprite Floppy => _floppy ??= BuildFloppy(false);
        public static Sprite FloppyPlus => _floppyPlus ??= BuildFloppy(true);
        public static Sprite Folder => _folder ??= BuildFolder();
        public static Sprite Undo => _undo ??= BuildArrow(false);
        public static Sprite Redo => _redo ??= BuildArrow(true);
        public static Sprite Pin => _pin ??= BuildPin();
        public static Sprite Warning => _warning ??= BuildWarning();

        // --- Иконки ---

        private static Sprite BuildGear()
        {
            var px = NewCanvas();
            int c = S / 2;
            Disc(px, c, c, 22, Ink);
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f;
                int tx = c + Mathf.RoundToInt(Mathf.Cos(a) * 24);
                int ty = c + Mathf.RoundToInt(Mathf.Sin(a) * 24);
                Rect(px, tx - 7, ty - 7, tx + 7, ty + 7, Ink);
            }
            Disc(px, c, c, 9, Clear); // отверстие в центре
            return Finish(px);
        }

        private static Sprite BuildFloppy(bool plus)
        {
            var px = NewCanvas();
            Rect(px, 10, 8, 54, 56, Ink2);          // корпус
            Rect(px, 16, 30, 48, 52, Ink);          // верхняя «шторка»
            Rect(px, 30, 34, 44, 50, Ink2);         // вырез шторки
            Rect(px, 16, 10, 48, 26, Ink);          // нижняя этикетка
            Rect(px, 20, 13, 44, 23, Ink2);         // полоски этикетки
            if (plus)
            {
                // зелёный бейдж «+» в углу — «сохранить как»
                Disc(px, 50, 50, 11, Accent);
                Rect(px, 48, 44, 52, 56, Ink);
                Rect(px, 44, 48, 56, 52, Ink);
            }
            return Finish(px);
        }

        private static Sprite BuildFolder()
        {
            var px = NewCanvas();
            Rect(px, 8, 40, 30, 50, Ink2);   // язычок
            Rect(px, 7, 12, 57, 44, Ink);    // корпус папки
            Rect(px, 7, 40, 57, 43, Ink2);   // линия сгиба
            return Finish(px);
        }

        // Круговая стрелка: дуга-«радуга» сверху и наконечник, свисающий с одного
        // конца. Слева (redo=false) — отмена, справа (redo=true) — повтор.
        private static Sprite BuildArrow(bool redo)
        {
            var px = NewCanvas();
            Arc(px, 32, 28, 15, 10f, 170f, 3, Ink);
            ArrowDown(px, redo ? 47 : 17, 32, 11, Ink);
            return Finish(px);
        }

        // Канцелярская булавка: круглая головка + игла вниз.
        private static Sprite BuildPin()
        {
            var px = NewCanvas();
            Disc(px, 32, 44, 12, Ink);    // головка
            Disc(px, 32, 46, 5, Ink2);    // блик на головке
            Rect(px, 30, 12, 35, 44, Ink); // игла
            return Finish(px);
        }

        // Предупреждающий треугольник с «!». Рисуется БЕЛЫМ (в отличие от прочих
        // иконок): цвет задаёт вызывающий через Image.color — красный при ошибках,
        // оранжевый при предупреждениях, серый когда кнопка неактивна.
        private static Sprite BuildWarning()
        {
            var px = NewCanvas();
            TriangleUp(px, 32, 8, 48, 27, Color.white);
            Rect(px, 30, 24, 35, 40, Clear);   // палочка «!»
            Disc(px, 32, 19, 3, Clear);        // точка «!»
            return Finish(px);
        }

        // --- Примитивы рисования ---

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

        // Треугольный наконечник, указывающий вниз: основание у baseY, вершина ниже на h.
        private static void ArrowDown(Color32[] px, int tx, int baseY, int h, Color col)
        {
            for (int i = 0; i <= h; i++)
            {
                int yy = baseY - i;
                int half = Mathf.RoundToInt((h - i) * 0.55f);
                Rect(px, tx - half, yy, tx + half + 1, yy + 1, col);
            }
        }

        // Заполненный треугольник вершиной вверх: основание шириной 2*halfW у baseY,
        // вершина в (cx, apexY).
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
