using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core
{
    public class PipeCapElement : PipeFittingElement
    {
        public override ElementFront Front =>
            ElementFront.NoSeparateFacePart("заглушка — тело вращения");

        public override PipeNodeKind NodeKind => PipeNodeKind.Cap;
    }
}
