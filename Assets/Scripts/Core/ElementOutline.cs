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

        /// <summary>Меш с топологией Lines — 8 вершин, 24 индекса (12 рёбер).</summary>
        public static Mesh CreateLinesMesh()
        {
            var mesh = new Mesh { name = "BoxWireframe" };
            mesh.vertices = Corners;
            mesh.SetIndices(EdgeIndices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>Чёрный проволочный контур короба для «прозрачного» режима: грани
    /// детали делаются сквозными, а форма читается по рёбрам. Живёт отдельным
    /// дочерним объектом, поэтому НЕ конфликтует с материалами детали и с
    /// подсветкой выделения. Коллайдера нет — клик по-прежнему ловит саму деталь.</summary>
    public class ElementOutline : MonoBehaviour
    {
        private static Mesh _sharedMesh;
        private static Material _blackMat;
        private static Material _selectedMat;

        private GameObject _child;

        /// <summary>Получить контур, если он уже создан (иначе null).</summary>
        public static ElementOutline For(KitchenElement element)
            => element != null ? element.GetComponent<ElementOutline>() : null;

        /// <summary>Получить или создать контур на детали.</summary>
        public static ElementOutline Ensure(KitchenElement element)
        {
            if (element == null) return null;
            var outline = element.GetComponent<ElementOutline>();
            if (outline == null) outline = element.gameObject.AddComponent<ElementOutline>();
            outline.Build();
            return outline;
        }

        private void Build()
        {
            if (_child != null) return;

            _child = new GameObject("__Outline");
            _child.transform.SetParent(transform, false);
            _child.transform.localPosition = Vector3.zero;
            _child.transform.localRotation = Quaternion.identity;
            _child.transform.localScale = Vector3.one; // рёбра ±0.5 * localScale детали

            var mf = _child.AddComponent<MeshFilter>();
            mf.sharedMesh = SharedMesh();

            var mr = _child.AddComponent<MeshRenderer>();
            mr.sharedMaterial = BlackMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.renderingLayerMask = uint.MaxValue;

            _child.SetActive(false);
        }

        /// <summary>Показать контур. selected → жёлтый (иначе чёрный).</summary>
        public void Show(bool selected)
        {
            if (_child == null) Build();
            var mr = _child.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = selected ? SelectedMaterial() : BlackMaterial();
            _child.SetActive(true);
        }

        public void Hide()
        {
            if (_child != null) _child.SetActive(false);
        }

        private static Mesh SharedMesh()
        {
            if (_sharedMesh == null) _sharedMesh = BoxWireframe.CreateLinesMesh();
            return _sharedMesh;
        }

        private static Material MakeUnlit(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var m = new Material(shader);
            m.SetColor("_BaseColor", color);
            m.color = color;
            return m;
        }

        private static Material BlackMaterial()
        {
            if (_blackMat == null) _blackMat = MakeUnlit(Color.black);
            return _blackMat;
        }

        private static Material SelectedMaterial()
        {
            if (_selectedMat == null) _selectedMat = MakeUnlit(new Color(1f, 0.85f, 0.1f, 1f));
            return _selectedMat;
        }
    }
}
