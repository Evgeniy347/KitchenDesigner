using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class PosedGeometryContractTests : SnapCoreTestBase
{
    private sealed class RecordingBox : IPosedGeometry
    {
        private readonly Vector3 _size;
        public readonly List<Vector3> AskedFor = new List<Vector3>();

        public RecordingBox(Vector3Int dims) => _size = new Vector3(dims.x, dims.y, dims.z) * MM;

        public ElementGeometry At(Vector3 position)
        {
            AskedFor.Add(position);
            return ElementGeometry.Box("moved", position, _size);
        }
    }

    private sealed class LiftedBox : IPosedGeometry
    {
        private readonly Vector3 _size;
        private readonly Vector3 _lift;

        public LiftedBox(Vector3Int dims, Vector3 lift)
        {
            _size = new Vector3(dims.x, dims.y, dims.z) * MM;
            _lift = lift;
        }

        public ElementGeometry At(Vector3 position)
            => ElementGeometry.Box("moved", position + _lift, _size);
    }

    private sealed class NamedBox : IPosedGeometry
    {
        private readonly string _name;
        private readonly Vector3 _size;

        public NamedBox(string name, Vector3Int dims)
        {
            _name = name;
            _size = new Vector3(dims.x, dims.y, dims.z) * MM;
        }

        public ElementGeometry At(Vector3 position)
            => ElementGeometry.Box(_name, position, _size);
    }

    private const float BoardHalfHeight = 0.2f;

    private static readonly Vector3Int Board = new Vector3Int(800, 400, 18);

    [Test]
    public void TrySnap_AsksTheMovedPartForGeometry_AtEveryTrialPositionItConsiders()
    {
        var moved = new RecordingBox(Board);
        var start = new Vector3(0f, BoardHalfHeight + 0.03f, 0f);

        var result = SnapCore.TrySnap(moved, new List<ElementGeometry> { Floor() },
            start, Threshold);

        Assert.IsTrue(result.snapped, "деталь в 30 мм над полом при пороге 50 мм обязана прилипнуть");
        CollectionAssert.Contains(moved.AskedFor, start,
            "первый проход спрашивает геометрию для исходной примеряемой позиции");
        Assert.Greater(new HashSet<Vector3>(moved.AskedFor).Count, 1,
            "доборные проходы обязаны СПРОСИТЬ геометрию заново для сдвинутой позиции: "
            + "готовый снимок сдвигать нельзя — у открытой дверцы, выдвинутого ящика и "
            + "приподнятой мойки поза не следует за позицией детали");
    }

    [Test]
    public void TrySnap_UsesTheGeometryThePartReturns_NotTheRequestedPositionItself()
    {
        var lift = new Vector3(0f, 0.125f, 0f);
        var moved = new LiftedBox(Board, lift);
        var start = new Vector3(0f, BoardHalfHeight + 0.03f - lift.y, 0f);

        var result = SnapCore.TrySnap(moved, new List<ElementGeometry> { Floor() },
            start, Threshold);

        Assert.IsTrue(result.snapped, "геометрия детали в 30 мм над полом — прилипание в пределах порога");
        Assert.AreEqual(BoardHalfHeight - lift.y, result.position.y, Tol,
            "ядро считает контакт по геометрии, которую вернул сам элемент, а не по "
            + "запрошенной позиции: у детали со смещённой позой это разные вещи, и "
            + "перепутав их, снэп поставит её мимо пола ровно на величину смещения");
    }

    [Test]
    public void TrySnap_SkipsTheNeighbourWhoseIdMatchesTheMovedPart()
    {
        var floor = Floor();
        var start = new Vector3(0f, BoardHalfHeight + 0.03f, 0f);

        var alien = SnapCore.TrySnap(new NamedBox("moved", Board),
            new List<ElementGeometry> { floor }, start, Threshold);
        Assert.IsTrue(alien.snapped,
            "контроль: чужой сосед на том же расстоянии прилипание даёт");

        var floorTwin = ElementGeometry.Box("moved", new Vector3(0f, -9f * MM, 0f),
            new Vector3(3000, 18, 3000) * MM);
        var self = SnapCore.TrySnap(new NamedBox("moved", Board),
            new List<ElementGeometry> { floorTwin }, start, Threshold);

        Assert.IsFalse(self.snapped,
            "снимок с тем же Id — это сама движимая деталь: ядро отличает соседа от себя "
            + "только по Id, и без этой отсечки деталь прилипала бы к собственной копии");
    }

    [Test]
    public void Box_RotatedBy45Degrees_BoundsAreWiderThanTheBody()
    {
        var size = new Vector3(0.8f, 0.4f, 0.016f);
        var box = ElementGeometry.Box("rotated", Vector3.zero, size, RotY(45f));

        float half = 0.5f * (size.x + size.z) * (float)System.Math.Sqrt(0.5);

        Assert.AreEqual(2f * half, box.Max.x - box.Min.x, 1e-5f,
            "AABB строится по восьми ПОВЁРНУТЫМ углам, а не по половине габарита");
        float bodyVolume = size.x * size.y * size.z;
        float boundsVolume = (box.Max.x - box.Min.x) * (box.Max.y - box.Min.y)
            * (box.Max.z - box.Min.z);
        Assert.Greater(boundsVolume, bodyVolume,
            "у повёрнутой детали габарит ОБЪЕМЛЕТ тело с запасом — на это рассчитаны "
            + "консервативные отсечки широкой фазы, а AABB по half дал бы ровно тело");
    }

    [Test]
    public void Box_NotRotated_BoundsHugTheBodyExactly()
    {
        var size = new Vector3(0.8f, 0.4f, 0.016f);
        var box = ElementGeometry.Box("upright", Vector3.zero, size);

        Assert.AreEqual(size.x, box.Max.x - box.Min.x, 1e-6f,
            "контроль к повёрнутому случаю: без поворота AABB совпадает с телом");
    }
}
