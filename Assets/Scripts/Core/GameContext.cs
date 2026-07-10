namespace KitchenDesigner.Core
{
    public class GameServices
    {
        public IBoardRegistry BoardRegistry { get; }
        public ICommandStack CommandStack { get; }
        public IElementFactory ElementFactory { get; }
        public ISaveLoadManager SaveLoadManager { get; }

        public GameServices(
            IBoardRegistry boardRegistry,
            ICommandStack commandStack,
            IElementFactory elementFactory,
            ISaveLoadManager saveLoadManager)
        {
            BoardRegistry = boardRegistry;
            CommandStack = commandStack;
            ElementFactory = elementFactory;
            SaveLoadManager = saveLoadManager;
        }
    }

    public static class GameContext
    {
        public static GameServices Services { get; private set; }

        public static void Initialize(GameServices services)
        {
            Services = services;
        }

        public static void InitializeWithDefaults()
        {
            Initialize(new GameServices(
                new BoardRegistryInstance(),
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
