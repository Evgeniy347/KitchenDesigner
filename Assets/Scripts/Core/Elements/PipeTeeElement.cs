using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core
{
    public class PipeTeeElement : PipeFittingElement
    {
        public override ElementFront Front =>
            ElementFront.NoSeparateFacePart("тройник — три цилиндра, лицевой детали нет");

        public override PipeNodeKind NodeKind => PipeNodeKind.Tee;
    }
}
