using System;
using System.Collections.Generic;
using KitchenDesigner.Core.Plumbing;
using NUnit.Framework;

/// <summary>Геометрия фитинга — целиком арифметика от условного прохода, и она
/// описана ДВАЖДЫ: раз как набор ног от ступицы (по ним строится меш и считаются
/// порты) и раз как габаритный ящик, который уходит в DimensionsMM и в валидацию.
/// Расхождение этих двух описаний — ровно тот класс дефекта, из-за которого ножки
/// капсульного стола уезжали за столешницу: обе функции «про одно и то же», обе
/// правдоподобны, и врут они только на несимметричном входе.
///
/// Поэтому здесь габарит не переписан константами, а ВЫВЕДЕН из ног, и сравнение
/// идёт с объявленным. Несимметричные входы взяты специально: отвод и тройник —
/// единственные фитинги, у которых ступица смещена относительно оси элемента, и
/// муфта с заглушкой такой ошибки не поймали бы.</summary>
public class PipeFittingSpecTests
{
    private const float Eps = 1e-3f;

    private const string Frame = PipeSpec.DEFAULT_SIZE;

    private static readonly PipeNodeKind[] Fittings =
    {
        PipeNodeKind.Elbow, PipeNodeKind.Coupling, PipeNodeKind.Tee,
        PipeNodeKind.Cap, PipeNodeKind.Supply, PipeNodeKind.Return,
    };

    private readonly struct Extent
    {
        public readonly float Min;
        public readonly float Max;

        public Extent(float min, float max)
        {
            Min = min;
            Max = max;
        }

        public float Size => Max - Min;
    }

    /// <summary>Габарит, выведенный из тех же ног, из которых строится меш:
    /// цилиндр вокруг каждой ноги плюс, у подачи и обратки, фланцевый диск у
    /// ступицы. Второе описание того же контура — если оно разойдётся с
    /// объявленным, элемент будет нарисован не в своём ящике.
    ///
    /// Рама (положение ступицы и длина ног) считается по frameSizeId, тело — по
    /// boreSizeId. Это НЕ два способа сказать одно и то же: устья фитинга — его
    /// монтажный контракт, и стоит им поехать вслед за выведенным диаметром, как
    /// стык, из которого этот диаметр и выведен, разойдётся, диаметр пропадёт,
    /// рама вернётся назад — и так каждый кадр.</summary>
    private static (Extent x, Extent y, Extent z) DerivedBox(PipeNodeKind kind,
        string frameSizeId, string boreSizeId)
    {
        var hub = PipeFittingSpec.HubOffsetMm(kind, frameSizeId);
        float radius = PipeFittingSpec.BodyDiameterMm(boreSizeId) * 0.5f;
        float flange = PipeFittingSpec.HasFlange(kind)
            ? PipeFittingSpec.FlangeDiameterMm(boreSizeId) * 0.5f
            : radius;

        var xs = new List<float>();
        var ys = new List<float>();
        var zs = new List<float>();

        void Cylinder(in PointMm a, in PointMm b, in PipeAxis along, float r)
        {
            float rx = Math.Abs(along.X) > 0.5f ? 0f : r;
            float ry = Math.Abs(along.Y) > 0.5f ? 0f : r;
            float rz = Math.Abs(along.Z) > 0.5f ? 0f : r;
            xs.Add(Math.Min(a.XMm, b.XMm) - rx); xs.Add(Math.Max(a.XMm, b.XMm) + rx);
            ys.Add(Math.Min(a.YMm, b.YMm) - ry); ys.Add(Math.Max(a.YMm, b.YMm) + ry);
            zs.Add(Math.Min(a.ZMm, b.ZMm) - rz); zs.Add(Math.Max(a.ZMm, b.ZMm) + rz);
        }

        for (int i = 0; i < PipeFittingSpec.PortCount(kind); i++)
            Cylinder(hub, PipeFittingSpec.PortOffsetMm(kind, frameSizeId, i),
                PipeFittingSpec.PortAxis(kind, i), radius);

        if (PipeFittingSpec.HasFlange(kind))
            Cylinder(hub, hub.Shifted(PipeAxis.Down, PipeFittingSpec.FlangeThicknessMm),
                PipeAxis.Down, flange);

        return (new Extent(Min(xs), Max(xs)), new Extent(Min(ys), Max(ys)),
            new Extent(Min(zs), Max(zs)));
    }

