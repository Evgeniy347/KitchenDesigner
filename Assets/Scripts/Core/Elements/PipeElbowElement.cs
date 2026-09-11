using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core
{
    public class PipeElbowElement : PipeFittingElement
    {
        public override ElementFront Front =>
            ElementFront.NoSeparateFacePart("отвод — два цилиндра под углом, лицевой детали нет");

        public override PipeNodeKind NodeKind => PipeNodeKind.Elbow;
    }
}
