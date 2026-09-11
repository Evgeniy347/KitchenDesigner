using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core
{
    public class PipeCouplingElement : PipeFittingElement
    {
        public override ElementFront Front =>
            ElementFront.NoSeparateFacePart("муфта — тело вращения");

        public override PipeNodeKind NodeKind => PipeNodeKind.Coupling;
    }
}