    private static (Extent x, Extent y, Extent z) DerivedBox(PipeNodeKind kind, string sizeId) =>
        DerivedBox(kind, sizeId, sizeId);

    private static float Reach(in Extent extent) =>
        2f * Math.Max(Math.Abs(extent.Min), Math.Abs(extent.Max));

    private static float Min(List<float> values)
    {
        float m = values[0];
        foreach (var v in values) m = Math.Min(m, v);
        return m;
    }

    private static float Max(List<float> values)
    {
        float m = values[0];
        foreach (var v in values) m = Math.Max(m, v);
        return m;
    }

    [Test]
    public void EveryFitting_HasExactlyThePortsTheCoreCountsForItsKind()
    {
        foreach (var kind in Fittings)
            Assert.AreEqual(PipeNodePorts.CountOf(kind), PipeFittingSpec.PortCount(kind),
                "у геометрии фитинга " + kind + " одно число портов, а у ядра другое: "
                + "правила PIP-01 и PIP-02 считают порты по своей таблице, и разойдясь с "
                + "геометрией, они станут судить о концах, которых на сцене нет");
    }

    [Test]
    public void ThePipeItself_IsNotAFitting_AndDeclaresNoLegs()
    {
        Assert.IsFalse(PipeFittingSpec.IsFitting(PipeNodeKind.Pipe),
            "труба — не фитинг: её длина задаётся руками, а не выводится из ног");
        Assert.AreEqual(0, PipeFittingSpec.PortCount(PipeNodeKind.Pipe),
            "порты трубы строит ScenePipeSnapshot по её концам, а не эта таблица");
        foreach (var kind in Fittings)
            Assert.IsTrue(PipeFittingSpec.IsFitting(kind), kind + " обязан быть фитингом");
    }

    [Test]
    public void EveryDeclaredBox_MatchesTheGeometryItsLegsDescribe()
    {
        foreach (var sizeId in PipeSpec.Sizes)
            foreach (var kind in Fittings)
            {
                var box = DerivedBox(kind, sizeId);
                string what = kind + " @ " + sizeId;

                Assert.AreEqual(PipeFittingSpec.WidthMm(kind, sizeId), box.x.Size, Eps,
                    "ширина ящика не накрывает ноги: " + what);
                Assert.AreEqual(PipeFittingSpec.HeightMm(kind, sizeId), box.y.Size, Eps,
                    "высота ящика не накрывает ноги: " + what);
                Assert.AreEqual(PipeFittingSpec.DepthMm(kind, sizeId), box.z.Size, Eps,
                    "глубина ящика не накрывает ноги: " + what);
            }
    }

    [Test]
    public void EveryBox_IsCentredOnThePivot_BecauseValidationDrawsItThatWay()
    {
        foreach (var sizeId in PipeSpec.Sizes)
            foreach (var kind in Fittings)
            {
                var box = DerivedBox(kind, sizeId);
                string what = kind + " @ " + sizeId;

                Assert.AreEqual(0f, box.x.Min + box.x.Max, Eps,
                    "ящик KitchenElement строится вокруг точки элемента, а геометрия "
                    + "уехала по X: " + what);
                Assert.AreEqual(0f, box.y.Min + box.y.Max, Eps, "то же по Y: " + what);
                Assert.AreEqual(0f, box.z.Min + box.z.Max, Eps, "то же по Z: " + what);
            }
    }

    [Test]
    public void TheElbowTurnsARightAngle_AndTheCouplingDoesNot()
    {
        var elbow = PipeFittingSpec.Legs(PipeNodeKind.Elbow);
        Assert.AreEqual(0f, PipeAxis.Dot(elbow[0], elbow[1]), Eps,
            "отвод 90° обязан разводить порты под прямым углом — иначе это муфта");

        var coupling = PipeFittingSpec.Legs(PipeNodeKind.Coupling);
        Assert.IsTrue(PipeAxis.AreOpposite(coupling[0], coupling[1]),
            "муфта соосна: её порты смотрят строго в разные стороны");
    }

