namespace KitchenDesigner.Core
{
    public readonly struct SofaPose
    {
        public readonly float SeatSlideMM;
        public readonly float BackrestAngleDeg;
        public readonly float BackrestShiftMM;
        public readonly bool CushionsOnSeat;

        public SofaPose(float seatSlideMM, float backrestAngleDeg, float backrestShiftMM,
            bool cushionsOnSeat)
        {
            SeatSlideMM = seatSlideMM;
            BackrestAngleDeg = backrestAngleDeg;
            BackrestShiftMM = backrestShiftMM;
            CushionsOnSeat = cushionsOnSeat;
        }
    }
}
