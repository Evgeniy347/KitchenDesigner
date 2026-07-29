using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Порог прилипания — настройка сцены, а не свойство алгоритма: ядро
/// принимает его параметром и ничего не клампит. Кламп живёт в KitchenSettings,
/// здесь и проверяется.
///
/// Остальные «известные ограничения» снэпа переехали в
/// Assets/Tests/EditMode/Geometry/SnapKnownLimitationTests.cs — они чистые
/// и гоняются ещё и под dotnet test.</summary>
[Category("KnownLimitation")]
public class SnapThresholdSettingTests
{
    private float _saved;

    [SetUp]
    public void SetUp() => _saved = KitchenSettings.Instance.SnapThreshold;

    [TearDown]
    public void TearDown() => KitchenSettings.Instance.SnapThreshold = _saved;

    /// <summary>Нулевой порог означал бы «прилипание выключено» в обход флага
    /// SnapEnabled — вместо этого он зажимается в 1 мм.</summary>
    [Test]
    public void Threshold_CannotBeZero_ClampedTo1mm()
    {
        KitchenSettings.Instance.SnapThreshold = 0f;
        Assert.AreEqual(1f, KitchenSettings.Instance.SnapThreshold, 0.0001f);
    }
}
