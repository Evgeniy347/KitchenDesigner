using KitchenDesigner.Core.Construction;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public partial class KitchenSettings
    {
        public const int CONSTRUCTION_FLOOR_HEIGHT_DEFAULT_MM = 3000;
        public const int CONSTRUCTION_FLOOR_HEIGHT_MIN_MM = 2000;
        public const int CONSTRUCTION_FLOOR_HEIGHT_MAX_MM = 6000;
        public const int CONSTRUCTION_JOINT_DEFAULT_MM = 10;
        public const int CONSTRUCTION_JOINT_MAX_MM = 30;
        public const int CONSTRUCTION_WASTE_DEFAULT_PCT = 5;
        public const int CONSTRUCTION_WASTE_MAX_PCT = 50;
        public const int CONSTRUCTION_SAND_DEFAULT_MM = 100;
        public const int CONSTRUCTION_GRAVEL_DEFAULT_MM = 100;
        public const int CONSTRUCTION_BEDDING_MAX_MM = 500;

        [SerializeField] private ConstructionRegion _constructionRegion = ConstructionRegion.Urals;
        [SerializeField] private int _constructionFloorHeightMm = CONSTRUCTION_FLOOR_HEIGHT_DEFAULT_MM;
        [SerializeField] private MasonryTechnology _constructionMasonry = MasonryTechnology.BrickSingle;
        [SerializeField] private int _constructionJointMm = CONSTRUCTION_JOINT_DEFAULT_MM;
        [SerializeField] private int _constructionWastePct = CONSTRUCTION_WASTE_DEFAULT_PCT;
        [SerializeField] private SoilKind _constructionSoil = SoilKind.Unknown;
        [SerializeField] private ConcreteGrade _constructionConcrete = ConcreteGrade.B20;
        [SerializeField] private int _constructionSandMm = CONSTRUCTION_SAND_DEFAULT_MM;
        [SerializeField] private int _constructionGravelMm = CONSTRUCTION_GRAVEL_DEFAULT_MM;
        [SerializeField] private bool _constructionCompacted = true;

        public ConstructionRegion ConstructionRegion
        {
            get => _constructionRegion;
            set => _constructionRegion = (ConstructionRegion)Mathf.Clamp((int)value,
                0, ConstructionRegionTitles.All.Length - 1);
        }

        public int ConstructionFloorHeightMm
        {
            get => _constructionFloorHeightMm;
            set => _constructionFloorHeightMm = Mathf.Clamp(value,
                CONSTRUCTION_FLOOR_HEIGHT_MIN_MM, CONSTRUCTION_FLOOR_HEIGHT_MAX_MM);
        }

        public MasonryTechnology ConstructionMasonry
        {
            get => _constructionMasonry;
            set => _constructionMasonry = (MasonryTechnology)Mathf.Clamp((int)value,
                0, MasonryUnit.Table.Count - 1);
        }

        public int ConstructionJointMm
        {
            get => _constructionJointMm;
            set => _constructionJointMm = Mathf.Clamp(value, 0, CONSTRUCTION_JOINT_MAX_MM);
        }

        public int ConstructionWastePct
        {
            get => _constructionWastePct;
            set => _constructionWastePct = Mathf.Clamp(value, 0, CONSTRUCTION_WASTE_MAX_PCT);
        }

        public SoilKind ConstructionSoil
        {
            get => _constructionSoil;
            set => _constructionSoil = (SoilKind)Mathf.Clamp((int)value,
                0, SoilKindTitles.All.Length - 1);
        }

        public ConcreteGrade ConstructionConcrete
        {
            get => _constructionConcrete;
            set => _constructionConcrete = (ConcreteGrade)Mathf.Clamp((int)value,
                0, ConcreteGradeTitles.All.Length - 1);
        }

        public int ConstructionSandMm
        {
            get => _constructionSandMm;
            set => _constructionSandMm = Mathf.Clamp(value, 0, CONSTRUCTION_BEDDING_MAX_MM);
        }

        public int ConstructionGravelMm
        {
            get => _constructionGravelMm;
            set => _constructionGravelMm = Mathf.Clamp(value, 0, CONSTRUCTION_BEDDING_MAX_MM);
        }

        public bool ConstructionCompacted
        {
            get => _constructionCompacted;
            set => _constructionCompacted = value;
        }

        internal void ResetConstruction()
        {
            _constructionRegion = ConstructionRegion.Urals;
            _constructionFloorHeightMm = CONSTRUCTION_FLOOR_HEIGHT_DEFAULT_MM;
            _constructionMasonry = MasonryTechnology.BrickSingle;
            _constructionJointMm = CONSTRUCTION_JOINT_DEFAULT_MM;
            _constructionWastePct = CONSTRUCTION_WASTE_DEFAULT_PCT;
            _constructionSoil = SoilKind.Unknown;
            _constructionConcrete = ConcreteGrade.B20;
            _constructionSandMm = CONSTRUCTION_SAND_DEFAULT_MM;
            _constructionGravelMm = CONSTRUCTION_GRAVEL_DEFAULT_MM;
            _constructionCompacted = true;
        }

        private void CaptureConstruction(KitchenSettingsData data)
        {
            data.constructionRegion = (int)_constructionRegion;
            data.constructionFloorHeightMm = _constructionFloorHeightMm;
            data.constructionMasonry = (int)_constructionMasonry;
            data.constructionJointMm = _constructionJointMm;
            data.constructionWastePct = _constructionWastePct;
            data.constructionSoil = (int)_constructionSoil;
            data.constructionConcrete = (int)_constructionConcrete;
            data.constructionSandMm = _constructionSandMm;
            data.constructionGravelMm = _constructionGravelMm;
            data.constructionCompacted = _constructionCompacted;
        }

        private void ApplyConstruction(KitchenSettingsData data)
        {
            _constructionRegion = (ConstructionRegion)data.constructionRegion;
            _constructionFloorHeightMm = data.constructionFloorHeightMm;
            _constructionMasonry = (MasonryTechnology)data.constructionMasonry;
            _constructionJointMm = data.constructionJointMm;
            _constructionWastePct = data.constructionWastePct;
            _constructionSoil = (SoilKind)data.constructionSoil;
            _constructionConcrete = (ConcreteGrade)data.constructionConcrete;
            _constructionSandMm = data.constructionSandMm;
            _constructionGravelMm = data.constructionGravelMm;
            _constructionCompacted = data.constructionCompacted;
        }
    }
}
