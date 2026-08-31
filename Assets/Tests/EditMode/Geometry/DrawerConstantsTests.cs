using NUnit.Framework;
using KitchenDesigner.Core;

public class DrawerConstantsTests
{
    [Test]
    public void ValidLengths_HasEightEntries()
    {
        Assert.AreEqual(8, DrawerConstants.ValidLengths.Length);
    }

    [Test]
    public void ValidLengths_ContainsAllExpected()
    {
        var expected = new[] { 250, 300, 350, 400, 450, 500, 550, 600 };
        Assert.AreEqual(expected.Length, DrawerConstants.ValidLengths.Length);
        for (int i = 0; i < expected.Length; i++)
            Assert.AreEqual(expected[i], DrawerConstants.ValidLengths[i]);
    }

    [Test]
    public void IsValidLength_ReturnsTrue_ForAllValid()
    {
        foreach (var len in DrawerConstants.ValidLengths)
            Assert.IsTrue(DrawerConstants.IsValidLength(len), $"len={len} should be valid");
    }

    [Test]
    public void IsValidLength_ReturnsFalse_ForInvalid()
    {
        var invalid = new[] { 200, 625, 700, 0, -1 };
        foreach (var len in invalid)
            Assert.IsFalse(DrawerConstants.IsValidLength(len), $"len={len} should be invalid");
    }

    [Test]
    public void GetTypeHeight_A_Returns86()
    {
        Assert.AreEqual(86, DrawerConstants.GetTypeHeight(DrawerType.A));
    }

    [Test]
    public void GetTypeHeight_B_Returns120()
    {
        Assert.AreEqual(120, DrawerConstants.GetTypeHeight(DrawerType.B));
    }

    [Test]
    public void GetTypeHeight_C_Returns168()
    {
        Assert.AreEqual(168, DrawerConstants.GetTypeHeight(DrawerType.C));
    }

    [Test]
    public void GetTypeHeight_D_Returns200()
    {
        Assert.AreEqual(200, DrawerConstants.GetTypeHeight(DrawerType.D));
    }

    [Test]
    public void GetTypeLabel_ReturnsCorrect()
    {
        var types = new[] { DrawerType.A, DrawerType.B, DrawerType.C, DrawerType.D };
        foreach (var t in types)
            Assert.IsNotEmpty(DrawerConstants.GetTypeLabel(t), $"type={t} label should not be empty");
    }

    [Test]
    public void GetColorName_Anthracite()
    {
        Assert.AreEqual("Антрацит", DrawerConstants.GetColorName(DrawerColor.Anthracite));
    }

    [Test]
    public void GetColorName_White()
    {
        Assert.AreEqual("Белый", DrawerConstants.GetColorName(DrawerColor.White));
    }

    [Test]
    public void GetColorName_Black()
    {
        Assert.AreEqual("Чёрный", DrawerConstants.GetColorName(DrawerColor.Black));
    }

    [Test]
    public void AllColors_HaveUniqueMaterialIds()
    {
        var a = DrawerConstants.GetColorMaterialId(DrawerColor.Anthracite);
        var w = DrawerConstants.GetColorMaterialId(DrawerColor.White);
        var b = DrawerConstants.GetColorMaterialId(DrawerColor.Black);
        Assert.AreNotEqual(a, w);
        Assert.AreNotEqual(a, b);
        Assert.AreNotEqual(w, b);
    }

    [Test]
    public void GetCycleButtonLabel_Closed()
    {
        var label = DrawerConstants.GetCycleButtonLabel(DoubleDrawerState.Closed);
        StringAssert.Contains("Открыть", label);
    }

    [Test]
    public void GetCycleButtonLabel_BothOpen()
    {
        var label = DrawerConstants.GetCycleButtonLabel(DoubleDrawerState.BothOpen);
        StringAssert.Contains("верхний", label);
    }

    [Test]
    public void GetCycleButtonLabel_LowerOnly()
    {
        var label = DrawerConstants.GetCycleButtonLabel(DoubleDrawerState.LowerOnly);
        StringAssert.Contains("всё", label);
    }

