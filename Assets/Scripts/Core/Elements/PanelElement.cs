using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>ДВП/ХДФ — тонкая вкладная панель (задняя стенка, дно ящика).
    /// От обычной детали отличается только зазорами: как у фасада, они входят в
    /// ГАБАРИТ (см. GappedBox), но не в физический меш.
    ///
    /// Смысл: в паз заходит НОМИНАЛ панели — на всю глубину паза, — а
    /// технологический зазор (по умолчанию 1 мм) остаётся внутри детали. Поэтому
    /// прилипание считает панель «по проёму», а раскрой получает размер меньше
    /// на зазор. Дверцей панель не является и не открывается.</summary>
    public class PanelElement : KitchenElement
    {
        /// <summary>Технологический зазор по умолчанию, мм.</summary>
        public const int DEFAULT_GAP_MM = 1;

        public int GapLeft
        {
            get => Data.GapLeft;
            set { Data.GapLeft = value; ApplyDimensions(); }
        }

        public int GapRight
        {
            get => Data.GapRight;
            set { Data.GapRight = value; ApplyDimensions(); }
        }

        public int GapTop
        {
            get => Data.GapTop;
            set { Data.GapTop = value; ApplyDimensions(); }
        }

        public int GapBottom
        {
            get => Data.GapBottom;
            set { Data.GapBottom = value; ApplyDimensions(); }
        }

        public int GapMM => Data.GapMM;

        protected override Vector3 EffectiveScale => GappedBox.EffectiveScale(transform.localScale, Data.Gaps);

        public override Vector3[] GetVertices()
            => GappedBox.Vertices(transform.localScale, Data.Gaps, transform.position, transform.rotation);

        public override Face[] GetFaces()
            => GappedBox.Faces(transform.localScale, Data.Gaps, transform.position, transform.rotation);

        /// <summary>Проставить одинаковый зазор со всех четырёх сторон.</summary>
        public void SetUniformGap(int gapMM)
        {
            int g = Mathf.Max(0, gapMM);
            Data.GapLeft = g;
            Data.GapRight = g;
            Data.GapTop = g;
            Data.GapBottom = g;
            ApplyDimensions();
        }
    }
}
