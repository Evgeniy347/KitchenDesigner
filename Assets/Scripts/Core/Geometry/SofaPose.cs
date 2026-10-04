namespace KitchenDesigner.Core
{
    public readonly struct SofaPose
    {
        public readonly float SeatSlideMM;
        public readonly float BackrestAngleDeg;

        public SofaPose(float seatSlideMM, float backrestAngleDeg)
        {
            SeatSlideMM = seatSlideMM;
            BackrestAngleDeg = backrestAngleDeg;
        }
    }
}
