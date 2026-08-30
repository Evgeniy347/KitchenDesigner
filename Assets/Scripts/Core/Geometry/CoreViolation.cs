namespace KitchenDesigner.Core
{
    public readonly struct CoreViolation
    {
        public readonly int Element;
        public readonly int Other;
        public readonly ViolationKind Kind;

        public CoreViolation(int element, int other, ViolationKind kind)
        {
            Element = element; Other = other; Kind = kind;
        }
    }
}
