using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core.MCP;

/// <summary>Типы рисования картинки (Plan*) — не провод. McpResponseUnitContractTests считает «DTO ответа» каждый публичный
/// класс пространства имён MCP и требует единицу в имени каждого числа; холст, раскладка и фигуры живут в пикселях и
/// индексах палитры, и принять их за ответ — значит либо переименовать пол-рисовальщика, либо плодить исключения.
/// Поэтому они internal: этот тест не даёт кому-нибудь «для удобства» сделать один из них public.</summary>
public class PlanPictureTypesTests
{
    private const string TheOnePlanWireType = "PlanReply";

    [Test]
    public void ThePictureTypes_AreInternal_SoNoResponseScanMistakesThemForTheWire()
    {
        var leaked = typeof(DigestEntry).Assembly.GetTypes()
            .Where(t => t.Namespace == "KitchenDesigner.Core.MCP" && t.IsClass && t.IsPublic)
            .Where(t => t.Name.StartsWith("Plan") && t.Name != TheOnePlanWireType)
            .Select(t => t.Name)
            .ToList();

        CollectionAssert.IsEmpty(leaked, "публичный класс рисования попадёт в сторож единиц ответа: сделай его internal");
    }

    [Test]
    public void TheScan_FindsThePictureTypes_AndTheWireOne()
    {
        var names = typeof(DigestEntry).Assembly.GetTypes().Where(t => t.Namespace == "KitchenDesigner.Core.MCP").Select(t => t.Name).ToList();

        CollectionAssert.Contains(names, "PlanComposer", "сторож ослеп: типов рисования не нашёл");
        CollectionAssert.Contains(names, TheOnePlanWireType);
    }
}
