using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    [SelectionBase]
    public class KitchenElement : MonoBehaviour
    {
        [SerializeField] private string _boardName = "Board";
        [SerializeField] private Vector3Int _dimensionsMM = new Vector3Int(800, 400, 18);
        [SerializeField] private bool _movable = true;
        [SerializeField] private int _groupId = 0;
        [SerializeField] private string _materialId = MaterialCatalog.DefaultId;

        public string BoardName
        {
            get => _boardName;
            set => _boardName = value;
        }

        public Vector3Int DimensionsMM
        {
            get => _dimensionsMM;
            set
            {
                var clamped = new Vector3Int(
                    Mathf.Max(1, value.x),
                    Mathf.Max(1, value.y),
                    Mathf.Max(1, value.z)
                );
                _dimensionsMM = clamped;
                ApplyDimensions();
            }
        }

        /// <summary>Можно ли перемещать объект (ЛКМ-drag и стрелки). Управляется
        /// чекбоксом «Запретить перемещение» в свойствах объекта.</summary>
        public bool Movable
        {
            get => _movable;
            set => _movable = value;
        }

        /// <summary>Id группы связывания (0 — не связан). См. GroupManager.</summary>
        public int GroupId
        {
            get => _groupId;
            set => _groupId = value;
        }

        /// <summary>Id декора материала (см. MaterialCatalog). Применяется через
        /// MaterialManager.Apply; сохраняется в проект.</summary>
        public string MaterialId
        {
            get => string.IsNullOrEmpty(_materialId) ? MaterialCatalog.DefaultId : _materialId;
            set => _materialId = string.IsNullOrEmpty(value) ? MaterialCatalog.DefaultId : value;
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
            BoardRegistry.Register(this);
        }

        private void OnDestroy()
        {
            BoardRegistry.Unregister(this);
        }

        public void ApplyDimensions()
        {
            transform.localScale = new Vector3(
                _dimensionsMM.x * AppConstants.MM_TO_UNITS,
                _dimensionsMM.y * AppConstants.MM_TO_UNITS,
                _dimensionsMM.z * AppConstants.MM_TO_UNITS
            );
        }

        public Vector3[] GetVertices()
        {
            var size = transform.localScale;
            var half = size * 0.5f;
            var pos = transform.position;
            var rot = transform.rotation;

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

        public Face[] GetFaces()
        {
            var size = transform.localScale;
            var half = size * 0.5f;
            var pos = transform.position;
            var rot = transform.rotation;

            var axes = new Vector3[]
            {
                rot * Vector3.right,
                rot * Vector3.up,
                rot * Vector3.forward
            };

            var faceDims = new Vector2[]
            {
                new Vector2(size.y, size.z), // right/left (X faces)
                new Vector2(size.x, size.z), // top/bottom (Y faces)
                new Vector2(size.x, size.y), // front/back (Z faces)
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

        /// <summary>Короткое описание для логов: имя, размеры (мм), позиция.</summary>
        public string Describe()
        {
            var p = transform.position;
            return $"{_boardName} ({_dimensionsMM.x}x{_dimensionsMM.y}x{_dimensionsMM.z}мм @ " +
                   $"{p.x:F3},{p.y:F3},{p.z:F3})";
        }

        /// <summary>Поворот вокруг собственного центра (позиция не меняется).</summary>
        public void Rotate(Quaternion rotation)
        {
            transform.rotation = rotation * transform.rotation;
        }

        /// <summary>Повернуть на angle градусов вокруг оси (в мировых координатах).</summary>
        public void RotateAroundAxis(Vector3 axis, float angle)
        {
            Rotate(Quaternion.AngleAxis(angle, axis));
        }
    }
}
