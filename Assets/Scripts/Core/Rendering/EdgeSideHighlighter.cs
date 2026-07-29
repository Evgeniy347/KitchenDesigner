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
    ///
    /// Квады НЕ парентятся к детали: у KitchenElement в transform.localScale
    /// лежит физический габарит (см. ApplyDimensions), и накладка-ребёнок
    /// домножалась бы на него — по тонкой оси в 0.018 раза, то есть в ноль, да
    /// ещё с перекосом от неравномерного масштаба. Держим их под собственным
    /// корнем в мировых координатах, как ElementOutline.
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
        private static GameObject? _root;
        private static KitchenElement? _shownFor;
        private static EdgeSide _shownSide;
        // Индекс грани для ShowFace; −1 — показана сторона под кромку (Show).
        private static int _shownFaceIndex = -1;
        // Поза и габарит детали на момент построения: по ним Sync() понимает,
        // что накладки разъехались с деталью и их надо пересобрать.
        private static Vector3 _shownPos;
        private static Quaternion _shownRot;
        private static Vector3Int _shownDims;

        /// <summary>Что подсвечено сейчас (для тестов и повторных наведений).</summary>
        public static bool IsShown(KitchenElement element, EdgeSide side) =>
            _shownFor == element && _shownFaceIndex < 0 && _shownSide == side && Quads.Count > 0;

        /// <summary>Показана ли ИМЕННО грань (ShowFace), а не сторона под кромку.</summary>
        public static bool IsFaceShown(KitchenElement element, int faceIndex) =>
            _shownFor == element && _shownFaceIndex == faceIndex && Quads.Count > 0;

        /// <summary>Количество накладок: 1 торец + 4 полосы. Меньше — если у
        /// детали не разобран габарит.</summary>
        public static int QuadCount => Quads.Count;

        /// <summary>Сами накладки — тестам нужно проверять их МИРОВОЙ размер:
        /// на этом класс и горел (накладка наследовала габарит детали и
        /// схлопывалась по тонкой оси), а счётчик квадов такое не ловит.
        /// Первая в списке — торец, остальные четыре — полосы.</summary>
        public static IReadOnlyList<GameObject> QuadObjects => Quads;

        public static void Show(KitchenElement element, EdgeSide side)
        {
            Hide();
            if (element == null) return;

            var layout = EdgeBanding.LayoutOf(element.DimensionsMM);
            if (!layout.IsValid) return;

            // Материал добываем ДО построения накладок: без него квады рисовались
            // бы стандартным розовым «нет материала», что хуже отсутствия подсветки.
            if (HighlightMaterial() == null) return;

            var faces = element.GetFaces();
            int endIndex = layout.FaceIndex(side);
            var end = faces[endIndex];

            // Сам торец — целиком.
            AddQuad(end, end.size, Vector2.zero);

            // Соседние грани — все, кроме самого торца и противоположного ему.
            int oppositeIndex = endIndex % 2 == 0 ? endIndex + 1 : endIndex - 1;
            for (int i = 0; i < faces.Length; i++)
            {
                if (i == endIndex || i == oppositeIndex) continue;
                AddBand(faces[i], end);
            }

            _shownFor = element;
            _shownSide = side;
            _shownFaceIndex = -1;
            Remember(element);
        }

        /// <summary>Подсветить ОДНУ грань целиком, без каёмок на соседних.
        ///
        /// Так подсвечивается сторона под накладку текстуры: там сторона выбирается
        /// в выпадающем списке буквой (A…F), и человеку надо понять, какая это
        /// грань в сцене. Полосы на соседних гранях здесь только мешали бы — они
        /// нужны для торца, который сам по себе почти не виден, а грань стены
        /// видна и так.</summary>
        public static void ShowFace(KitchenElement element, int faceIndex)
        {
            Hide();
            if (element == null) return;
            if (HighlightMaterial() == null) return;

            var faces = element.GetFaces();
            if (faceIndex < 0 || faceIndex >= faces.Length) return;

            var face = faces[faceIndex];
            AddQuad(face, face.size, Vector2.zero);

            _shownFor = element;
            _shownFaceIndex = faceIndex;
            Remember(element);
        }

        private static void Remember(KitchenElement element)
        {
            _shownPos = element.transform.position;
            _shownRot = element.transform.rotation;
            _shownDims = element.DimensionsMM;
        }

        /// <summary>Догнать деталь: снять подсветку, если детали больше нет или
        /// её рендер погашен, и пересобрать накладки, если деталь сдвинули,
        /// повернули или изменили в размерах (undo, ввод в полях, MCP). Зовётся
        /// из ContextMenuUI.Update — своего апдейта у статического класса нет.</summary>
        public static void Sync()
        {
            if (Quads.Count == 0) return;

            if (_shownFor == null || !SceneVisibility.AnyRendererEnabled(_shownFor))
            {
                Hide();
                return;
            }

            var t = _shownFor.transform;
            if (t.position == _shownPos && t.rotation == _shownRot
                && _shownFor.DimensionsMM == _shownDims) return;

            if (_shownFaceIndex >= 0) ShowFace(_shownFor, _shownFaceIndex);
            else Show(_shownFor, _shownSide);
        }

        public static void Hide()
        {
            foreach (var q in Quads)
                if (q != null) DestroyObject(q);
            Quads.Clear();
            _shownFor = null;
            _shownFaceIndex = -1;
        }

        /// <summary>В рантайме Destroy: DestroyImmediate из колбэка UI-события
        /// Unity запрещает. В EditMode-тестах Destroy не отрабатывает вовсе,
        /// поэтому там — DestroyImmediate.</summary>
        private static void DestroyObject(GameObject go)
        {
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }

        /// <summary>Общий корень накладок — в мировых координатах, вне иерархии
        /// детали (см. комментарий к классу).</summary>
        private static Transform Root()
        {
            if (_root == null)
            {
                _root = new GameObject("__EdgeSideHighlight") { hideFlags = HideFlags.DontSave };
                _root.transform.SetParent(null, worldPositionStays: false);
            }
            return _root.transform;
        }

        /// <summary>Полоса вдоль общего ребра соседней грани с торцом: она
        /// прижата к торцу и уходит вглубь на BandFraction размера ЭТОЙ грани в
        /// направлении от торца, но не больше BandMaxMm.</summary>
        private static void AddBand(in Face face, in Face end)
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
            AddQuad(face, size, shift);
        }

        /// <summary>Накладка на грань: размер в юнитах и сдвиг от центра грани
        /// в её собственных осях.</summary>
        private static void AddQuad(in Face face, Vector2 size, Vector2 shiftInFace)
        {
            var go = new GameObject("EdgeSideHighlight");
            go.hideFlags = HideFlags.DontSave;
            // Корень стоит в позе identity, поэтому локальный трансформ квада и
            // есть мировой: масштаб детали в накладку не просачивается.
            go.transform.SetParent(Root(), worldPositionStays: false);

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
        /// примитивы вырезаются из WebGL-сборки вместе с коллайдерами.
        /// Цвет лежит в вершинах — его берёт Hidden/OverlayLine.</summary>
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
            var c = UI.UIStyle.EdgeHighlight3D;
            _quad.colors = new[] { c, c, c, c };
            return _quad;
        }

        /// <summary>Подмена материала для тестов. В редакторе Shader.Find находит
        /// всё, и отказ шейдера, специфичный для СБОРКИ, там не воспроизвести —
        /// а проверить, что подсветка при этом не роняет приложение, надо.</summary>
        public static System.Func<Material?>? MaterialFactory;

        /// <summary>Шейдера нет в сборке — повторно не ищем и не спамим в лог.</summary>
        private static bool _shaderMissing;

        /// <summary>Материал накладки; null — подходящего шейдера в сборке нет.</summary>
        private static Material? HighlightMaterial()
        {
            if (MaterialFactory != null) return MaterialFactory();
            if (_material != null) return _material;
            if (_shaderMissing) return null;

            var shader = FindHighlightShader();
            if (shader == null)
            {
                _shaderMissing = true;
                Debug.LogWarning("[EdgeSideHighlight] Шейдер накладки не найден — "
                    + "подсветка стороны под кромку не будет видна.");
                return null;
            }

            _material = new Material(shader) { hideFlags = HideFlags.DontSave };
            var color = UI.UIStyle.EdgeHighlight3D;
            _material.color = color;
            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", color);

            // Hidden/OverlayLine уже настроен как надо (ZTest Always, ZWrite Off,
            // альфа-смешивание, своя очередь) и цвет берёт из вершин — ему ничего
            // выставлять не нужно, а перебивать его renderQueue вредно.
            if (shader.name == "Hidden/OverlayLine") return _material;

            // Прозрачность: под накладкой должна читаться текстура детали. У URP
            // мало выставить _Surface — без ключевого слова и режимов смешивания
            // материал остаётся непрозрачным.
            if (_material.HasProperty("_Surface")) _material.SetFloat("_Surface", 1f);
            if (_material.HasProperty("_Blend")) _material.SetFloat("_Blend", 0f);
            if (_material.HasProperty("_SrcBlend"))
                _material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (_material.HasProperty("_DstBlend"))
                _material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (_material.HasProperty("_ZWrite")) _material.SetFloat("_ZWrite", 0f);
            _material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return _material;
        }

        /// <summary>Шейдер накладки с запасными вариантами.
        ///
        /// Основной — Hidden/OverlayLine (рулетка): ZTest Always, ZWrite Off,
        /// Cull Off, альфа-смешивание, цвет из вершин. ZTest Always здесь по
        /// делу: подсветка должна читаться и когда торец прижат к соседней
        /// детали, и когда сторона смотрит от камеры, — иначе она «пропадает»
        /// в геометрии, ради чего вся затея и не работала. Лежит в Resources,
        /// поэтому стриппинг его не трогает; Resources.Load — на случай, когда
        /// Shader.Find не видит шейдер, не использованный ни одним материалом
        /// сцены (тот же приём в EdgeOutlineRenderer).
        ///
        /// Дальше — прежняя цепочка. URP/Unlit ВЫРЕЗАЕТСЯ стриппингом, если им
        /// не пользуется ни один материал проекта, а Unlit/Color — шейдер
        /// встроенного пайплайна, которого в URP-сборке нет вовсе: в билде оба
        /// давали null, и конструктор материала падал с ArgumentNullException
        /// на каждое наведение (Player.log).</summary>
        private static Shader? FindHighlightShader()
        {
            var shader = Shader.Find("Hidden/OverlayLine");
            if (shader == null) shader = Resources.Load<Shader>("Shaders/OverlayLine");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            return shader;
        }
    }
}