    [Test]
    public void TheTee_HasAStraightRunAndOneBranchAcrossIt()
    {
        var legs = PipeFittingSpec.Legs(PipeNodeKind.Tee);

        Assert.IsTrue(PipeAxis.AreOpposite(legs[0], legs[1]),
            "два порта тройника лежат на одной прямой — это его проход");
        Assert.AreEqual(0f, PipeAxis.Dot(legs[0], legs[2]), Eps,
            "третий порт тройника — отвод поперёк прохода");
    }

    [Test]
    public void ADeadEndFitting_HasOneMouthAndItFacesTheRunItCloses()
    {
        foreach (var kind in new[] { PipeNodeKind.Cap, PipeNodeKind.Supply, PipeNodeKind.Return })
        {
            Assert.AreEqual(1, PipeFittingSpec.PortCount(kind), kind + ": ровно один порт");
            var axis = PipeFittingSpec.PortAxis(kind, 0);
            Assert.IsTrue(PipeAxis.AreOpposite(axis, PipeAxis.Down),
                kind + ": единственный порт смотрит вверх, а тело уходит вниз — так он "
                + "встаёт под нижний конец стояка, чей выходной вектор направлен вниз");
        }
    }

    [Test]
    public void SupplyAndReturn_CarryAFlange_AndTheCapDoesNot()
    {
        Assert.IsTrue(PipeFittingSpec.HasFlange(PipeNodeKind.Supply));
        Assert.IsTrue(PipeFittingSpec.HasFlange(PipeNodeKind.Return));
        Assert.IsFalse(PipeFittingSpec.HasFlange(PipeNodeKind.Cap),
            "заглушка просто закрывает конец: фланец у неё был бы выдумкой");

        Assert.Greater(PipeFittingSpec.WidthMm(PipeNodeKind.Supply, PipeSpec.Dn20),
            PipeFittingSpec.WidthMm(PipeNodeKind.Cap, PipeSpec.Dn20),
            "фланец обязан быть шире тела — иначе его не видно и он ничего не значит");
    }

    [Test]
    public void TheGeometryFollowsTheBore_NotAConstant()
    {
        foreach (var kind in Fittings)
        {
            Assert.Greater(PipeFittingSpec.BodyDiameterMm(PipeSpec.Dn50),
                PipeFittingSpec.BodyDiameterMm(PipeSpec.Dn15),
                "тело фитинга обязано расти вместе с трубой");
            Assert.Greater(PipeFittingSpec.HeightMm(kind, PipeSpec.Dn50),
                PipeFittingSpec.HeightMm(kind, PipeSpec.Dn15),
                kind + ": габарит замер на константе и от диаметра не зависит");
        }
    }

    [Test]
    public void TheBodyIsWiderThanThePipeItSwallows_AndTheLegIsLongerThanItIsWide()
    {
        foreach (var sizeId in PipeSpec.Sizes)
        {
            float outer = PipeSpec.Get(sizeId).OuterDiameterMm;
            Assert.Greater(PipeFittingSpec.BodyDiameterMm(sizeId), outer,
                "фитинг надевается НА трубу: тело уже трубы физически не собрать");
            Assert.Greater(PipeFittingSpec.LegLengthMm(sizeId),
                PipeFittingSpec.BodyDiameterMm(sizeId) * 0.5f,
                "нога короче радиуса тела превратила бы фитинг в шар: ступица вылезла бы "
                + "за собственный порт, и стык считался бы внутри тела");
        }
    }

    [Test]
    public void PortsOfOneFitting_NeverShareAPlace()
    {
        foreach (var kind in Fittings)
            for (int i = 0; i < PipeFittingSpec.PortCount(kind); i++)
                for (int j = i + 1; j < PipeFittingSpec.PortCount(kind); j++)
                {
                    var a = PipeFittingSpec.PortOffsetMm(kind, PipeSpec.Dn20, i);
                    var b = PipeFittingSpec.PortOffsetMm(kind, PipeSpec.Dn20, j);
                    Assert.Greater(a.DistanceMmTo(b), PipeJoint.JoinToleranceMm,
                        kind + ": порты " + i + " и " + j + " стоят в одной точке — "
                        + "PipeNetwork состыковал бы фитинг сам с собой");
                }
    }

