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
        private static Mesh? _builtinCubeMesh;
        private Mesh? _ownedMesh;

        public bool SupportsGrooves =>
            GetType() == typeof(KitchenElement)
            && GetComponent<Wall>() == null
            && GetComponent<BasePlate>() == null;

        public IReadOnlyList<GrooveSpec> Grooves => _data.Grooves;

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

        /// <summary>Пересобрать меш под текущие пазы. Без пазов возвращается
        /// встроенный куб — деталь не тащит собственный меш без нужды.</summary>
        public void RebuildGrooveMesh()
        {
            if (!SupportsGrooves) return;
            var filter = GetComponent<MeshFilter>();
            var meshRenderer = GetComponent<MeshRenderer>();
            if (filter == null || meshRenderer == null) return;

            // Первое касание пуловой детали: запоминаем встроенный куб, иначе
            // после удаления пазов вернуть исходный меш было бы нечем. Проверка
            // имени обязательна — деталь могла получиться конвертацией и нести
            // чужой меш, который нельзя раздавать всем деталям через статик.
            if (_ownedMesh == null && _builtinCubeMesh == null
                && filter.sharedMesh != null && filter.sharedMesh.name == "Cube")
                _builtinCubeMesh = filter.sharedMesh;

            var mats = meshRenderer.sharedMaterials;
            var decor = mats != null && mats.Length > 0 && mats[0] != null
                ? mats[0] : meshRenderer.sharedMaterial;

            if (_data.Grooves.Count == 0)
            {
                if (_ownedMesh == null) return; // меш и так стандартный
                // Куба под рукой нет (деталь пришла не из пула) — собираем
                // собственную коробку без пазов.
                var restored = _builtinCubeMesh != null
                    ? _builtinCubeMesh
                    : GrooveMesh.Build(_data.DimensionsMM, null);
                DestroyOwnedMesh();
                if (restored != _builtinCubeMesh) _ownedMesh = restored;
                filter.sharedMesh = restored;
                if (decor != null) meshRenderer.sharedMaterials = new[] { decor };
                return;
            }

            var mesh = GrooveMesh.Build(_data.DimensionsMM, _data.Grooves);
            DestroyOwnedMesh();
            _ownedMesh = mesh;
            filter.sharedMesh = mesh;
            // Сабмеш 0 — декор (им управляет MaterialManager), 1 — пазы.
            meshRenderer.sharedMaterials = new[] { decor!, GrooveMesh.GrooveMaterial() };
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

        public virtual void ApplyDimensions()
        {
            transform.localScale = new Vector3(
                _data.DimensionsMM.x * AppConstants.MM_TO_UNITS,
                _data.DimensionsMM.y * AppConstants.MM_TO_UNITS,
                _data.DimensionsMM.z * AppConstants.MM_TO_UNITS
            );

            // Доли паза считаются от размеров детали — при ресайзе меш надо
            // пересобрать, иначе паз растянется вместе с localScale.
            if (_data.Grooves.Count > 0) RebuildGrooveMesh();
        }

        public virtual Vector3[] GetVertices()
        {
            var size = EffectiveScale;
            var pos = transform.position;
            var rot = transform.rotation;

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
            var pos = transform.position;
            var rot = transform.rotation;

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
