using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Сторож против опечатки в разделе ведомости: до этого <see cref="SpecItem"/>
/// принимал раздел свободной строкой, и опечатка в ней тихо рождала новый раздел
/// вместо того, чтобы попасть в существующий или упасть тестом. Каждый
/// <see cref="IQuantifies"/>-тип обязан отдавать раздел ТОЛЬКО из
/// <see cref="SpecSections.All"/>.</summary>
public class SpecSectionsTests
{
    [TearDown]
    public void TearDown() => EveryElementType.ClearScene();

    [Test]
    public void EveryIQuantifiesType_ReportsOnlySectionsFromSpecSectionsAll()
    {
        var violations = new List<string>();

        foreach (var type in EveryElementType.Declared())
        {
            EveryElementType.ClearScene();
            var element = EveryElementType.Spawn(type, "Sec" + type.Name);

            foreach (var quantifies in element.GetComponents<IQuantifies>())
            foreach (var item in quantifies.GetSpecItems(new[] { element }))
                if (!SpecSections.All.Contains(item.section))
                    violations.Add($"{type.Name}: \"{item.section}\"");
        }

        Assert.IsEmpty(violations,
            "эти строки ведомости несут раздел вне SpecSections.All — опечатка в разделе "
            + "тихо родит новый раздел вместо того, чтобы упасть здесь: "
            + string.Join(", ", violations));
    }
}
