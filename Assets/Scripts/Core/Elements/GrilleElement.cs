using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.Ventilation;

namespace KitchenDesigner.Core
{
    public class GrilleElement : KitchenElement, IQuantifies, IWallMounted, IKeepsPlacementHeight
    {
        public override string DisplayTypeName => Loc.T("elementType.grille");

        public override ElementFront Front => ElementFront.NoSeparateFacePart(
            "вентиляционная решётка — плоская накладка без выделенной грани");

        public override bool CanFollowAnAttachParent => false;

        public override bool CanCarryAttachedParts => false;

        public override CutoutNeighbourRole CutoutRole => CutoutNeighbourRole.None;

        public override bool ParticipatesInGapChecks => false;

        public const int DEFAULT_WIDTH_MM = GrilleDefaults.DefaultWidthMm;
        public const int DEFAULT_HEIGHT_MM = GrilleDefaults.DefaultHeightMm;

        [SerializeField] private int _airflowM3PerHour = GrilleDefaults.DefaultAirflowM3PerHour;

        private int _lastPoseVersion;

        [Undoable]
        public int WidthMm
        {
            get => DimensionsMM.x;
            set => DimensionsMM = new Vector3Int(
                Mathf.Clamp(value, GrilleDefaults.MinSizeMm, GrilleDefaults.MaxSizeMm),
                DimensionsMM.y, DimensionsMM.z);
        }

        [Undoable]
        public int HeightMm
        {
            get => DimensionsMM.y;
            set => DimensionsMM = new Vector3Int(DimensionsMM.x,
                Mathf.Clamp(value, GrilleDefaults.MinSizeMm, GrilleDefaults.MaxSizeMm),
                DimensionsMM.z);
        }

        [Undoable]
        public int AirflowM3PerHour
        {
            get => _airflowM3PerHour;
            set => _airflowM3PerHour = Mathf.Clamp(value, GrilleDefaults.MinAirflowM3PerHour,
                GrilleDefaults.MaxAirflowM3PerHour);
        }

        public IEnumerable<SpecItem> GetSpecItems(IReadOnlyList<KitchenElement> allElements)
        {
            yield return DuctSpecItems.GrilleLine(WidthMm, HeightMm);
        }

        private void Start() => SnapToWall();

        internal void Update()
        {
            if (PoseVersion == _lastPoseVersion) { enabled = false; return; }
            _lastPoseVersion = PoseVersion;
            SnapToWall();
        }

        protected override void OnOwnPoseVersionBumped() => enabled = true;

        public void SnapToWall() => WallSeating.Seat(this, GrilleDefaults.DepthMm);
    }
}
