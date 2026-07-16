namespace KitchenDesigner.Core
{
    public class GameServices
    {
        public IPartRegistry PartRegistry { get; }
        public ICommandStack CommandStack { get; }
        public IElementFactory ElementFactory { get; }
        public ISaveLoadManager SaveLoadManager { get; }
        public IGroupService GroupService { get; }

        public GameServices(
            IPartRegistry partRegistry,
            ICommandStack commandStack,
            IElementFactory elementFactory,
            ISaveLoadManager saveLoadManager,
            IGroupService? groupService = null)
        {
            PartRegistry = partRegistry;
            CommandStack = commandStack;
            ElementFactory = elementFactory;
            SaveLoadManager = saveLoadManager;
            GroupService = groupService ?? new GroupServiceInstance();
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
                new SaveLoadManagerInstance(),
                new GroupServiceInstance()
            ));
        }

        public static void Clear()
        {
            Services = null;
        }
    }
}
