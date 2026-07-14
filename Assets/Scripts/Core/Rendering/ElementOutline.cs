using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Чистая геометрия «проволочного» короба: 8 углов ±0.5 и 12 рёбер.
    /// Вынесена отдельно, чтобы её можно было проверить тестом без сцены.</summary>
    public static class BoxWireframe
    {
        // 8 углов единичного куба (совпадает с примитивом Cube: центр в 0, ±0.5).
        public static readonly Vector3[] Corners =
        {
            new Vector3(-0.5f, -0.5f, -0.5f), // 0
            new Vector3( 0.5f, -0.5f, -0.5f), // 1
            new Vector3( 0.5f, -0.5f,  0.5f), // 2
            new Vector3(-0.5f, -0.5f,  0.5f), // 3
            new Vector3(-0.5f,  0.5f, -0.5f), // 4
            new Vector3( 0.5f,  0.5f, -0.5f), // 5
            new Vector3( 0.5f,  0.5f,  0.5f), // 6
            new Vector3(-0.5f,  0.5f,  0.5f), // 7
        };

        // 12 рёбер как пары индексов углов (низ, верх, вертикали).
        public static readonly int[] EdgeIndices =
        {
            0,1, 1,2, 2,3, 3,0, // нижняя грань
            4,5, 5,6, 6,7, 7,4, // верхняя грань
            0,4, 1,5, 2,6, 3,7, // вертикальные рёбра
        };

        public const int EdgeCount = 12;

        /// <summary>8 углов короба в МИРОВЫХ координатах через матрицу детали
        /// (localToWorld). Учитывает позицию, поворот и масштаб — поэтому рёбра
        /// ложатся точно на грани при любой ориентации детали. Чистая функция.</summary>
        public static void WorldCorners(Matrix4x4 localToWorld, Vector3[] into)
        {
            for (int i = 0; i < 8; i++)
                into[i] = localToWorld.MultiplyPoint3x4(Corners[i]);
        }
    }

    /// <summary>Чёрный контур короба для «прозрачного» режима: грани детали
    /// делаются сквозными, а форма читается по 12 рёбрам. Каждое ребро — тонкий
    /// брусок в МИРОВЫХ координатах (не дочерний масштаб!), поэтому контур не
    /// «плывёт» при повороте/неравномерном масштабе детали и всегда заметной
    /// толщины. Коллайдеров у брусков нет — клик по-прежнему ловит саму деталь.</summary>
    [DisallowMultipleComponent]
    public class ElementOutline : MonoBehaviour
    {
        /// <summary>Толщина ребра в метрах.</summary>
        private const float ThicknessMeters = 0.004f;

        private static Material? _blackMat = null!;
        private static Material? _selectedMat = null!;

        private Transform _root = null!;
        private readonly Transform[] _edges = new Transform[BoxWireframe.EdgeCount];
        private readonly Vector3[] _corners = new Vector3[8];
        private bool _visible;

        public static ElementOutline? For(KitchenElement element)
            => element != null ? element.GetComponent<ElementOutline>() : null;

        public static ElementOutline? Ensure(KitchenElement element)
        {
            if (element == null) return null;
            var outline = element.GetComponent<ElementOutline>();
            if (outline == null) outline = element.gameObject.AddComponent<ElementOutline>();
            outline.Build();
            return outline;
        }

        private void Build()
        {
            if (_root != null) return;

            var rootGo = new GameObject("__Outline");
            _root = rootGo.transform;
            _root.SetParent(null, false); // мировые координаты, без наследования масштаба

            for (int i = 0; i < BoxWireframe.EdgeCount; i++)
            {
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = "Edge" + i;
                // Примитив-куб приносит BoxCollider — снимаем, чтобы контур не
                // перехватывал клики мыши (клик должен попадать в саму деталь).
                var col = seg.GetComponent<Collider>();
                if (col != null) Destroy(col);

                var mr = seg.GetComponent<MeshRenderer>();
                mr.sharedMaterial = BlackMaterial();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                seg.transform.SetParent(_root, false);
                _edges[i] = seg.transform;
            }

            _root.gameObject.SetActive(false);
        }

        public void Show(bool selected)
        {
            if (_root == null) Build();
            var mat = selected ? SelectedMaterial() : BlackMaterial();
            if (mat != null)
                foreach (var e in _edges)
                    if (e != null) e.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (_root != null) _root.gameObject.SetActive(true);
            _visible = true;
            UpdateEdges();
        }

        public void Hide()
        {
            _visible = false;
            if (_root != null) _root.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_visible) UpdateEdges();
        }

        private void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        /// <summary>Разложить 12 брусков по рёбрам мирового короба детали.</summary>
        private void UpdateEdges()
        {
            if (_root == null) return;
            BoxWireframe.WorldCorners(transform.localToWorldMatrix, _corners);

            var idx = BoxWireframe.EdgeIndices;
            for (int e = 0; e < BoxWireframe.EdgeCount; e++)
            {
                var seg = _edges[e];
                if (seg == null) continue;
                Vector3 a = _corners[idx[e * 2]];
                Vector3 b = _corners[idx[e * 2 + 1]];
                Vector3 dir = b - a;
                float len = dir.magnitude;

                seg.position = (a + b) * 0.5f;
                seg.rotation = len > 1e-6f
                    ? Quaternion.LookRotation(dir / len) // локальный +Z вдоль ребра
                    : Quaternion.identity;
                seg.localScale = new Vector3(ThicknessMeters, ThicknessMeters, len);
            }
        }

        private static Material? MakeUnlit(Color color)
        {
            // ВАЖНО: URP/Unlit вырезается из сборки, если им не пользуется ни один
            // материал (Shader.Find → null в билде → краш). Падаем на гарантированно
            // включённый URP/Lit (его используют все детали), затем на любой доступный.
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return null; // никогда не роняем игру
            var m = new Material(shader);
            m.SetColor("_BaseColor", color);
            m.color = color;
            return m;
        }

        private static Material? BlackMaterial()
        {
            if (_blackMat == null) _blackMat = MakeUnlit(Color.black);
            return _blackMat;
        }

        private static Material? SelectedMaterial()
        {
            if (_selectedMat == null) _selectedMat = MakeUnlit(new Color(1f, 0.85f, 0.1f, 1f));
            return _selectedMat;
        }
    }
}
