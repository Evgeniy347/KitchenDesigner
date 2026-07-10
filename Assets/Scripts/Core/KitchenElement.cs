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
        }

        protected virtual Vector3 EffectiveScale => transform.localScale;

        public virtual void ApplyDimensions()
        {
            transform.localScale = new Vector3(
                _data.DimensionsMM.x * AppConstants.MM_TO_UNITS,
                _data.DimensionsMM.y * AppConstants.MM_TO_UNITS,
                _data.DimensionsMM.z * AppConstants.MM_TO_UNITS
            );
        }

        public virtual Vector3[] GetVertices()
        {
            var size = EffectiveScale;
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

        public virtual Face[] GetFaces()
        {
            var size = EffectiveScale;
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
