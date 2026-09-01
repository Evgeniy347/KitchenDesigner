#nullable disable
using NUnit.Framework;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Core.Update;

/// <summary>Переходник между автообновлением и статус-полосой. Единственное его
/// собственное поведение — молчать, когда полосы ещё нет.</summary>
public class StatusBarSinkTests
{
    [Test]
    public void Show_BeforeTheStatusBarExists_IsSilentInsteadOfThrowing()
    {
        Assume.That(StatusBarUI.Instance, Is.Null,
            "проверяем именно случай «полосы ещё нет»");

        var sink = new StatusBarSink();

        Assert.DoesNotThrow(() => sink.Show("обновление", StatusLevel.Info, 3f),
            "проверка обновлений стартует по таймеру через пару секунд после запуска "
            + "и обгоняет узкие места старта: StatusBarUI может быть ещё не создан. "
            + "Это одноразовая подсказка, а не событие, ради которого можно уронить "
            + "стартующее приложение");
        Assert.DoesNotThrow(() => sink.Show("сбой", StatusLevel.Error, 3f));
        Assert.DoesNotThrow(() => sink.Show("готово", StatusLevel.Success, 3f));
    }
}
