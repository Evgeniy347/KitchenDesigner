namespace KitchenDesigner.Core.Lighting
{
    public static class SwitchPower
    {
        public static void Set(ILightSwitch? source, bool on)
        {
            if (source == null || source.IsOn == on) return;

            if (!(source is KitchenElement element))
            {
                source.IsOn = on;
                return;
            }

            var before = UndoableProperties.Capture(element);
            source.IsOn = on;
            var after = UndoableProperties.Capture(element);
            source.IsOn = !on;

            var command = SetPropertiesCommand.TryCreate(element, before, after);
            if (command == null) source.IsOn = on;
            else CommandStack.Execute(command);
        }
    }
}
