using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core
{
    public class PipeTeeElement : PipeFittingElement
    {
        public override PipeNodeKind NodeKind => PipeNodeKind.Tee;
    }
}
