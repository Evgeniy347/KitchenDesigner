namespace KitchenDesigner.Core
{
    public static class DefaultGameServices
    {
        public static GameServices Create() =>
            new GameServices(
                new PartRegistryInstance(),
                new CommandStackInstance(),
                new ElementFactoryInstance(),
                new SaveLoadManagerInstance(),
                new GroupServiceInstance());

        public static void Install() => GameContext.Initialize(Create());
    }
}
