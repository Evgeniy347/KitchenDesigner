using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>ДВП/ХДФ — тонкая вкладная панель (задняя стенка, дно ящика).
    /// От обычной детали отличается только величиной зазоров: механика у них
    /// общая (KitchenElement + GappedBox) — они входят в ГАБАРИТ, но не в
    /// физический меш.
    ///
    /// Смысл: в паз заходит НОМИНАЛ панели — на всю глубину паза, — а
    /// технологический зазор (по умолчанию 1 мм со всех шести сторон) остаётся
    /// внутри детали. Поэтому прилипание считает панель «по проёму», а раскрой
    /// получает размер меньше на зазор. Дверцей панель не является и не
    /// открывается.</summary>
    public class PanelElement : KitchenElement
    {

        public override string DisplayTypeName => "ДВП/ХДФ";
        /// <summary>Технологический зазор по умолчанию, мм.</summary>
        public const int DEFAULT_GAP_MM = 1;

        /// <summary>Зазоры есть, хотя панель — не базовая «Деталь»
        /// (SupportsGrooves у неё false).</summary>
        public override bool SupportsGaps => true;

        /// <summary>Проставить одинаковый зазор со всех шести сторон.</summary>
        public void SetUniformGap(int gapMM)
        {
            int g = Mathf.Max(0, gapMM);
            foreach (var side in GapSides.All) Data.SetGap(side, g);
            ApplyDimensions();
        }
    }
}
