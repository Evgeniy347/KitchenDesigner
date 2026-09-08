using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using NUnit.Framework;

/// <summary>Половина todo_evolution.md §2.1, которая не нуждается в сцене:
/// «разрешение пресета» — превращение строки каталога (drawerSystem,
/// drawerType, drawerColor) в конкретный перечислимый тип, который принимает
/// фабрика. До правки эти три переключателя (switch/тернарник) сидели внутри
/// SidebarSpawnRouter и ElementSpawner — MonoBehaviour-класса, которого
/// EditMode-тест мог достать только через живой сценарий спауна. Здесь та же
/// логика — чистая функция строка → enum, и у каждой обязана быть пара:
/// известное имя пресета и НЕИЗВЕСТНОЕ (опечатка), потому что опечатка не
/// кидает исключение — она молча даёт запасной вариант, и только тест видит
/// разницу.</summary>
public class SidebarPresetResolutionTests
{
    [Test]
    public void DrawerSystemOf_MoventoConstant_ResolvesToMovento()
    {
        Assert.AreEqual(DrawerSystem.Movento,
            SidebarPresetResolution.DrawerSystemOf(SidebarPresetResolution.MoventoDrawerSystem),
            "строка-константа Movento обязана давать вид системы Movento — иначе ящик "
            + "Movento в каталоге завёлся бы направляющими GTV");
    }

    [Test]
    public void DrawerSystemOf_AnythingElse_ResolvesToGtv()
    {
        Assert.AreEqual(DrawerSystem.Gtv, SidebarPresetResolution.DrawerSystemOf("gtv"));
        Assert.AreEqual(DrawerSystem.Gtv, SidebarPresetResolution.DrawerSystemOf(""),
            "опечатка или пустая строка обязана падать на запасной вариант GTV, а не кидать "
            + "исключение и не молча становиться Movento");
    }

    [Test]
    public void DrawerTypeOf_KnownLetters_ResolveToTheirOwnType()
    {
        Assert.AreEqual(DrawerType.A, SidebarPresetResolution.DrawerTypeOf("A"));
        Assert.AreEqual(DrawerType.B, SidebarPresetResolution.DrawerTypeOf("B"));
        Assert.AreEqual(DrawerType.C, SidebarPresetResolution.DrawerTypeOf("C"));
        Assert.AreEqual(DrawerType.D, SidebarPresetResolution.DrawerTypeOf("D"));
    }

    [Test]
    public void DrawerTypeOf_UnknownLetter_FallsBackToA()
    {
        Assert.AreEqual(DrawerType.A, SidebarPresetResolution.DrawerTypeOf("Z"),
            "неизвестная буква типа ящика обязана давать тип A по умолчанию, а не кидать "
            + "исключение и не превращаться в D — самый высокий и заметный тип");
    }

    [Test]
    public void DrawerColorOf_KnownNames_AreCaseInsensitive()
    {
        Assert.AreEqual(DrawerColor.White, SidebarPresetResolution.DrawerColorOf("White"));
        Assert.AreEqual(DrawerColor.White, SidebarPresetResolution.DrawerColorOf("white"));
        Assert.AreEqual(DrawerColor.Black, SidebarPresetResolution.DrawerColorOf("BLACK"));
    }

    [Test]
    public void DrawerColorOf_UnknownName_FallsBackToAnthracite()
    {
        Assert.AreEqual(DrawerColor.Anthracite, SidebarPresetResolution.DrawerColorOf("mauve"),
            "неизвестный цвет обязан давать цвет по умолчанию (антрацит), а не кидать "
            + "исключение");
    }
}