    [Test]
    public void NextCycleState_Closed_ReturnsBothOpen()
    {
        Assert.AreEqual(DoubleDrawerState.BothOpen, DrawerConstants.NextCycleState(DoubleDrawerState.Closed));
    }

    [Test]
    public void NextCycleState_BothOpen_ReturnsLowerOnly()
    {
        Assert.AreEqual(DoubleDrawerState.LowerOnly, DrawerConstants.NextCycleState(DoubleDrawerState.BothOpen));
    }

    [Test]
    public void NextCycleState_LowerOnly_ReturnsClosed()
    {
        Assert.AreEqual(DoubleDrawerState.Closed, DrawerConstants.NextCycleState(DoubleDrawerState.LowerOnly));
    }

    [Test]
    public void DrawerConstants_NextCycleState_ReturnsToClosedInThreeSteps()
    {
        var state = DoubleDrawerState.Closed;
        state = DrawerConstants.NextCycleState(state);
        Assert.AreEqual(DoubleDrawerState.BothOpen, state);
        state = DrawerConstants.NextCycleState(state);
        Assert.AreEqual(DoubleDrawerState.LowerOnly, state);
        state = DrawerConstants.NextCycleState(state);
        Assert.AreEqual(DoubleDrawerState.Closed, state);
    }

    [Test]
    public void DrawerTypeEnum_A_Equals86()
    {
        Assert.AreEqual(86, (int)DrawerType.A);
    }

    [Test]
    public void DrawerTypeEnum_D_Equals200()
    {
        Assert.AreEqual(200, (int)DrawerType.D);
    }

    // ── Монтажные размеры GTV AXIS PRO (брошюра, стр. 6 и 8) ──────────

    [Test]
    public void SlideClearance_Is37_5PerSide()
    {
        Assert.AreEqual(37.5f, DrawerConstants.SLIDE_CLEARANCE_PER_SIDE, 1e-4f);
    }

    [Test]
    public void GetBackHeight_MatchesCatalogVersion1()
    {
        Assert.AreEqual(84, DrawerConstants.GetBackHeight(DrawerType.A));
        Assert.AreEqual(116, DrawerConstants.GetBackHeight(DrawerType.B));
        Assert.AreEqual(167, DrawerConstants.GetBackHeight(DrawerType.C));
        Assert.AreEqual(199, DrawerConstants.GetBackHeight(DrawerType.D));
    }

    [Test]
    public void GetMinOpeningHeight_MatchesCatalog()
    {
        Assert.AreEqual(115, DrawerConstants.GetMinOpeningHeight(DrawerType.A));
        Assert.AreEqual(147, DrawerConstants.GetMinOpeningHeight(DrawerType.B));
        Assert.AreEqual(198, DrawerConstants.GetMinOpeningHeight(DrawerType.C));
        Assert.AreEqual(230, DrawerConstants.GetMinOpeningHeight(DrawerType.D));
    }

    [Test]
    public void TypeIndex_And_TypeFromIndex_RoundTrip()
    {
        // Значения enum — высоты в мм; прямой каст индекс↔enum ломал высоту (баг UI).
        for (int i = 0; i < DrawerConstants.Types.Length; i++)
            Assert.AreEqual(i, DrawerConstants.TypeIndex(DrawerConstants.TypeFromIndex(i)));

        Assert.AreEqual(DrawerType.A, DrawerConstants.TypeFromIndex(0));
        Assert.AreEqual(DrawerType.D, DrawerConstants.TypeFromIndex(3));
        Assert.AreEqual(3, DrawerConstants.TypeIndex(DrawerType.D));
        Assert.AreEqual(DrawerType.A, DrawerConstants.TypeFromIndex(-1), "невалидный индекс → A");
        Assert.AreEqual(DrawerType.A, DrawerConstants.TypeFromIndex(99), "невалидный индекс → A");
    }

