using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    [SelectionBase]
    public class KitchenElement : MonoBehaviour
    {
        [SerializeField] private PartData _data = new PartData();

        public PartData Data => _data;

        public string PartName
        {
            get => _data.PartName;
            set => _data.PartName = value;
        }

        public Vector3Int DimensionsMM
        {
            get => _data.DimensionsMM;
            set
            {
                _data.DimensionsMM = value;
                ApplyDimensions();
            }
        }

        public bool Movable
        {
            get => _data.Movable;
            set => _data.Movable = value;
        }

        /// <summary>Габаритная коробка детали строится от её трансформа. У открытой
        /// дверцы и выдвинутого ящика это НЕ так: геометрия считается от закрытой
        /// позы (ClosedPosition), которая заморожена и за трансформом не следует.
        /// Запись в transform.position тогда коробку не двигает — она лишь растёт
        /// симметрично от старого центра, тянущаяся грань уходит на половину дельты,
        /// снэп мажет, а при закрытии деталь прыгает на исходное место.</summary>
        public virtual bool PoseFollowsTransform => true;

        /// <summary>Деталь можно двигать и растягивать прямо сейчас. Кроме флага
        /// «подвижна» требует, чтобы поза шла за трансформом — иначе правка
        /// геометрии молча разъезжается с логической позой.</summary>
        public bool Transformable => Movable && PoseFollowsTransform;

        public int GroupId
        {
            get => _data.GroupId;
            set => _data.GroupId = value;
        }

        public string MaterialId
        {
            get => _data.MaterialId;
            set => _data.MaterialId = value;
        }

        public bool Transparent
        {
            get => _data.Transparent;
            set => _data.Transparent = value;
        }

        // ── Пазы ───────────────────────────────────────────────────────
        // Пазы поддерживает только базовая «Деталь»: у фасадов/ящиков/столов и
        // прочих подтипов геометрия своя процедурная, и врезка в неё пласти не
        // определена. Стена и подложка — тоже KitchenElement, но деталями не
        // являются, поэтому исключены явно.
        private Mesh? _ownedMesh;

        // Размеры, под которые собран _ownedMesh. UV в нём привязаны к пропорциям
        // детали (см. GrooveMesh.ScaleUvToDecor), поэтому ресайз требует пересборки,
        // а повторный вызов с теми же размерами — нет.
        private Vector3Int _meshDims;

        public bool SupportsGrooves =>
            GetType() == typeof(KitchenElement)
            && GetComponent<Wall>() == null
            && GetComponent<BasePlate>() == null;

        public IReadOnlyList<GrooveSpec> Grooves => _data.Grooves;

        // ── Накладки текстур ───────────────────────────────────────────
        // Локальный декор на куске грани: плитка на фартуке, обои на одной
        // стороне стены, ламинат на полу. Поддерживают стена и пол — только у
        // них есть большие плоскости, которые оформляют участками. У детали для
        // этого уже есть свой декор на весь щит (MaterialId).
        public bool SupportsTextureOverlays =>
            GetComponent<Wall>() != null || this is FloorElement;

        public IReadOnlyList<TextureOverlaySpec> TextureOverlays => _data.TextureOverlays;

        /// <summary>Заменить весь набор накладок (правки из UI, undo, загрузка,
        /// дублирование). Единственная точка мутации: ей же принадлежит
        /// уведомление рендера, поэтому накладки в сцене не могут разъехаться
        /// со списком.</summary>
        public void SetTextureOverlays(IEnumerable<TextureOverlaySpec>? overlays)
        {
            if (!SupportsTextureOverlays) return;
            _data.TextureOverlays.Clear();
            if (overlays != null)
                foreach (var o in overlays)
                {
                    if (_data.TextureOverlays.Count >= TextureOverlayGeometry.MAX_PER_ELEMENT) break;
                    _data.TextureOverlays.Add(o);
                }
            TextureOverlayRenderer.Refresh(this);
        }

        // ── Кромки ─────────────────────────────────────────────────────
        // Кромкование поддерживает та же базовая «Деталь», что и пазы, и
        // только когда деталь — лист: ровно одна сторона тоньше порога
        // (см. EdgeBanding.IsSheet). У бруска/куба торцов в смысле кромки нет.
        public bool SupportsEdges => SupportsGrooves && EdgeBanding.IsSheet(_data.DimensionsMM);

        /// <summary>Клеить ли кромку на открытые торцы. У детали, которая
        /// кромкование не поддерживает, всегда false.</summary>
        public bool EdgeBandingEnabled
        {
            get => SupportsEdges && _data.EdgeBanding;
            set => _data.EdgeBanding = value;
        }

        public float EdgeThicknessMM
        {
            get => _data.EdgeThicknessMM;
            set => _data.EdgeThicknessMM = value;
        }

        /// <summary>Стороны с ручной кромкой (маска по <see cref="EdgeSide"/>):
        /// пользователь взял их на себя, автоматическая проверка на них молчит.</summary>
        public int EdgeManualMask
        {
            get => _data.EdgeManualMask;
            set => _data.EdgeManualMask = value;
        }

        public bool IsEdgeManual(EdgeSide side) => _data.IsEdgeManual(side);

        public void SetEdgeManual(EdgeSide side, bool manual) => _data.SetEdgeManual(side, manual);

        // ── Врезанные мойки ────────────────────────────────────────────
        // Мойка живёт отдельным элементом, но её проём — часть геометрии
        // ДЕТАЛИ (как окно и стена). Список ведётся деталью, чтобы меш
        // пересобирался из одного места: и при добавлении мойки, и при
        // ресайзе детали (доля проёма считается от её размеров).
        private readonly List<SinkElement> _sinks = new List<SinkElement>();

        public IReadOnlyList<SinkElement> AttachedSinks => _sinks;

        public bool HasSink(SinkElement sink) => sink != null && _sinks.Contains(sink);

        public void RegisterSink(SinkElement sink)
        {
            if (sink == null || !SupportsGrooves || _sinks.Contains(sink)) return;
            _sinks.Add(sink);
            RebuildGrooveMesh();
        }

        public void UnregisterSink(SinkElement sink)
        {
            if (sink == null) return;
            if (_sinks.Remove(sink)) RebuildGrooveMesh();
        }

        /// <summary>Снять все мойки (возврат детали в пул).</summary>
        public void ClearSinks()
        {
            if (_sinks.Count == 0) return;
            _sinks.Clear();
            RebuildGrooveMesh();
        }

        /// <summary>Ось, ПОПЕРЁК которой режутся проёмы моек: та локальная ось
        /// детали, что смотрит вверх. У повёрнутой доски это Z (пласть), у
        /// столешницы-короба — Y (толщина). Без мойки — канонический Z.</summary>
        public int SinkHoleAxis => _sinks.Count > 0 ? SinkElement.HoleAxisFor(this) : 2;

        /// <summary>Проёмы врезанных моек в нормализованных координатах той
        /// плоскости, в которой их режет GrooveMesh (см. SinkHoleAxis).</summary>
        public List<GrooveMesh.Rect2> SinkHoleRects()
        {
            var result = new List<GrooveMesh.Rect2>();
            foreach (var sink in _sinks)
            {
                if (sink == null) continue;
                var rect = sink.CutoutRectIn(this);
                if (rect.IsValid) result.Add(rect);
            }
            return result;
        }

        /// <summary>Добавить паз. Дубль (та же сторона + тип) игнорируется:
        /// смещение фиксировано, второй такой паз лёг бы ровно на первый.</summary>
        public bool AddGroove(GrooveSpec spec)
        {
            if (!SupportsGrooves) return false;
            if (_data.Grooves.Count >= AppConstants.GROOVE_MAX_PER_PART) return false;
            if (_data.Grooves.Contains(spec)) return false;
            _data.Grooves.Add(spec);
            RebuildGrooveMesh();
            return true;
        }

        public bool RemoveGrooveAt(int index)
        {
            if (index < 0 || index >= _data.Grooves.Count) return false;
            _data.Grooves.RemoveAt(index);
            RebuildGrooveMesh();
            return true;
        }

        public void ClearGrooves()
        {
            if (_data.Grooves.Count == 0) return;
            _data.Grooves.Clear();
            RebuildGrooveMesh();
        }

        /// <summary>Заменить весь набор пазов (загрузка проекта, дублирование).</summary>
        public void SetGrooves(IEnumerable<GrooveSpec>? grooves)
        {
            if (!SupportsGrooves) return;
            _data.Grooves.Clear();
            if (grooves != null)
                foreach (var g in grooves)
                    if (_data.Grooves.Count < AppConstants.GROOVE_MAX_PER_PART
                        && !_data.Grooves.Contains(g))
                        _data.Grooves.Add(g);
            RebuildGrooveMesh();
        }

        /// <summary>Пересобрать меш под текущие пазы и размеры. Без пазов это
        /// обычная коробка — но СОБСТВЕННАЯ, а не встроенный куб: UV в ней
        /// приведены к физическому масштабу декора, а он зависит от пропорций
        /// детали (см. GrooveMesh.ScaleUvToDecor).</summary>
        public void RebuildGrooveMesh()
        {
            if (!SupportsGrooves) return;
            var filter = GetComponent<MeshFilter>();
            var meshRenderer = GetComponent<MeshRenderer>();
            if (filter == null || meshRenderer == null) return;

            var mats = meshRenderer.sharedMaterials;
            var decor = mats != null && mats.Length > 0 && mats[0] != null
                ? mats[0] : meshRenderer.sharedMaterial;

            var holes = SinkHoleRects();

            var mesh = GrooveMesh.Build(_data.DimensionsMM, _data.Grooves, holes, SinkHoleAxis);
            DestroyOwnedMesh();
            _ownedMesh = mesh;
            _meshDims = _data.DimensionsMM;
            filter.sharedMesh = mesh;
            // Сабмеш 0 — декор (им управляет MaterialManager), 1 — пазы; у детали
            // без пазов второго сабмеша нет и второй материал ей не нужен.
            meshRenderer.sharedMaterials = mesh.subMeshCount > 1 && decor != null
                ? new[] { decor, GrooveMesh.GrooveMaterial() }
                : new[] { decor! };
        }

        private void DestroyOwnedMesh()
        {
            if (_ownedMesh == null) return;
            DestroyImmediate(_ownedMesh);
            _ownedMesh = null;
        }

        /// <summary>Посадочные грани пазов — ДНО каждого паза как обычная Face.
        /// Благодаря этому прилипание не требует отдельной ветки: вкладная панель
        /// ловится тем же попарным сопоставлением встречных граней, что и всё
        /// остальное, и встаёт номиналом на дно паза (см. GappedBox).
        ///
        /// Габаритные грани (GetFaces) паз НЕ меняет: короб остаётся коробом,
        /// иначе поехали бы ручки, выделение и прилипание соседних деталей.</summary>
        public Face[] GetGrooveSeatFaces()
        {
            int count = _data.Grooves.Count;
            if (count == 0) return System.Array.Empty<Face>();

            var scale = transform.localScale;
            var rot = transform.rotation;
            var pos = transform.position;
            // Пласть, в которой режется паз, — локальная грань +Z.
            var normal = rot * Vector3.forward;
            var right = rot * Vector3.right;
            var up = rot * Vector3.up;
            float floorZ = 0.5f - GrooveMesh.DepthFraction(_data.DimensionsMM);

            var seats = new List<Face>(count);
            foreach (var groove in _data.Grooves)
            {
                var rect = GrooveMesh.ComputeRect(_data.DimensionsMM, groove);
                if (!rect.IsValid) continue;

                var localCenter = new Vector3(
                    (rect.xMin + rect.xMax) * 0.5f * scale.x,
                    (rect.yMin + rect.yMax) * 0.5f * scale.y,
                    floorZ * scale.z);
                var size = new Vector2(
                    (rect.xMax - rect.xMin) * scale.x,
                    (rect.yMax - rect.yMin) * scale.y);

                seats.Add(new Face(pos + rot * localCenter, normal, size, right, up));
            }
            return seats.ToArray();
        }

        /// <summary>Стенки пазов — разметочные плоскости для ВЫРАВНИВАНИЯ кромки
        /// любой детали, а не поверхности контакта: деталь обычно прилегает к
        /// пласти снаружи и в сам паз не заходит. Снэп берёт отсюда координаты
        /// и добавляет детенты «начало паза» / «конец паза» рядом с кромкой
        /// детали (16 и 20 мм от неё).</summary>
        public Face[] GetGrooveWallFaces()
        {
            int count = _data.Grooves.Count;
            if (count == 0) return System.Array.Empty<Face>();

            var scale = transform.localScale;
            var rot = transform.rotation;
            var pos = transform.position;
            float depthFrac = GrooveMesh.DepthFraction(_data.DimensionsMM);
            // Стенка тянется от дна паза до пласти.
            float wallCenterZ = 0.5f - depthFrac * 0.5f;

            var walls = new List<Face>(count * 4);
            foreach (var groove in _data.Grooves)
            {
                var rect = GrooveMesh.ComputeRect(_data.DimensionsMM, groove);
                if (!rect.IsValid) continue;

                bool horizontal = groove.side == GrooveSide.Top || groove.side == GrooveSide.Bottom;
                if (horizontal)
                {
                    // Паз идёт вдоль X — стенки перпендикулярны Y.
                    var axis = rot * Vector3.up;
                    var size = new Vector2((rect.xMax - rect.xMin) * scale.x, depthFrac * scale.z);
                    float cx = (rect.xMin + rect.xMax) * 0.5f * scale.x;
                    foreach (float y in new[] { rect.yMin, rect.yMax })
                    {
                        var c = pos + rot * new Vector3(cx, y * scale.y, wallCenterZ * scale.z);
                        walls.Add(new Face(c, axis, size, rot * Vector3.right, rot * Vector3.forward));
                    }
                }
                else
                {
                    // Паз идёт вдоль Y — стенки перпендикулярны X.
                    var axis = rot * Vector3.right;
                    var size = new Vector2((rect.yMax - rect.yMin) * scale.y, depthFrac * scale.z);
                    float cy = (rect.yMin + rect.yMax) * 0.5f * scale.y;
                    foreach (float x in new[] { rect.xMin, rect.xMax })
                    {
                        var c = pos + rot * new Vector3(x * scale.x, cy, wallCenterZ * scale.z);
                        walls.Add(new Face(c, axis, size, rot * Vector3.up, rot * Vector3.forward));
                    }
                }
            }
            return walls.ToArray();
        }

        public struct Face
        {
            public Vector3 center;
            public Vector3 normal;
            public Vector2 size;
            public Vector3 rightAxis;
            public Vector3 upAxis;

            public Face(Vector3 center, Vector3 normal, Vector2 size, Vector3 right, Vector3 up)
            {
                this.center = center;
                this.normal = normal;
                this.size = size;
                this.rightAxis = right;
                this.upAxis = up;
            }
        }

        /// <summary>Версия позы: растёт, когда <see cref="SceneChangeTracker"/> замечает
        /// запись в трансформ. Системам, которым важно «деталь сдвинули» (привязка окна к
        /// стене, мойки к столешнице, верхнего ящика к нижнему), достаточно сравнить её со
        /// своей — вместо покадрового пересчёта.</summary>
        public int PoseVersion { get; private set; }

        internal void BumpPoseVersion() => PoseVersion++;

        private void Awake()
        {
            ApplyDimensions();
            PartRegistry.Register(this);
        }

        private void OnDestroy()
        {
            PartRegistry.Unregister(this);
            DestroyOwnedMesh();
        }

        protected virtual Vector3 EffectiveScale => transform.localScale;

        /// <summary>Поза, в которой деталь проверяется на коллизии/связность.
        /// По умолчанию — текущий трансформ. Ящик/фасад переопределяют на
        /// ЗАКРЫТУЮ позу: открывание — транзитная анимация, её коллизии гасит
        /// OpeningCollision, и она не должна порождать нарушения в статической
        /// проверке (иначе открытый ящик «пересекает» свой же фасад).</summary>
        protected virtual Vector3 ValidationPosition => transform.position;
        protected virtual Quaternion ValidationRotation => transform.rotation;

        public virtual void ApplyDimensions()
        {
            transform.localScale = new Vector3(
                _data.DimensionsMM.x * AppConstants.MM_TO_UNITS,
                _data.DimensionsMM.y * AppConstants.MM_TO_UNITS,
                _data.DimensionsMM.z * AppConstants.MM_TO_UNITS
            );

            // Доли паза и проёма мойки считаются от размеров детали, а UV — от её
            // пропорций: при ресайзе меш надо пересобрать, иначе и то и другое
            // растянется вместе с localScale.
            if (_meshDims != _data.DimensionsMM || _ownedMesh == null) RebuildGrooveMesh();

            // localScale тянет UV вместе с деталью, поэтому «вырез» декора надо
            // пересчитать под новый размер — иначе рисунок растягивается вместо
            // того, чтобы повторяться в своём физическом масштабе.
            MaterialManager.RefreshTiling(this);
        }

        public virtual Vector3[] GetVertices()
        {
            var size = EffectiveScale;
            var pos = ValidationPosition;
            var rot = ValidationRotation;

            // Стена может быть опущена (режим обзора WallCutaway) — используем
            // ПОЛНУЮ геометрию, чтобы валидация связности не зависела от камеры.
            var wall = GetComponent<Wall>();
            if (wall != null && wall.IsLowered)
            {
                size.y = wall.FullScaleY;
                pos.y = wall.FullPosition.y;
            }

            var half = size * 0.5f;

            var localCorners = new Vector3[]
            {
                new Vector3(-half.x, -half.y, -half.z),
                new Vector3( half.x, -half.y, -half.z),
                new Vector3( half.x, -half.y,  half.z),
                new Vector3(-half.x, -half.y,  half.z),
                new Vector3(-half.x,  half.y, -half.z),
                new Vector3( half.x,  half.y, -half.z),
                new Vector3( half.x,  half.y,  half.z),
                new Vector3(-half.x,  half.y,  half.z),
            };

            var result = new Vector3[8];
            for (int i = 0; i < 8; i++)
                result[i] = pos + rot * localCorners[i];
            return result;
        }

        public virtual Face[] GetFaces()
        {
            var size = EffectiveScale;
            var pos = ValidationPosition;
            var rot = ValidationRotation;

            var wall = GetComponent<Wall>();
            if (wall != null && wall.IsLowered)
            {
                size.y = wall.FullScaleY;
                pos.y = wall.FullPosition.y;
            }

            var half = size * 0.5f;

            var axes = new Vector3[]
            {
                rot * Vector3.right,
                rot * Vector3.up,
                rot * Vector3.forward
            };

            var faceDims = new Vector2[]
            {
                new Vector2(size.y, size.z),
                new Vector2(size.x, size.z),
                new Vector2(size.x, size.y),
            };

            var offsets = new Vector3[]
            {
                 axes[0] * half.x, -axes[0] * half.x,
                 axes[1] * half.y, -axes[1] * half.y,
                 axes[2] * half.z, -axes[2] * half.z,
            };

            var normals = new Vector3[]
            {
                 axes[0], -axes[0],
                 axes[1], -axes[1],
                 axes[2], -axes[2],
            };

            var rightAxis = new Vector3[]
            {
                axes[1], axes[1],
                axes[0], axes[0],
                axes[0], axes[0],
            };

            var upAxis = new Vector3[]
            {
                axes[2], axes[2],
                axes[2], axes[2],
                axes[1], axes[1],
            };

            var faces = new Face[6];
            for (int i = 0; i < 6; i++)
            {
                int dimIdx = i / 2;
                faces[i] = new Face(
                    pos + offsets[i],
                    normals[i],
                    faceDims[dimIdx],
                    rightAxis[i],
                    upAxis[i]
                );
            }
            return faces;
        }

        public void SetDimensionsFromUI(int w, int h, int d)
        {
            DimensionsMM = new Vector3Int(w, h, d);
        }

        public string Describe()
        {
            var p = transform.position;
            return $"{_data.PartName} ({_data.DimensionsMM.x}x{_data.DimensionsMM.y}x{_data.DimensionsMM.z}мм @ " +
                   $"{p.x:F3},{p.y:F3},{p.z:F3})";
        }

        public void Rotate(Quaternion rotation)
        {
            transform.rotation = rotation * transform.rotation;
        }

        public void RotateAroundAxis(Vector3 axis, float angle)
        {
            Rotate(Quaternion.AngleAxis(angle, axis));
        }
    }
}
