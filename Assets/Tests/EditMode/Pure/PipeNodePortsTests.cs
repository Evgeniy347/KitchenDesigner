using NUnit.Framework;
using KitchenDesigner.Core.Plumbing;

/// <summary>Сколько портов у элемента трассы и кто из них объявляет свой диаметр.
///
/// Число портов — это форма железки: у отвода и муфты два, у тройника три, у
/// заглушки, подачи и обратки один. Диаметр объявляет ТОЛЬКО труба; у фитинга он
/// выводится из подведённых труб, поэтому DeclaresOwnSize для него ложно — иначе
/// в свойствах фитинга появится диаметр по умолчанию, которого никто не задавал.
///
/// RequiresOneSize отвечает на другой вопрос: какой фитинг обязан свести всё к
/// ОДНОМУ диаметру. Отвод и тройник — обязаны, их не бывает переходными. Муфта
/// здесь ПЕРЕХОДНАЯ: два её порта соосны, а диаметры сторон независимы, и разные
/// ДУ на ней — норма монтажа, а не нарушение. Отдельного вида «переходник» больше
/// нет: два имени для одной железки разъезжаются, и одно из них молча остаётся
/// без элемента, без кнопки и без ветки в фабрике.</summary>
public class PipeNodePortsTests
{
    [Test]
    public void PipeNodePorts_CountOf_MatchesTheShapeOfEachFitting()
    {
        Assert.AreEqual(2, PipeNodePorts.CountOf(PipeNodeKind.Pipe));
        Assert.AreEqual(2, PipeNodePorts.CountOf(PipeNodeKind.Elbow));
        Assert.AreEqual(2, PipeNodePorts.CountOf(PipeNodeKind.Coupling));
        Assert.AreEqual(3, PipeNodePorts.CountOf(PipeNodeKind.Tee));
        Assert.AreEqual(1, PipeNodePorts.CountOf(PipeNodeKind.Cap));
        Assert.AreEqual(1, PipeNodePorts.CountOf(PipeNodeKind.Supply));
        Assert.AreEqual(1, PipeNodePorts.CountOf(PipeNodeKind.Return));
    }

    [Test]
    public void PipeNodePorts_Kinds_ListsEveryKindOfNode()
    {
        var all = System.Enum.GetValues(typeof(PipeNodeKind));
        Assert.AreEqual(all.Length, PipeNodePorts.Kinds.Length,
            "новый вид узла обязан попасть в таблицу портов тем же изменением, что заводит его");
        foreach (PipeNodeKind kind in all)
            CollectionAssert.Contains(PipeNodePorts.Kinds, kind);
    }

    [Test]
    public void PipeNodePorts_EveryKindOtherThanThePipe_IsOfferedAsAFitting()
    {
        foreach (var kind in PipeNodePorts.Kinds)
        {
            if (kind == PipeNodeKind.Pipe) continue;
            CollectionAssert.Contains(PipeFittingNames.Kinds, kind,
                kind + ": вид узла, которому не соответствует ни одна кнопка и ни один "
                + "элемент сцены, — это второе имя для чего-то уже существующего");
        }
    }

    [Test]
    public void PipeNodePorts_DeclaresOwnSize_IsTrueForPipeOnly()
    {
        Assert.IsTrue(PipeNodePorts.DeclaresOwnSize(PipeNodeKind.Pipe));
        foreach (var kind in PipeNodePorts.Kinds)
        {
            if (kind == PipeNodeKind.Pipe) continue;
            Assert.IsFalse(PipeNodePorts.DeclaresOwnSize(kind),
                kind + ": диаметр фитинга выводится из труб, а не задаётся на нём");
        }
    }

    [Test]
    public void PipeNodePorts_RequiresOneSize_HoldsForFittingsThatDoNotChangeDiameter()
    {
        Assert.IsTrue(PipeNodePorts.RequiresOneSize(PipeNodeKind.Elbow));
        Assert.IsTrue(PipeNodePorts.RequiresOneSize(PipeNodeKind.Tee));
        Assert.IsFalse(PipeNodePorts.RequiresOneSize(PipeNodeKind.Coupling),
            "переходная муфта существует ровно для того, чтобы свести два разных диаметра");
        Assert.IsFalse(PipeNodePorts.RequiresOneSize(PipeNodeKind.Pipe));
    }

    [Test]
    public void PipePort_DeclaredSize_IsNullOnAFitting_AndDefaultsOnAPipe()
    {
        var at = new PointMm(0f, 0f, 0f);
        var elbow = new PipePort("elbow", PipeNodeKind.Elbow, 0, at, PipeAxis.Right, PipeSpec.Dn50);
        Assert.IsNull(elbow.DeclaredSizeId,
            "даже если сцена положила размер в порт фитинга, ядро его не объявляет своим");

        var pipe = new PipePort("pipe", PipeNodeKind.Pipe, 0, at, PipeAxis.Right, null);
        Assert.AreEqual(PipeSpec.DEFAULT_SIZE, pipe.DeclaredSizeId,
            "труба без указанного размера — 3/4\"");
    }
}
