using UnityEngine;
using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core
{
    public class PipeReturnElement : PipeFittingElement
    {
        public override ElementFront Front =>
            ElementFront.NoSeparateFacePart("обратка — тело вращения");

        public override PipeNodeKind NodeKind => PipeNodeKind.Return;

        public override Material FactoryMaterial => HeatingMaterials.Return;
    }
}
