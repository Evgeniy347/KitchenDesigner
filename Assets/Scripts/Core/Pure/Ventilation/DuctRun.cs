namespace KitchenDesigner.Core.Ventilation
{
    public readonly struct DuctRun
    {
        public readonly string ElementId;
        public readonly DuctProfile Profile;
        public readonly float AirflowM3PerHour;
        public readonly int PortAIndex;
        public readonly int PortBIndex;

        public DuctRun(string elementId, in DuctProfile profile, float airflowM3PerHour,
            int portAIndex, int portBIndex)
        {
            ElementId = elementId;
            Profile = profile;
            AirflowM3PerHour = airflowM3PerHour;
            PortAIndex = portAIndex;
            PortBIndex = portBIndex;
        }
    }
}