    [Test]
    public void GetBottomLift_IsMountedTopMinusSideHeight()
    {
        Assert.AreEqual(21, DrawerConstants.GetBottomLift(DrawerType.A)); // 107−86
        Assert.AreEqual(19, DrawerConstants.GetBottomLift(DrawerType.B)); // 139−120
        Assert.AreEqual(22, DrawerConstants.GetBottomLift(DrawerType.C)); // 190−168
        Assert.AreEqual(22, DrawerConstants.GetBottomLift(DrawerType.D)); // 222−200
    }

    // ── Границы и запасные ветки ──────────────────────────────────────────
    // Ниже — то, что не достигается перебором A/B/C/D, поэтому и не проверялось:
    // границы индекса и default-формулы. Незакрытые, они тихо гниют.

    [Test]
    public void TypeFromIndex_AcceptsBothEndsOfTheRange()
    {
        Assert.AreEqual(DrawerConstants.Types[0], DrawerConstants.TypeFromIndex(0),
            "нижняя граница диапазона обязана попадать в таблицу");
        int last = DrawerConstants.Types.Length - 1;
        Assert.AreEqual(DrawerConstants.Types[last], DrawerConstants.TypeFromIndex(last),
            "верхняя граница диапазона тоже внутри таблицы");
    }

    [Test]
    public void TypeFromIndex_FallsBackJustOutsideTheRange()
    {
        Assert.AreEqual(DrawerType.A, DrawerConstants.TypeFromIndex(-1));
        Assert.AreEqual(DrawerType.A, DrawerConstants.TypeFromIndex(DrawerConstants.Types.Length));
    }

    /// <summary>Запасные формулы для типа вне таблицы. Высота типа — это его
    /// числовое значение, от него и считаются задник, проём и верх боковины.</summary>
    [Test]
    public void UnknownType_UsesArithmeticFallbacks()
    {
        var unknown = (DrawerType)500;

        Assert.AreEqual(500, DrawerConstants.GetTypeHeight(unknown));
        Assert.AreEqual(498, DrawerConstants.GetBackHeight(unknown), "задник = высота − 2");
        Assert.AreEqual(530, DrawerConstants.GetMinOpeningHeight(unknown), "проём = высота + 30");
        Assert.AreEqual(521, DrawerConstants.GetMountedTopHeight(unknown), "верх боковины = высота + 21");
        Assert.AreEqual(21, DrawerConstants.GetBottomLift(unknown), "подъём = верх боковины − высота");
    }

    [Test]
    public void PresetDimensions_AreNotEmpty()
    {
        Assert.IsNotEmpty(AppConstants.PRESET_DIMENSIONS_MM);
    }

    /// <summary>Идентификатор материала — это не подпись для глаз, а ключ в
    /// каталоге декоров: пустой или задвоенный ключ красит ящик не в тот цвет.</summary>
    [Test]
    public void ColorMaterialIds_AreDistinctAndNonEmpty()
    {
        var ids = new System.Collections.Generic.List<string>();
        foreach (DrawerColor color in System.Enum.GetValues(typeof(DrawerColor)))
        {
            var id = DrawerConstants.GetColorMaterialId(color);
            Assert.IsNotEmpty(id, $"у цвета {color} нет идентификатора материала");
            ids.Add(id);
        }

        CollectionAssert.AllItemsAreUnique(ids);
    }

    /// <summary>Каждая система ящиков обязана называться по-своему: имена
    /// уходят в спецификацию, где их читает человек на производстве.</summary>
    [Test]
    public void SystemLabelsAndNames_AreDistinctAndNonEmpty()
    {
        var labels = new System.Collections.Generic.List<string>();
        var names = new System.Collections.Generic.List<string>();
        foreach (DrawerSystem system in System.Enum.GetValues(typeof(DrawerSystem)))
        {
            var label = DrawerConstants.GetSystemLabel(system);
            var name = DrawerConstants.GetDefaultName(system);
            Assert.IsNotEmpty(label, $"у системы {system} нет подписи");
            Assert.IsNotEmpty(name, $"у системы {system} нет имени по умолчанию");
            labels.Add(label);
            names.Add(name);
        }

        CollectionAssert.AllItemsAreUnique(labels);
        CollectionAssert.AllItemsAreUnique(names);
    }
}