    [Test]
    public void RoundedMm_KeepsAWholeMillimetreWhole_AndDoesNotFloorAHalf()
    {
        Assert.AreEqual(27, PipeFittingSpec.RoundedMm(26.8f));
        Assert.AreEqual(34, PipeFittingSpec.RoundedMm(33.5f),
            "половина округляется ОТ нуля: банковское округление дало бы 34 здесь и 32 "
            + "у 32,5, и габариты соседних типоразмеров разъехались бы без причины");
        Assert.AreEqual(3, PipeFittingSpec.RoundedMm(2.5f));
    }

    /// <summary>Тело фитинга следует за ВЫВЕДЕННЫМ диаметром, а рама остаётся
    /// номинальной. Ящик поэтому определён как наименьший ЦЕНТРИРОВАННЫЙ на точке
    /// элемента ящик, который накрывает геометрию: KitchenElement строит контур
    /// вокруг своей точки, и «плотный, но смещённый» ящик рисовал бы фитинг
    /// наполовину снаружи выделения.</summary>
    [Test]
    public void EveryBoxAtAnyDerivedBore_IsTheSmallestPivotCentredBoxThatCoversTheBody()
    {
        foreach (var bore in PipeSpec.Sizes)
            foreach (var kind in Fittings)
            {
                var box = DerivedBox(kind, Frame, bore);
                string what = kind + " @ рама " + Frame + ", проход " + bore;

                Assert.AreEqual(Reach(box.x), PipeFittingSpec.WidthMm(kind, Frame, bore), Eps,
                    "ширина ящика разошлась с телом: " + what);
                Assert.AreEqual(Reach(box.y), PipeFittingSpec.HeightMm(kind, Frame, bore), Eps,
                    "высота ящика разошлась с телом: " + what);
                Assert.AreEqual(Reach(box.z), PipeFittingSpec.DepthMm(kind, Frame, bore), Eps,
                    "глубина ящика разошлась с телом: " + what);
            }
    }

    /// <summary>Тело обязано расти вслед за проходом — иначе отвод «на глаз
    /// двадцатый» стоит на трубе ДУ 50 и читается как ошибка чертежа. Обратная
    /// половина утверждения — что устья при этом не двигаются — проверяется на
    /// элементе сцены (PipeFittingBoreTests), потому что только там диаметр
    /// действительно ВЫВОДИТСЯ, и только там расхождение может закольцеваться.</summary>
    [Test]
    public void TheBodyFollowsTheDerivedBore_NotTheNominalFrame()
    {
        foreach (var kind in Fittings)
        {
            Assert.Greater(PipeFittingSpec.DepthMm(kind, Frame, PipeSpec.Dn50),
                PipeFittingSpec.DepthMm(kind, Frame, PipeSpec.Dn15),
                kind + ": тело замерло на номинале и на подведённую трубу не смотрит");
            Assert.AreEqual(PipeFittingSpec.DepthMm(kind, Frame),
                PipeFittingSpec.DepthMm(kind, Frame, Frame), Eps,
                kind + ": проход, равный раме, обязан давать ровно номинальный габарит");
        }

        Assert.AreEqual(PipeFittingSpec.HeightMm(PipeNodeKind.Coupling, Frame, PipeSpec.Dn15),
            PipeFittingSpec.HeightMm(PipeNodeKind.Coupling, Frame, PipeSpec.Dn50), Eps,
            "а вот длина муфты — это её рама: она от прохода не зависит вовсе, иначе "
            + "устья уехали бы вместе с ней");
    }

    /// <summary>Число, на котором держится вся конструкция: рама зафиксирована на
    /// номинале, и её нога обязана быть длиннее радиуса САМОГО ТОЛСТОГО тела,
    /// какое на неё может встать. Иначе ступица вылезет за собственное устье, и
    /// стык окажется внутри тела фитинга.</summary>
    [Test]
    public void TheNominalLeg_IsLongerThanTheWidestBodyItWillEverCarry()
    {
        float widest = 0f;
        foreach (var sizeId in PipeSpec.Sizes)
            widest = Math.Max(widest, PipeFittingSpec.BodyDiameterMm(sizeId) * 0.5f);

        Assert.Greater(PipeFittingSpec.LegLengthMm(Frame), widest,
            "нога номинальной рамы короче радиуса самого толстого прохода: ступица вылезла "
            + "бы за собственное устье, а высота тройника перестала бы быть двумя ногами");
    }
}
