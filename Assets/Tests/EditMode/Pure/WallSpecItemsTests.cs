using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Construction;

/// <summary>Строки ведомости по стене. Числа здесь НЕ считаются заново: они
/// берутся у <see cref="WallQuantities"/> — единственного места, где живут
/// формулы кладки. Проверяется проводка: какие строки, в каком разделе, в каких
/// единицах и не теряется ли что-нибудь по дороге.</summary>
public class WallSpecItemsTests
{
    private const float LengthMm = 3000f;
    private const float HeightMm = 2700f;
    private const float ThicknessMm = 250f;
    private const float JointMm = 10f;
    private const float NoSpare = 0f;

    private static List<SpecItem> Items(MasonryTechnology technology,
        IReadOnlyList<WallOpening>? openings = null, float sparePct = NoSpare,
        float lengthMm = LengthMm, float heightMm = HeightMm, float thicknessMm = ThicknessMm) =>
        WallSpecItems.Of(technology, lengthMm, heightMm, thicknessMm, openings, JointMm, sparePct)
            .ToList();

    [Test]
    public void ABrickWall_GivesPiecesAndMortar_InTheWallsSection()
    {
        var items = Items(MasonryTechnology.BrickSingle);

        Assert.AreEqual(2, items.Count,
            "кирпичная стена — это две строки: сами камни и раствор между ними");
        Assert.IsTrue(items.All(i => i.section == SpecSections.Walls),
            "обе строки живут в разделе «Стены», а не среди деталей ЛДСП");

        var pieces = items[0];
        var mortar = items[1];

        Assert.AreEqual(SpecUnit.Pieces, pieces.unit);
        Assert.AreEqual(MasonryUnit.Of(MasonryTechnology.BrickSingle).Title, pieces.name,
            "название строки — заголовок формата из MasonryUnit.Table, второго списка имён нет");
        Assert.IsTrue(pieces.hasDims, "у штучного камня есть габарит, и он идёт в колонки Ш|В|Г");
        Assert.AreEqual(new Vector3Int(250, 65, 120), pieces.dimsMM,
            "габарит строки — формат камня, а не размер стены");

        Assert.AreEqual(WallSpecItems.MortarName, mortar.name);
        Assert.AreEqual(SpecUnit.VolumeM3, mortar.unit, "раствор считается кубометрами");
        Assert.IsFalse(mortar.hasDims, "у раствора габарита нет — он заполняет швы");
    }

    [Test]
    public void ThePieceCountAndTheMortar_ComeFromWallQuantities_NotFromASecondFormula()
    {
        var expected = WallQuantities.Of(MasonryTechnology.BrickSingle,
            LengthMm, HeightMm, ThicknessMm, null, JointMm, NoSpare);

        var items = Items(MasonryTechnology.BrickSingle);

        Assert.AreEqual(expected.Pieces, items[0].qty,
            "штуки в ведомости обязаны быть теми же, что посчитал WallQuantities");
        Assert.AreEqual((float)expected.MortarM3, items[1].qty, 1e-4f,
            "и раствор тоже — иначе в проекте появится вторая арифметика кладки");
    }

    [Test]
    public void ATimberWall_IsOneVolumeRow_WithoutMortar()
    {
        var items = Items(MasonryTechnology.Timber);

        Assert.AreEqual(1, items.Count, "у бруса нет швов, значит и строки раствора нет");
        Assert.AreEqual(SpecUnit.VolumeM3, items[0].unit);
        Assert.AreEqual(MasonryUnit.Of(MasonryTechnology.Timber).Title, items[0].name);
        Assert.AreEqual(
            (float)WallQuantities.Of(MasonryTechnology.Timber, LengthMm, HeightMm, ThicknessMm,
                null, JointMm, NoSpare).TimberM3,
            items[0].qty, 1e-4f);
    }

    [Test]
    public void AFrameWall_IsStudsInPiecesAndTimberInRunningMetres()
    {
        var items = Items(MasonryTechnology.Frame);

        Assert.AreEqual(2, items.Count, "каркас — стойки штуками и погонаж обвязки со стойками");
        Assert.AreEqual(SpecUnit.Pieces, items[0].unit);
        Assert.AreEqual(SpecUnit.LinearMeters, items[1].unit,
            "погонные метры каркаса — «м», а не кубометры: сечение бруска здесь ещё не выбрано");
    }

    /// <summary>Противоположный вход: стена нулевого размера не имеет права
    /// выдавать строку «0 шт». Ноль в ведомости хуже отсутствия строки — его
    /// заказывают.</summary>
    [Test]
    public void ADegenerateWall_GivesNoRowsAtAll()
    {
        Assert.IsEmpty(Items(MasonryTechnology.BrickSingle, lengthMm: 0f, heightMm: 0f,
            thicknessMm: 0f));
        Assert.IsEmpty(Items(MasonryTechnology.Timber, lengthMm: 0f, heightMm: 0f,
            thicknessMm: 0f));
        Assert.IsEmpty(Items(MasonryTechnology.Frame, lengthMm: 0f, heightMm: 0f,
            thicknessMm: 0f));
    }

    [Test]
    public void AnOpening_TakesPiecesOutOfTheWall()
    {
        var solid = Items(MasonryTechnology.BrickSingle);
        var withADoor = Items(MasonryTechnology.BrickSingle,
            new[] { new WallOpening("Дверь", 900f, 2100f) });

        Assert.Less(withADoor[0].qty, solid[0].qty,
            "проём вынимает объём кладки, а значит и штуки: иначе дверной проём заказан кирпичом");
    }

    [Test]
    public void TheSpare_AddsToThePieces_ButNotToTheMortar()
    {
        var bare = Items(MasonryTechnology.BrickSingle);
        var withSpare = Items(MasonryTechnology.BrickSingle, sparePct: 10f);

        Assert.Greater(withSpare[0].qty, bare[0].qty, "запас добавляется к штукам");
        Assert.AreEqual(bare[1].qty, withSpare[1].qty, 1e-4f,
            "а раствор от запаса не растёт — его льют в швы уложенных камней, "
            + "а не в те, что лежат в поддоне");
    }
}
