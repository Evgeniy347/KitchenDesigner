using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Общий набор процедурных деталей мебели: дочерние объекты
/// адресуются ПО ИМЕНИ, а не по индексу.
///
/// До слияния таких наборов было два. У того, что обслуживал диван и пуфик,
/// список рос и никогда не сокращался: диван всегда отдавал ровно четыре
/// подушки, поэтому дефект не срабатывал, — но первый вариант с переменным
/// числом подушек (угловой диван, диван без подлокотников) оставил бы лишние
/// подушки висеть в сцене, и НИ ОДИН тест не сказал бы об этом ни слова.
/// Ровно эта болезнь уже кусала кровать через LegSet, и лечится она здесь
/// тем же способом: набор, который сократился, обязан потерять лишние объекты.
///
/// Тест живёт в EditMode, а не на быстром пути: набор владеет GameObject'ами
/// и Transform'ами, сцена ему нужна по существу.</summary>
public class FurniturePartSetTests
{
    private readonly List<GameObject> _owners = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var owner in _owners)
            if (owner != null) Object.DestroyImmediate(owner);
        _owners.Clear();
    }

    private GameObject NewOwner()
    {
        var owner = new GameObject("FurniturePartSetOwner");
        _owners.Add(owner);
        return owner;
    }

    private static FurniturePartBox Box(string name) => new FurniturePartBox(
        name, Vector3.zero, 400f, 300f, 200f, 40f,
        FurniturePartOrientation.Horizontal, FurniturePartShape.Cushion);

    private static string[] ChildNames(Transform owner)
    {
        var names = new string[owner.childCount];
        for (int i = 0; i < owner.childCount; i++) names[i] = owner.GetChild(i).name;
        return names;
    }

    [Test]
    public void ASetThatShrinks_LosesTheExtraObjectsFromTheScene()
    {
        var owner = NewOwner();
        var set = new FurniturePartSet(owner.transform);

        set.Place(new[] { Box("A"), Box("B"), Box("C") });
        CollectionAssert.AreEquivalent(new[] { "A", "B", "C" }, ChildNames(owner.transform));
        var dropped = owner.transform.Find("C").gameObject;

        set.Place(new[] { Box("A") });

        CollectionAssert.AreEqual(new[] { "A" }, ChildNames(owner.transform),
            "набор сократился до одной детали — лишние обязаны исчезнуть из сцены. "
            + "Набор, который только растёт, оставляет их висеть молча: ни компилятор, "
            + "ни один тест об этом не скажут, а пользователь увидит подушки в воздухе");
        Assert.IsTrue(dropped == null,
            "мало отцепить лишнюю деталь от родителя — она обязана быть УНИЧТОЖЕНА: "
            + "GameObject без родителя остаётся в сцене корневым объектом");
    }

    [Test]
    public void ASetThatShrinks_KeepsTheObjectOfEveryPartThatSurvived()
    {
        var owner = NewOwner();
        var set = new FurniturePartSet(owner.transform);

        set.Place(new[] { Box("A"), Box("B") });
        var survivor = owner.transform.Find("A").gameObject;

        set.Place(new[] { Box("A") });

        Assert.AreSame(survivor, owner.transform.Find("A").gameObject,
            "уцелевшая деталь переиспользует свой объект: пересоздание всего набора на "
            + "каждой перестройке — это мусор в сцене на каждый кадр правки габарита");
    }

    [Test]
    public void RemovedByName_TheObjectLeavesTheScene()
    {
        var owner = NewOwner();
        var set = new FurniturePartSet(owner.transform);

        set.Cushion("Pillow1", Vector3.zero, new Vector3(700f, 120f, 450f), 90f);
        set.Cushion("Pillow2", Vector3.zero, new Vector3(700f, 120f, 450f), 90f);
        var second = owner.transform.Find("Pillow2").gameObject;

        set.Remove("Pillow2");

        CollectionAssert.AreEqual(new[] { "Pillow1" }, ChildNames(owner.transform),
            "именованное удаление — то, чем кровать убирает вторую подушку при "
            + "переключении на односпальную");
        Assert.IsTrue(second == null, "удалённая деталь обязана быть уничтожена");
    }
}
