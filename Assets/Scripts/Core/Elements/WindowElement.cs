using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public enum GlassTint { Clear = 0, Tinted = 1 }

    public class WindowElement : KitchenElement
    {
        private const float OpenSeconds = 0.4f;
        private const float MaxAngleDeg = 90f;

        [SerializeField] private GlassTint _tint = GlassTint.Clear;
        [SerializeField] private int _sillProtrusionMM = AppConstants.WINDOW_SILL_DEFAULT_MM;
        [SerializeField] private DoorMode _mode = DoorMode.HingeFrontLeft;
        [SerializeField] private bool _isOpen = false;
        [SerializeField] private string _attachedWallName = "";

        private static Shader? _cachedShader;
        private static Material? _tintedGlassMat;
        private static Material? _clearGlassMat;
        private static Material? _slopeMat;

        private readonly List<GameObject> _children = new List<GameObject>();
        private GameObject? _frameTop, _frameBottom, _frameLeft, _frameRight;
        private GameObject? _glassPane;
        private GameObject? _sillObj;
        private GameObject? _dripObj;
        private GameObject? _slopeTop, _slopeBottom, _slopeLeft, _slopeRight;
        private Transform? _pivotGroup;

        private float _openT;
        private Vector3 _closedPos;
        private Quaternion _closedRot = Quaternion.identity;

        public GlassTint Tint
        {
            get => _tint;
            set { _tint = value; ApplyTint(); }
        }

        public int SillProtrusionMM
        {
            get => _sillProtrusionMM;
            set { _sillProtrusionMM = Mathf.Clamp(value, 0, 200); ApplyDimensions(); }
        }

        public DoorMode Mode
        {
            get => _mode;
            set { _mode = value; if (_openT > 0f) ApplyDoorPose(); }
        }

        public bool IsOpen => _isOpen;
        public string AttachedWallName { get => _attachedWallName; set => _attachedWallName = value ?? ""; }

        public Vector3 ClosedPosition => IsDoorClosed ? transform.position : _closedPos;
        public Quaternion ClosedRotation => IsDoorClosed ? transform.rotation : _closedRot;
        public bool IsDoorClosed => !_isOpen && _openT <= 0f;

        private void Start()
        {
            RegisterWithNearestWall();
        }

        public override void ApplyDimensions()
        {
            base.ApplyDimensions();
            EnsureChildren();
            RebuildGeometry();
        }

        public void SetOpen(bool open)
        {
            if (open && _openT <= 0f) CaptureClosed();
            _isOpen = open;
            if (!Mathf.Approximately(_openT, open ? 1f : 0f))
                FrameRateManager.KeepAwake(OpenSeconds + 0.2f);
        }

        public void ToggleOpen() => SetOpen(!_isOpen);

        public void ForceClose()
        {
            if (_openT <= 0f && !_isOpen) return;
            _isOpen = false;
            _openT = 0f;
            transform.SetPositionAndRotation(_closedPos, _closedRot);
            ApplyDoorPose();
        }

        private void CaptureClosed()
        {
            _closedPos = transform.position;
            _closedRot = transform.rotation;
        }

        private void ApplyDoorPose()
        {
            if (_pivotGroup == null) return;
            var half = transform.localScale * 0.5f;
            FacadeDoor.Pose(_closedPos, _closedRot, half, _mode, _openT, out var pos, out var rot);
            transform.SetPositionAndRotation(pos, rot);
        }

        private void Update() => StepDoor(Time.deltaTime);

        public void StepDoor(float dt)
        {
            float target = _isOpen ? 1f : 0f;
            if (Mathf.Approximately(_openT, target))
            {
                if (_openT <= 0f) CaptureClosed();
                return;
            }
            float step = OpenSeconds > 0f ? dt / OpenSeconds : 1f;
            _openT = Mathf.MoveTowards(_openT, target, step);
            ApplyDoorPose();
        }

        private void RegisterWithNearestWall()
        {
            float bestDist = float.MaxValue;
            Wall? bestWall = null;
            foreach (var el in PartRegistry.GetAll())
            {
                if (el == null || el == this) continue;
                var wall = el.GetComponent<Wall>();
                if (wall == null) continue;
                float dist = Vector3.Distance(transform.position, el.transform.position);
                if (dist < bestDist) { bestDist = dist; bestWall = wall; }
            }
            if (bestWall != null)
            {
                _attachedWallName = bestWall.gameObject.name;
                bestWall.RegisterWindow(this);
            }
        }

        private void UnregisterFromWall()
        {
            if (string.IsNullOrEmpty(_attachedWallName)) return;
            foreach (var el in PartRegistry.GetAll())
            {
                if (el == null) continue;
                var wall = el.GetComponent<Wall>();
                if (wall != null && wall.gameObject.name == _attachedWallName)
                {
                    wall.UnregisterWindow(this);
                    return;
                }
            }
        }

        private void EnsureChildren()
        {
            if (_pivotGroup == null)
            {
                var pivotGo = new GameObject("_PivotGroup");
                pivotGo.transform.SetParent(transform, false);
                pivotGo.transform.localPosition = Vector3.zero;
                pivotGo.transform.localRotation = Quaternion.identity;
                _pivotGroup = pivotGo.transform;
            }

            int needed = 11;
            while (_children.Count < needed)
            {
                var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
                child.name = GetChildName(_children.Count);
                child.transform.SetParent(_pivotGroup, false);
                var col = child.GetComponent<BoxCollider>();
                if (col != null) Object.DestroyImmediate(col);
                var mr = child.GetComponent<MeshRenderer>();
                if (mr != null) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _children.Add(child);
            }

            _frameLeft   = _children[0];
            _frameRight  = _children[1];
            _frameTop    = _children[2];
            _frameBottom = _children[3];
            _glassPane   = _children[4];
            _sillObj     = _children[5];
            _dripObj     = _children[6];
            _slopeTop    = _children[7];
            _slopeBottom = _children[8];
            _slopeLeft   = _children[9];
            _slopeRight  = _children[10];
        }

        private string GetChildName(int idx) => idx switch
        {
            0 => "FrameLeft", 1 => "FrameRight", 2 => "FrameTop", 3 => "FrameBottom",
            4 => "Glass", 5 => "Sill", 6 => "DripCap",
            7 => "SlopeTop", 8 => "SlopeBottom", 9 => "SlopeLeft", 10 => "SlopeRight",
            _ => "Child" + idx
        };

        private void RebuildGeometry()
        {
            if (_pivotGroup == null) return;
            var dims = DimensionsMM;
            float toU = AppConstants.MM_TO_UNITS;
            float frameMM = AppConstants.WINDOW_FRAME_MM;
            float frameU = frameMM * toU;
            float glassThick = AppConstants.WINDOW_GLASS_THICKNESS_MM * toU;

            float totalW = dims.x * toU;
            float totalH = dims.y * toU;
            float totalD = dims.z * toU;
            float halfW = totalW * 0.5f;
            float halfH = totalH * 0.5f;
            float halfD = totalD * 0.5f;

            float innerW = totalW - 2f * frameU;
            float innerH = totalH - 2f * frameU;

            if (_frameLeft != null)
            {
                _frameLeft.transform.localPosition = new Vector3(-halfW + frameU * 0.5f, 0f, 0f);
                _frameLeft.transform.localScale = new Vector3(frameU, totalH, totalD);
                _frameLeft.SetActive(true);
            }
            if (_frameRight != null)
            {
                _frameRight.transform.localPosition = new Vector3(halfW - frameU * 0.5f, 0f, 0f);
                _frameRight.transform.localScale = new Vector3(frameU, totalH, totalD);
                _frameRight.SetActive(true);
            }
            if (_frameTop != null)
            {
                _frameTop.transform.localPosition = new Vector3(0f, halfH - frameU * 0.5f, 0f);
                _frameTop.transform.localScale = new Vector3(innerW, frameU, totalD);
                _frameTop.SetActive(true);
            }
            if (_frameBottom != null)
            {
                _frameBottom.transform.localPosition = new Vector3(0f, -halfH + frameU * 0.5f, 0f);
                _frameBottom.transform.localScale = new Vector3(innerW, frameU, totalD);
                _frameBottom.SetActive(true);
            }
            if (_glassPane != null)
            {
                _glassPane.transform.localPosition = new Vector3(0f, 0f, halfD);
                _glassPane.transform.localScale = new Vector3(innerW, innerH, glassThick);
                _glassPane.SetActive(true);
            }
            if (_sillObj != null)
            {
                float sillProt = _sillProtrusionMM * toU;
                float sillY = -halfH + frameU + sillProt * 0.5f;
                _sillObj.transform.localPosition = new Vector3(0f, sillY, halfD + sillProt * 0.5f);
                _sillObj.transform.localScale = new Vector3(totalW, sillProt, sillProt);
                _sillObj.SetActive(_sillProtrusionMM > 0);
            }
            if (_dripObj != null)
            {
                float dripH = AppConstants.WINDOW_DRIP_DEFAULT_MM * toU;
                float dripProtr = 30f * toU;
                _dripObj.transform.localPosition = new Vector3(0f, -halfH + frameU * 0.5f, halfD + dripProtr * 0.5f);
                _dripObj.transform.localScale = new Vector3(totalW, dripH, dripProtr);
                _dripObj.SetActive(true);
            }
            if (_slopeTop != null)
            {
                float slopeT = AppConstants.WINDOW_SLOPE_MM * toU;
                _slopeTop.transform.localPosition = new Vector3(0f, halfH - frameU * 0.5f, -halfD + slopeT * 0.5f);
                _slopeTop.transform.localScale = new Vector3(innerW, slopeT, slopeT);
                _slopeTop.SetActive(true);
            }
            if (_slopeBottom != null)
            {
                float slopeT = AppConstants.WINDOW_SLOPE_MM * toU;
                _slopeBottom.transform.localPosition = new Vector3(0f, -halfH + frameU * 0.5f, -halfD + slopeT * 0.5f);
                _slopeBottom.transform.localScale = new Vector3(innerW, slopeT, slopeT);
                _slopeBottom.SetActive(true);
            }
            if (_slopeLeft != null)
            {
                float slopeT = AppConstants.WINDOW_SLOPE_MM * toU;
                _slopeLeft.transform.localPosition = new Vector3(-halfW + frameU * 0.5f, 0f, -halfD + slopeT * 0.5f);
                _slopeLeft.transform.localScale = new Vector3(slopeT, innerH, slopeT);
                _slopeLeft.SetActive(true);
            }
            if (_slopeRight != null)
            {
                float slopeT = AppConstants.WINDOW_SLOPE_MM * toU;
                _slopeRight.transform.localPosition = new Vector3(halfW - frameU * 0.5f, 0f, -halfD + slopeT * 0.5f);
                _slopeRight.transform.localScale = new Vector3(slopeT, innerH, slopeT);
                _slopeRight.SetActive(true);
            }

            ApplyTint();
            ApplyMaterialFrame();
        }

        private static Shader GetShader()
        {
            if (_cachedShader == null)
                _cachedShader = Shader.Find("Universal Render Pipeline/Lit");
            return _cachedShader;
        }

        private void ApplyTint()
        {
            if (_glassPane == null) return;
            var mr = _glassPane.GetComponent<MeshRenderer>();
            if (mr == null) return;

            if (_tint == GlassTint.Tinted)
            {
                if (_tintedGlassMat == null)
                    _tintedGlassMat = ElementHighlighter.MakeTransparent(GetShader(), new Color(0.15f, 0.18f, 0.22f, 0.70f));
                mr.sharedMaterial = _tintedGlassMat;
            }
            else
            {
                if (_clearGlassMat == null)
                    _clearGlassMat = ElementHighlighter.MakeTransparent(GetShader(), new Color(0.6f, 0.75f, 0.85f, 0.35f));
                mr.sharedMaterial = _clearGlassMat;
            }
        }

        private static Material SlopeMaterial()
        {
            if (_slopeMat == null)
            {
                _slopeMat = new Material(GetShader());
                _slopeMat.SetColor("_BaseColor", new Color(0.85f, 0.85f, 0.82f, 1f));
                _slopeMat.color = new Color(0.85f, 0.85f, 0.82f, 1f);
            }
            return _slopeMat;
        }

        private void ApplyMaterialFrame()
        {
            var def = MaterialCatalog.Get(MaterialId);
            if (def == null) return;
            var mat = MaterialManager.GetSharedMaterial(def);
            if (mat == null) return;
            for (int i = 0; i < _children.Count && i < 5; i++)
            {
                if (i == 4) continue;
                var mr = _children[i].GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = mat;
            }
            var slopeMat = SlopeMaterial();
            for (int i = 5; i < _children.Count; i++)
            {
                var mr = _children[i].GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = slopeMat;
            }
        }

        public void DestroyChildren()
        {
            foreach (var child in _children)
                if (child != null)
                {
                    if (Application.isPlaying) Object.Destroy(child);
                    else Object.DestroyImmediate(child);
                }
            _children.Clear();
            _frameLeft = _frameRight = _frameTop = _frameBottom = null;
            _glassPane = _sillObj = _dripObj = null;
            _slopeTop = _slopeBottom = _slopeLeft = _slopeRight = null;
        }

        private void OnDestroy()
        {
            PartRegistry.Unregister(this);
            UnregisterFromWall();
            DestroyChildren();
            if (_pivotGroup != null)
            {
                if (Application.isPlaying) Object.Destroy(_pivotGroup.gameObject);
                else Object.DestroyImmediate(_pivotGroup.gameObject);
                _pivotGroup = null;
            }
        }
    }
}
