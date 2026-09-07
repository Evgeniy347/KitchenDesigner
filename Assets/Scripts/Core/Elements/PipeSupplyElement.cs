using UnityEngine;
using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core
{
    public class PipeSupplyElement : PipeFittingElement
    {
        public override PipeNodeKind NodeKind => PipeNodeKind.Supply;

        public override Material FactoryMaterial => HeatingMaterials.Supply;
    }
}
