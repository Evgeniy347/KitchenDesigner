using NUnit.Framework;
using KitchenDesigner.Core;

public class GameContextTests
{
    [SetUp]
    public void Setup()
    {
        GameContext.Clear();
    }

    [TearDown]
    public void Teardown()
    {
        GameContext.Clear();
    }

    [Test]
    public void Clear_ServicesAreNull()
    {
        Assert.IsNull(GameContext.Services);
    }

    [Test]
    public void InitializeWithDefaults_CreatesAllServices()
    {
        GameContext.InitializeWithDefaults();
        Assert.IsNotNull(GameContext.Services);
        Assert.IsNotNull(GameContext.Services.BoardRegistry);
        Assert.IsNotNull(GameContext.Services.CommandStack);
        Assert.IsNotNull(GameContext.Services.ElementFactory);
        Assert.IsNotNull(GameContext.Services.SaveLoadManager);
    }

    [Test]
    public void InitializeWithDefaults_ServicesAreCorrectTypes()
    {
        GameContext.InitializeWithDefaults();
        Assert.IsInstanceOf<BoardRegistryInstance>(GameContext.Services.BoardRegistry);
        Assert.IsInstanceOf<CommandStackInstance>(GameContext.Services.CommandStack);
        Assert.IsInstanceOf<ElementFactoryInstance>(GameContext.Services.ElementFactory);
        Assert.IsInstanceOf<SaveLoadManagerInstance>(GameContext.Services.SaveLoadManager);
    }

    [Test]
    public void CustomServices_AreUsed()
    {
        var boardReg = new BoardRegistryInstance();
        var cmdStack = new CommandStackInstance();
        var factory = new ElementFactoryInstance();
        var saveLoad = new SaveLoadManagerInstance();

        var services = new GameServices(boardReg, cmdStack, factory, saveLoad);
        GameContext.Initialize(services);

        Assert.AreSame(boardReg, GameContext.Services.BoardRegistry);
        Assert.AreSame(cmdStack, GameContext.Services.CommandStack);
        Assert.AreSame(factory, GameContext.Services.ElementFactory);
        Assert.AreSame(saveLoad, GameContext.Services.SaveLoadManager);
    }

    [Test]
    public void StaticFacades_UseContextServices_WhenInitialized()
    {
        GameContext.InitializeWithDefaults();
        var boardReg = (BoardRegistryInstance)GameContext.Services.BoardRegistry;
        var cmdStack = (CommandStackInstance)GameContext.Services.CommandStack;

        Assert.IsFalse(CommandStack.CanUndo);
        Assert.AreEqual(0, BoardRegistry.GetAll().Count);
    }

    [Test]
    public void Clear_ResetsToDefaultFallback()
    {
        GameContext.InitializeWithDefaults();
        Assert.IsNotNull(GameContext.Services);
        GameContext.Clear();
        Assert.IsNull(GameContext.Services);
    }

    [Test]
    public void MultipleInitializations_ReplacesServices()
    {
        GameContext.InitializeWithDefaults();
        var first = GameContext.Services;

        GameContext.InitializeWithDefaults();
        var second = GameContext.Services;

        Assert.AreNotSame(first, second);
    }

    [Test]
    public void BoardRegistry_StaticFacade_UsesContextWhenAvailable()
    {
        GameContext.InitializeWithDefaults();
        var instance = (BoardRegistryInstance)GameContext.Services.BoardRegistry;

        Assert.AreEqual(0, instance.GetAll().Count);
        BoardRegistry.Clear();

        Assert.AreEqual(0, BoardRegistry.GetAll().Count);
        Assert.IsFalse(CommandStack.CanUndo);
    }
}
