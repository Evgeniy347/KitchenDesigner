using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Грань коробки, на которую кладётся накладка текстуры. A…F — это
    /// индексы 0…5 из <see cref="KitchenElement.GetFaces"/> (index/2 = ось,
    /// чётный = положительное направление), <see cref="All"/> — все шесть сразу.
    ///
    /// Буквы, а не «Перед/Зад/Верх»: стена может стоять как угодно и быть
    /// повёрнутой, поэтому словесное название грани врало бы ровно там, где оно
    /// нужнее всего. Какая грань какая, показывает подсветка при наведении на
    /// пункт списка (SideHighlighter.ShowFace).</summary>
    public enum OverlaySide
    {
        A = 0,
        B = 1,
        C = 2,
        D = 3,
        E = 4,
        F = 5,
        All = 6,
    }

    /// <summary>Одна накладка текстуры: декор из каталога на прямоугольной
    /// области одной грани.
    ///
    /// Область задаётся в плоскости грани, в мм, от её ЛЕВОГО НИЖНЕГО угла
    /// (оси — <c>Face.rightAxis</c> и <c>Face.upAxis</c>). Нулевой размер
    /// означает «во всю грань»: так накладка по умолчанию покрывает сторону
    /// целиком и продолжает покрывать её после ресайза элемента, без отдельного
    /// флага и без пересчёта каждой области на каждое изменение габарита.
    ///
    /// Растяжение области НЕ масштабирует картинку: декор всегда показывается в
    /// своём физическом масштабе (MaterialDef.tileSizeMM), а область — окно, в
    /// котором он повторяется и обрезается.</summary>
    [System.Serializable]
    public struct TextureOverlaySpec : System.IEquatable<TextureOverlaySpec>
    {
        /// <summary>Наименьшая область, которую можно оставить ручками, мм.
        /// Меньше — накладку уже не подцепить и не разглядеть.</summary>
        public const int MIN_SIZE_MM = 10;

        public OverlaySide side;
        public string materialId;
        public int u0MM, v0MM;
        public int widthMM, heightMM;

        public TextureOverlaySpec(OverlaySide side, string? materialId,
            int u0MM = 0, int v0MM = 0, int widthMM = 0, int heightMM = 0)
        {
            this.side = side;
            this.materialId = string.IsNullOrEmpty(materialId) ? MaterialCatalog.DefaultId : materialId!;
            this.u0MM = u0MM;
            this.v0MM = v0MM;
            this.widthMM = widthMM;
            this.heightMM = heightMM;
        }

        /// <summary>Накладка во всю грань (то, что получается при добавлении).</summary>
        public static TextureOverlaySpec FullFace(OverlaySide side, string? materialId)
            => new TextureOverlaySpec(side, materialId);

        /// <summary>Область не задана явно — накладка тянется по всей грани.</summary>
        public bool IsFullFace => widthMM <= 0 || heightMM <= 0;

        public string MaterialId =>
            string.IsNullOrEmpty(materialId) ? MaterialCatalog.DefaultId : materialId;

        /// <summary>Фактическая область на грани размером faceMM: «во всю грань»
        /// разворачивается в полный прямоугольник, явная область подрезается по
        /// границам грани. Пустой результат (нулевая ширина/высота) означает, что
        /// накладка целиком уехала за грань — рисовать нечего.</summary>
        public RectInt Resolve(Vector2Int faceMM)
        {
            int fw = Mathf.Max(0, faceMM.x);
            int fh = Mathf.Max(0, faceMM.y);
            if (IsFullFace) return new RectInt(0, 0, fw, fh);

            int x0 = Mathf.Clamp(u0MM, 0, fw);
            int y0 = Mathf.Clamp(v0MM, 0, fh);
            int x1 = Mathf.Clamp(u0MM + widthMM, 0, fw);
            int y1 = Mathf.Clamp(v0MM + heightMM, 0, fh);
            return new RectInt(x0, y0, Mathf.Max(0, x1 - x0), Mathf.Max(0, y1 - y0));
        }

        /// <summary>Та же накладка с явно заданной областью (ручки превращают
        /// «во всю грань» в конкретный прямоугольник при первом же перетаскивании).</summary>
        public TextureOverlaySpec WithRect(RectInt rect)
            => new TextureOverlaySpec(side, materialId, rect.x, rect.y,
                Mathf.Max(MIN_SIZE_MM, rect.width), Mathf.Max(MIN_SIZE_MM, rect.height));

        public TextureOverlaySpec WithSide(OverlaySide newSide)
            => new TextureOverlaySpec(newSide, materialId, u0MM, v0MM, widthMM, heightMM);

        public TextureOverlaySpec WithMaterial(string? newMaterialId)
            => new TextureOverlaySpec(side, newMaterialId, u0MM, v0MM, widthMM, heightMM);

        public static string SideLabel(OverlaySide side) => side switch
        {
            OverlaySide.A => "A",
            OverlaySide.B => "B",
            OverlaySide.C => "C",
            OverlaySide.D => "D",
            OverlaySide.E => "E",
            OverlaySide.F => "F",
            _ => "(все)",
        };

        public override string ToString() =>
            $"{SideLabel(side)}:{MaterialId}" + (IsFullFace ? "" : $"@{u0MM},{v0MM}+{widthMM}×{heightMM}");

        public bool Equals(TextureOverlaySpec other) =>
            side == other.side && MaterialId == other.MaterialId
            && u0MM == other.u0MM && v0MM == other.v0MM
            && widthMM == other.widthMM && heightMM == other.heightMM;

        public override bool Equals(object? obj) => obj is TextureOverlaySpec other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + (int)side;
                h = h * 31 + MaterialId.GetHashCode();
                h = h * 31 + u0MM;
                h = h * 31 + v0MM;
                h = h * 31 + widthMM;
                h = h * 31 + heightMM;
                return h;
            }
        }
    }

    /// <summary>Общая геометрия накладок: перевод стороны в индекс грани и
    /// размер грани в мм. Отдельно от <see cref="TextureOverlaySpec"/>, потому
    /// что этим пользуются и рендер, и ручки, и UI.</summary>
    public static class TextureOverlayGeometry
    {
        /// <summary>Сколько накладок помещается в окно свойств одной строкой на
        /// каждую. Ограничение прикладное, а не техническое: длинный список в
        /// инспекторе всё равно нечитаем.</summary>
        public const int MAX_PER_ELEMENT = 12;

        public const int FACE_COUNT = 6;

        /// <summary>Индексы граней стороны: у A…F — одна, у «(все)» — все шесть.</summary>
        public static int[] FaceIndices(OverlaySide side) =>
            side == OverlaySide.All
                ? new[] { 0, 1, 2, 3, 4, 5 }
                : new[] { (int)side };

        /// <summary>Размер грани в мм: index/2 = ось, поперёк которой грань.
        /// Порядок осей грани тот же, что у <c>Face.rightAxis</c>/<c>upAxis</c>
        /// в <see cref="KitchenElement.GetFaces"/> — иначе область легла бы боком.</summary>
        public static Vector2Int FaceSizeMM(Vector3Int dimsMM, int faceIndex) => (faceIndex / 2) switch
        {
            0 => new Vector2Int(dimsMM.y, dimsMM.z),
            1 => new Vector2Int(dimsMM.x, dimsMM.z),
            _ => new Vector2Int(dimsMM.x, dimsMM.y),
        };
    }
}
