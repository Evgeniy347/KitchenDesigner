using System.Linq;
using System.Reflection;
using KitchenDesigner.Core;
using NUnit.Framework;

public class SidebarDockChoiceStaysOutOfTheProjectFileTests
{
    private static bool NamesTheDockChoice(FieldInfo field) =>
        field.Name.ToLowerInvariant().Contains("dock");

    [Test]
    public void ProjectData_CarriesNoDockField()
    {
        var offenders = typeof(ProjectData)
            .GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(NamesTheDockChoice)
            .Select(f => f.Name)
            .ToArray();

        Assert.IsEmpty(offenders,
            "режим дока (раскрытый / рейка) — настройка пользователя, а не проекта: она не "
            + "должна путешествовать в файле проекта к другому человеку. Найдено в ProjectData: "
            + string.Join(", ", offenders));
    }

    [Test]
    public void KitchenSettingsData_CarriesNoDockField()
    {
        var offenders = typeof(KitchenSettingsData)
            .GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(NamesTheDockChoice)
            .Select(f => f.Name)
            .ToArray();

        Assert.IsEmpty(offenders,
            "KitchenSettingsData тоже сериализуется В ФАЙЛ ПРОЕКТА (ProjectData.settings) — "
            + "выбор режима дока обязан жить в PlayerPrefs, а не здесь. Найдено: "
            + string.Join(", ", offenders));
    }

    private static bool NamesTheSidebarPreset(FieldInfo field) =>
        field.Name.ToLowerInvariant().Contains("preset");

    [Test]
    public void ProjectData_CarriesNoSidebarPresetField()
    {
        var offenders = typeof(ProjectData)
            .GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(NamesTheSidebarPreset)
            .Select(f => f.Name)
            .ToArray();

        Assert.IsEmpty(offenders,
            "последний использованный пресет плитки — тоже настройка пользователя, а не "
            + "проекта: она не должна путешествовать в файле проекта к другому человеку. "
            + "Найдено в ProjectData: " + string.Join(", ", offenders));
    }

    [Test]
    public void KitchenSettingsData_CarriesNoSidebarPresetField()
    {
        var offenders = typeof(KitchenSettingsData)
            .GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(NamesTheSidebarPreset)
            .Select(f => f.Name)
            .ToArray();

        Assert.IsEmpty(offenders,
            "KitchenSettingsData тоже сериализуется В ФАЙЛ ПРОЕКТА (ProjectData.settings) — "
            + "выбор последнего пресета обязан жить в PlayerPrefs, а не здесь. Найдено: "
            + string.Join(", ", offenders));
    }
}
