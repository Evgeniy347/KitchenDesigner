using KitchenDesigner.Core.Ports;

namespace KitchenDesigner.Core.Ventilation
{
    public readonly struct GrilleRun
    {
        public readonly string ElementId;
        public readonly PointMm PositionMm;
        public readonly float AirflowM3PerHour;

        public GrilleRun(string elementId, in PointMm positionMm, float airflowM3PerHour)
        {
            ElementId = elementId;
            PositionMm = positionMm;
            AirflowM3PerHour = airflowM3PerHour;
        }
    }
}
