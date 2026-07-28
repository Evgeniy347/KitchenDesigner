using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>
    /// Подсветка стороны под кромку прямо на детали.
    ///
    /// В окне свойств стороны обозначены как L1/L2/W1/W2 на плоской схеме, и по
    /// ней невозможно понять, какой торец имеется в виду в реальной сцене —
    /// особенно если камера смотрит с другой стороны. При наведении на полосу
    /// схемы этот класс кладёт поверх детали светло-жёлтые накладки: сам торец
    /// целиком плюс полоса на каждой из четырёх соседних граней. Полосы нужны
    /// именно для того, чтобы сторону было видно с ЛЮБОГО ракурса: даже когда
    /// торец смотрит от камеры, его выдаёт жёлтая кайма на видимых гранях.
    ///
    /// Накладки — отдельные квады поверх поверхности, геометрия детали не
    /// трогается. Живут только пока курсор на полосе.
    /// </summary>
    public static class EdgeSideHighlighter
    {
        /// <summary>Насколько глубоко полоса заходит на соседнюю грань — доля
        /// её размера в этом направлении.</summary>
        public const float BandFraction = 0.2f;

        /// <summary>Потолок глубины полосы, мм. На крупной детали 20 % — это
        /// пол-пласти: кайма перестаёт читаться как кайма.</summary>
        public const float BandMaxMm = 50f;

        /// <summary>Зазор над поверхностью, чтобы накладку не съедал z-fighting
        /// с самой деталью (0.2 мм — меньше любого видимого сдвига).</summary>
        private const float LiftMm = 0.2f;

        private static readonly List<GameObject> Quads = new List<GameObject>();
        private static Material? _material;
        private static KitchenElement? _shownFor;
        private static EdgeSide _shownSide;

        /// <summary>Что подсвечено сейчас (для тестов и повторных наведений).</summary>
        public static bool IsShown(KitchenElement element, EdgeSide side) =>
            _shownFor == element && _shownSide == side && Quads.Count > 0;

        /// <summary>Количество накладок: 1 торец + 4 полосы. Меньше — если у
        /// детали не разобран габарит.</summary>
        public static int QuadCount => Quads.Count;

        public static void Show(KitchenElement element, EdgeSide side)
        {
            Hide();
            if (element == null) return;

            var layout = EdgeBanding.LayoutOf(element.DimensionsMM);
            if (!layout.IsValid) return;

            var faces = element.GetFaces();
            int endIndex = layout.FaceIndex(side);
            var end = faces[endIndex];

            // Сам торец — целиком.
            AddQuad(element, end, end.size, Vector2.zero);

            // Соседние грани — все, кроме самого торца и противоположного ему.
            int oppositeIndex = endIndex % 2 == 0 ? endIndex + 1 : endIndex - 1;
            for (int i = 0; i < faces.Length; i++)
            {
                if (i == endIndex || i == oppositeIndex) continue;
                AddBand(element, faces[i], end);
            }

            _shownFor = element;
            _shownSide = side;
        }

        public static void Hide()
        {
            foreach (var q in Quads)
                if (q != null) Object.DestroyImmediate(q);
            Quads.Clear();
            _shownFor = null;
        }

        /// <summary>Полоса вдоль общего ребра соседней грани с торцом: она
        /// прижата к торцу и уходит вглубь на BandFraction размера ЭТОЙ грани в
        /// направлении от торца, но не больше BandMaxMm.</summary>
        private static void AddBand(KitchenElement element, in KitchenElement.Face face,
            in KitchenElement.Face end)
        {
            // Направление «от торца» внутри плоскости соседней грани — это
            // нормаль торца, спроецированная на плоскость грани. Для граней
            // прямоугольной детали она совпадает с одной из осей грани.
            float alongRight = Vector3.Dot(end.normal, face.rightAxis);
            float alongUp = Vector3.Dot(end.normal, face.upAxis);
            bool alongU = Mathf.Abs(alongRight) >= Mathf.Abs(alongUp);

            float faceSpan = alongU ? face.size.x : face.size.y;
            float depth = Mathf.Min(faceSpan * BandFraction, BandMaxMm * AppConstants.MM_TO_UNITS);
            if (depth <= 0f) return;

            // Полоса прижата к тому краю грани, который граничит с торцом:
            // знак проекции говорит, к какому именно.
            float sign = alongU ? Mathf.Sign(alongRight) : Mathf.Sign(alongUp);
            float offset = (faceSpan - depth) * 0.5f * sign;

            var size = alongU ? new Vector2(depth, face.size.y) : new Vector2(face.size.x, depth);
            var shift = alongU ? new Vector2(offset, 0f) : new Vector2(0f, offset);
            AddQuad(element, face, size, shift);
        }

        /// <summary>Накладка на грань: размер в юнитах и сдвиг от центра грани
        /// в её собственных осях.</summary>
        private static void AddQuad(KitchenElement element, in KitchenElement.Face face,
            Vector2 size, Vector2 shiftInFace)
        {
            var go = new GameObject("EdgeSideHighlight");
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(element.transform, worldPositionStays: true);

            Vector3 center = face.center
                + face.rightAxis * shiftInFace.x
                + face.upAxis * shiftInFace.y
                + face.normal * (LiftMm * AppConstants.MM_TO_UNITS);

            go.transform.position = center;
            go.transform.rotation = Quaternion.LookRotation(-face.normal, face.upAxis);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = QuadMesh();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = HighlightMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Quads.Add(go);
        }

        private static Mesh? _quad;

        /// <summary>Единичный квад в плоскости XY. Свой, а не CreatePrimitive:
        /// примитивы вырезаются из WebGL-сборки вместе с коллайдерами.</summary>
        private static Mesh QuadMesh()
        {
            if (_quad != null) return _quad;
            _quad = new Mesh { name = "EdgeSideHighlightQuad", hideFlags = HideFlags.DontSave };
            _quad.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
            };
            _quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _quad.normals = new[] { -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward };
            _quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            return _quad;
        }

        private static Material HighlightMaterial()
        {
            if (_material != null) return _material;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            _material = new Material(shader) { hideFlags = HideFlags.DontSave };
            _material.color = UI.UIStyle.EdgeHighlight3D;
            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", UI.UIStyle.EdgeHighlight3D);
            // Прозрачность: под накладкой должна читаться текстура детали.
            if (_material.HasProperty("_Surface")) _material.SetFloat("_Surface", 1f);
            _material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return _material;
        }
    }
}
