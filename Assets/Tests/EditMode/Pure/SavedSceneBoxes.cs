using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Tests.Geometry;

/// <summary>Сцена пользователя как список коробок быстрого пути.
///
/// Зачем отдельный загрузчик. Настоящий снимок строит <c>ValidationSnapshot</c> по
/// живым <c>KitchenElement</c>, то есть требует Unity; на быстром пути сцены нет.
/// Но валидации достаётся от каждой детали ровно КОРОБКА: <c>Face.BoxFaceCount</c>
/// равен шести, и все сканы граней перебирают именно 6×6. Поэтому запись файла —
/// имя, габарит, позиция, поворот — разворачивается тем же самым
/// <c>ElementGeometry.Box</c>, которым пользуется продакшен, и получается тот же
/// набор граней, а не похожий.
///
/// Чего здесь НЕТ и почему это честно для замера: составные детали (мойка, ящик,
/// труба) в приложении дают дополнительные тела и пазы, а здесь — одну коробку по
/// габариту записи. Инструмент сравнивает ДВА алгоритма контакта граней на одном и
/// том же входе, и для этого вход обязан быть представительным, а не тождественным
/// снимку. Число деталей и разброс поворотов берутся из файла как есть.
///
/// Файл пользователя открывается ТОЛЬКО на чтение (<c>agents/TESTS.md</c> →
/// «docs/example.save.json — NEVER TOUCH IT»): ни одного пути записи сюда не
/// заведено намеренно.</summary>
internal static class SavedSceneBoxes
{
    public const string SaveFileName = "example.save.json";

    public const string NameKey = "name";

    public const string DimensionsKey = "dimensionsMM";

    public const string PositionKey = "position";

    public const string RotationKey = "rotation";

    /// <summary>Замороженная копия сцены, на которой стоит baseline
    /// <c>ValidationInvariantTests</c>. Разбирать её приходится отдельно от живого
    /// файла пользователя: baseline закреплён ИМЕННО на ней, и объяснять его сдвиг
    /// числами с другой сцены — значит объяснять не то.</summary>
    public const string FixtureFileName = "validation-scene.save.json";

    public static string SaveFilePath =>
        Path.Combine(RepoPaths.Subdir("docs"), SaveFileName);

    public static string FixturePath =>
        Path.Combine(RepoPaths.Subdir("Assets", "Tests", "EditMode", "Fixtures"),
            FixtureFileName);

    public static List<SavedBox> OfTheUserScene() => Parse(File.ReadAllText(SaveFilePath));

    public static List<SavedBox> OfTheValidationFixture() => Parse(File.ReadAllText(FixturePath));

    public static List<SavedBox> Parse(string projectJson)
    {
        var boxes = new List<SavedBox>();
        foreach (var record in RawElementRecords.Extract(projectJson))
        {
            var root = JsonText.RootObject(record);
            string name = Text(record, JsonText.MemberValue(record, root, NameKey));
            var dimensionsMm = Numbers(record, JsonText.MemberValue(record, root, DimensionsKey));
            var position = Numbers(record, JsonText.MemberValue(record, root, PositionKey));
            var rotation = Numbers(record, JsonText.MemberValue(record, root, RotationKey));
            if (dimensionsMm.Count < 3 || position.Count < 3 || rotation.Count < 4) continue;

            boxes.Add(new SavedBox(
                name,
                new Vector3(position[0], position[1], position[2]),
                new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]),
                new Vector3(dimensionsMm[0], dimensionsMm[1], dimensionsMm[2])
                    * AppConstants.MM_TO_UNITS));
        }
        return boxes;
    }

    public static List<ValidationElement> AsValidationElements(IReadOnlyList<SavedBox> boxes)
    {
        var elements = new List<ValidationElement>(boxes.Count);
        for (int i = 0; i < boxes.Count; i++)
        {
            var box = boxes[i];
            var geometry = ElementGeometry.Box(box.Name, box.Centre, box.SizeUnits, box.Rotation);
            elements.Add(new ValidationElement(geometry, System.Array.Empty<Vector3>(),
                ElementKind.None, ValidationElement.NoGroup, null,
                Span.FromCenter(box.Centre.y, box.SizeUnits.y), ValidationElement.NoIndex));
        }
        return elements;
    }

    private static string Text(string source, JsonSpan span)
    {
        string raw = span.Text(source);
        return raw.Length >= 2 && raw[0] == '"' ? raw.Substring(1, raw.Length - 2) : raw;
    }

    private static List<float> Numbers(string source, JsonSpan span)
    {
        var values = new List<float>(4);
        foreach (var item in JsonText.ArrayItems(source, span))
            if (float.TryParse(item.Text(source), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float value))
                values.Add(value);
        return values;
    }
}

internal readonly struct SavedBox
{
    public readonly string Name;
    public readonly Vector3 Centre;
    public readonly Quaternion Rotation;
    public readonly Vector3 SizeUnits;

    public SavedBox(string name, Vector3 centre, Quaternion rotation, Vector3 sizeUnits)
    {
        Name = name;
        Centre = centre;
        Rotation = rotation;
        SizeUnits = sizeUnits;
    }
}
