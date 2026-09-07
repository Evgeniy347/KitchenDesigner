using UnityEngine;
using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core
{
    public class PipeReturnElement : PipeFittingElement
    {
        public override PipeNodeKind NodeKind => PipeNodeKind.Return;

        public override Material FactoryMaterial => HeatingMaterials.Return;
    }
}
