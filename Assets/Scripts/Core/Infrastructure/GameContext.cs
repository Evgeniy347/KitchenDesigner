namespace KitchenDesigner.Core
{
    public class GameServices
    {
        public IPartRegistry PartRegistry { get; }
        public ICommandStack CommandStack { get; }
        public IElementFactory ElementFactory { get; }
        public ISaveLoadManager SaveLoadManager { get; }

        public GameServices(
            IPartRegistry partRegistry,
            ICommandStack commandStack,
            IElementFactory elementFactory,
            ISaveLoadManager saveLoadManager)
        {
            PartRegistry = partRegistry;
            CommandStack = commandStack;
            ElementFactory = elementFactory;
            SaveLoadManager = saveLoadManager;
        }
    }

    public static class GameContext
    {
        public static GameServices? Services { get; private set; }

        public static void Initialize(GameServices services)
        {
            Services = services;
        }

        public static void InitializeWithDefaults()
        {
            Initialize(new GameServices(
                new PartRegistryInstance(),
                new CommandStackInstance(),
                new ElementFactoryInstance(),
                new SaveLoadManagerInstance()
            ));
        }

        public static void Clear()
        {
            Services = null;
        }
    }
}
