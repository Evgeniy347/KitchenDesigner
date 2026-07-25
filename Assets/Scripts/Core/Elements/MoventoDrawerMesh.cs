using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Процедурная геометрия и раскрой деревянного ящика на направляющих
    /// Blum MOVENTO. В отличие от GTV (металлический покупной короб, одна строка
    /// спецификации) короб Movento — пять плитных деталей 16 мм: 2 боковины, перед,
    /// задник и дно. Каждая деталь уходит в спецификацию отдельной позицией
    /// (см. DrawerElement.GetSpecParts / SpecificationManager), но самостоятельным
    /// элементом сцены НЕ является — раскрой полностью автоматический.
    ///
    /// Формулы Blum «Building a MOVENTO drawer» (плита 16 мм):
    ///   • наружная ширина короба SKW = LW − 42 (зазор направляющих 21 мм на сторону);
    ///   • длина боковины = NL − 10;
    ///   • перед и задник встают МЕЖДУ боковин: ширина = SKW − 2·16 = LW − 74;
    ///   • дно лежит заподлицо с низом боковин (требование скрытой направляющей).
    ///
    /// Панели задаются в мм от левого-нижнего-ЗАДНЕГО угла контурного бокса
    /// LW × минПроём × NL (тот же контур, что у GTV, — снэп/коллайдер общие).</summary>
    public static class MoventoDrawerMesh
    {
        /// <summary>Суффиксы деталей для спецификации (добавляются к имени ящика).</summary>
        public const string SUFFIX_SIDE = "Боковина";
        public const string SUFFIX_FRONT = "Перед";
        public const string SUFFIX_BACK = "Задник";
        public const string SUFFIX_BOTTOM = "Дно";

        /// <summary>Раскладка короба на 5 панелей по формулам Movento.</summary>
        public static List<DrawerMesh.Box> ComputeBoxes(int lwMM, DrawerType type, int nlMM)
        {
            float w = lwMM, d = nlMM;
            float t = DrawerConstants.MOVENTO_BOARD_THICKNESS;
            float clr = DrawerConstants.MOVENTO_SLIDE_CLEARANCE_PER_SIDE;
            float lift = DrawerConstants.GetBottomLift(type);
            float h = DrawerConstants.GetTypeHeight(type);

            float sideLen = d - DrawerConstants.MOVENTO_SIDE_LENGTH_INSET; // NL − 10
            float fbW = w - DrawerConstants.MOVENTO_FRONT_BACK_INSET;      // LW − 74
            float bottomD = sideLen - 2f * t;                             // между передом и задником
            float backZ = d - sideLen;                                    // задняя грань короба
            float innerX = clr + t;                                       // внутренняя грань левой боковины

            return new List<DrawerMesh.Box>
            {
                // Боковины: наружная грань на 21 мм от стенки проёма, толщина 16 мм внутрь.
                new DrawerMesh.Box
                {
                    name = SUFFIX_SIDE + " L",
                    minMM = new Vector3(clr, lift, backZ),
                    sizeMM = new Vector3(t, h, sideLen),
                },
                new DrawerMesh.Box
                {
                    name = SUFFIX_SIDE + " R",
                    minMM = new Vector3(w - clr - t, lift, backZ),
                    sizeMM = new Vector3(t, h, sideLen),
                },
                // Перед: между боковин, у переднего торца (ящик выезжает в +Z).
                new DrawerMesh.Box
                {
                    name = SUFFIX_FRONT,
                    minMM = new Vector3(innerX, lift, d - t),
                    sizeMM = new Vector3(fbW, h, t),
                },
                // Задник: между боковин, у заднего торца короба.
                new DrawerMesh.Box
                {
                    name = SUFFIX_BACK,
                    minMM = new Vector3(innerX, lift, backZ),
                    sizeMM = new Vector3(fbW, h, t),
                },
                // Дно: между боковин и между передом/задником, заподлицо с низом.
                new DrawerMesh.Box
                {
                    name = SUFFIX_BOTTOM,
                    minMM = new Vector3(innerX, lift, backZ + t),
                    sizeMM = new Vector3(fbW, t, bottomD),
                },
            };
        }

        /// <summary>Меш короба Movento (тот же нормализованный контур LW × минПроём × NL).</summary>
        public static Mesh Build(int lwMM, DrawerType type, int nlMM)
        {
            var dims = new Vector3(
                Mathf.Max(1, lwMM),
                Mathf.Max(1, DrawerConstants.GetMinOpeningHeight(type)),
                Mathf.Max(1, nlMM));
            return DrawerMesh.BuildFromBoxes(ComputeBoxes(lwMM, type, nlMM), dims);
        }

        /// <summary>Раскладка короба на детали для спецификации/раскроя. Размеры —
        /// натуральные габариты плиты (мм): боковина — длина×высота×толщина, перед и
        /// задник — ширина×высота×толщина, дно — ширина×глубина×толщина. Одинаковые
        /// детали (2 боковины) сгруппируются в CSV сами по размеру.</summary>
        public static IEnumerable<AssembledFacadeMesh.Part> ComputeParts(int lwMM, DrawerType type, int nlMM)
        {
            int t = DrawerConstants.MOVENTO_BOARD_THICKNESS;
            int h = DrawerConstants.GetTypeHeight(type);
            int sideLen = Mathf.Max(1, nlMM - DrawerConstants.MOVENTO_SIDE_LENGTH_INSET);
            int fbW = Mathf.Max(1, lwMM - DrawerConstants.MOVENTO_FRONT_BACK_INSET);
            int bottomD = Mathf.Max(1, sideLen - 2 * t);

            return new List<AssembledFacadeMesh.Part>
            {
                new AssembledFacadeMesh.Part { suffix = SUFFIX_SIDE, dimsMM = new Vector3Int(sideLen, h, t) },
                new AssembledFacadeMesh.Part { suffix = SUFFIX_SIDE, dimsMM = new Vector3Int(sideLen, h, t) },
                new AssembledFacadeMesh.Part { suffix = SUFFIX_FRONT, dimsMM = new Vector3Int(fbW, h, t) },
                new AssembledFacadeMesh.Part { suffix = SUFFIX_BACK, dimsMM = new Vector3Int(fbW, h, t) },
                new AssembledFacadeMesh.Part { suffix = SUFFIX_BOTTOM, dimsMM = new Vector3Int(fbW, bottomD, t) },
            };
        }
    }
}
