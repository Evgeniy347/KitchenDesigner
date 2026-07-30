using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Сборный (рамочный) фасад: рамка A=100 мм + центр (панель/стекло/пусто)
    /// и вертикальные фрезеровки. Наследует открывание двери/ящика от FacadeElement.
    /// Меш процедурный (см. AssembledFacadeMesh) и пересобирается при изменении размера;
    /// localScale остаётся полным коробом, поэтому коллайдер/ручки/выделение не меняются.</summary>
    public class AssembledFacadeElement : FacadeElement, ISpecificationParts
    {
        private static Material? _grooveMat;
        private static Material? _glassMat;

        private AssembledFill _fill = AssembledFill.Blind;
        private int _grooveCount = AppConstants.ASSEMBLED_DEFAULT_GROOVES;

        private MeshFilter? _filter;
        private MeshRenderer? _renderer;
        private Mesh? _ownedMesh;
        private Transform? _glassInsert;

        [Undoable]
        public AssembledFill Fill
        {
            get => _fill;
            set { _fill = value; RebuildMesh(); }
        }

        [Undoable]
        public int GrooveCount
        {
            get => _grooveCount;
            set { _grooveCount = Mathf.Max(0, value); RebuildMesh(); }
        }

        public override void ApplyDimensions()
        {
            base.ApplyDimensions();
            RebuildMesh();
        }

        /// <summary>Пересобрать процедурный меш рамки и обновить стеклянную вставку.</summary>
        public void RebuildMesh()
        {
            if (_filter == null) _filter = GetComponent<MeshFilter>();
            if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
            if (_filter == null || _renderer == null) return;

            var mesh = AssembledFacadeMesh.Build(DimensionsMM, _fill, _grooveCount,
                AppConstants.ASSEMBLED_FRAME_MM);
            if (_ownedMesh != null) DestroyImmediate(_ownedMesh);
            _ownedMesh = mesh;
            _filter.sharedMesh = mesh;

            // Два сабмеша: [0] декор рамки (им управляет MaterialManager), [1] фрезеровки.
            var mats = _renderer.sharedMaterials;
            Material decor = mats != null && mats.Length > 0 && mats[0] != null
                ? mats[0] : _renderer.sharedMaterial;
            _renderer.sharedMaterials = new[] { decor, GrooveMaterial() };

            UpdateGlassInsert();
        }

        private void UpdateGlassInsert()
        {
            if (_fill == AssembledFill.Glass)
            {
                if (_glassInsert == null) BuildGlassInsert();
                float fx = AssembledFacadeMesh.Fraction(DimensionsMM.x, AppConstants.ASSEMBLED_FRAME_MM);
                float fy = AssembledFacadeMesh.Fraction(DimensionsMM.y, AppConstants.ASSEMBLED_FRAME_MM);
                // Дочерний масштаб * localScale детали = размер проёма (B×C) и тонкое стекло.
                _glassInsert!.localScale = new Vector3(1f - 2f * fx, 1f - 2f * fy, 0.05f);
                _glassInsert.gameObject.SetActive(true);
            }
            else if (_glassInsert != null)
            {
                _glassInsert.gameObject.SetActive(false);
            }
        }

        private void BuildGlassInsert()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "__Glass";
            var col = go.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col); // не перехватывать клики
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = GlassMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _glassInsert = go.transform;
            _glassInsert.SetParent(transform, false);
            _glassInsert.localPosition = Vector3.zero;
            _glassInsert.localRotation = Quaternion.identity;
        }

        private void OnDestroy()
        {
            if (_ownedMesh != null)
            {
                DestroyImmediate(_ownedMesh);
                _ownedMesh = null;
            }
            if (_glassInsert != null)
            {
                DestroyImmediate(_glassInsert.gameObject);
                _glassInsert = null;
            }
        }

        // ── Спецификация: раскладка на детали ──────────────────────────
        public IEnumerable<AssembledFacadeMesh.Part> GetSpecParts()
            => AssembledFacadeMesh.ComputeParts(DimensionsMM, _fill,
                AppConstants.ASSEMBLED_FRAME_MM, AppConstants.ASSEMBLED_GLASS_DEDUCT_MM);

        private static Material GrooveMaterial()
        {
            if (_grooveMat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                _grooveMat = new Material(shader);
                _grooveMat.SetColor("_BaseColor", new Color(0.18f, 0.18f, 0.20f, 1f));
                _grooveMat.color = new Color(0.18f, 0.18f, 0.20f, 1f);
            }
            return _grooveMat!;
        }

        private static Material GlassMaterial()
        {
            if (_glassMat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                _glassMat = ElementHighlighter.MakeTransparent(shader, new Color(0.6f, 0.75f, 0.85f, 0.18f));
            }
            return _glassMat!;
        }
    }
}
