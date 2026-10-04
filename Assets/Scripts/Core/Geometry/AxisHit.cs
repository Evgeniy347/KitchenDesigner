namespace KitchenDesigner.Core
{
    public readonly struct AxisHit
    {
        public readonly int Id;
        public readonly float Enter;
        public readonly float Exit;

        public AxisHit(int id, float enter, float exit)
        {
            Id = id;
            Enter = enter;
            Exit = exit;
        }
    }
}
