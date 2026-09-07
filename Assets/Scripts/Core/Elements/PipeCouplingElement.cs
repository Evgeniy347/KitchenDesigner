using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core
{
    public class PipeCouplingElement : PipeFittingElement
    {
        public override PipeNodeKind NodeKind => PipeNodeKind.Coupling;
    }
}
