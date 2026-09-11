using UnityEngine;
using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core
{
    public class PipeSupplyElement : PipeFittingElement
    {
        public override ElementFront Front =>
            ElementFront.NoSeparateFacePart("подача — тело вращения");

        public override PipeNodeKind NodeKind => PipeNodeKind.Supply;

        public override Material FactoryMaterial => HeatingMaterials.Supply;
    }
}
