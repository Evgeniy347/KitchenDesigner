using System.Collections.Generic;
using KitchenDesigner.Core.Plumbing;

/// <summary>Сборщик трассы для тестов ядра прокладки труб.
///
/// Порт задаётся точкой и осью НАРУЖУ, поэтому у трубы, идущей из A в B, ось
/// порта в A смотрит от B, а ось порта в B — от A. Ошибиться здесь легко, и
/// тогда «не соединилось» будет свойством теста, а не кода; вся арифметика осей
/// собрана в одном месте именно поэтому.</summary>
internal sealed class PipeTestScene : IPipeSceneSnapshot
{
    private readonly List<PipePort> _ports = new List<PipePort>();
    private readonly List<PipeRunSegment> _segments = new List<PipeRunSegment>();
    private readonly List<PipeObstacle> _obstacles = new List<PipeObstacle>();

    public static PointMm At(float xMm, float yMm, float zMm) => new PointMm(xMm, yMm, zMm);

    public static PipeAxis Towards(in PointMm from, in PointMm to) =>
        new PipeAxis(to.XMm - from.XMm, to.YMm - from.YMm, to.ZMm - from.ZMm).Normalized;

    public IReadOnlyList<PipePort> Ports() => _ports;

    public IReadOnlyList<PipeRunSegment> Segments() => _segments;

    public IReadOnlyList<PipeObstacle> Obstacles() => _obstacles;

    public PipeSurvey Survey() => PipeSurvey.Of(_ports);

    public int IndexOf(string elementId, int portIndex)
    {
        for (int i = 0; i < _ports.Count; i++)
            if (_ports[i].ElementId == elementId && _ports[i].PortIndex == portIndex)
                return i;
        return -1;
    }

    public PipeTestScene Pipe(string id, in PointMm from, in PointMm to, string sizeId)
    {
        _ports.Add(new PipePort(id, PipeNodeKind.Pipe, 0, from, Towards(to, from), sizeId));
        _ports.Add(new PipePort(id, PipeNodeKind.Pipe, 1, to, Towards(from, to), sizeId));
        _segments.Add(new PipeRunSegment(id, from, to, PipeSpec.Get(sizeId).OuterDiameterMm));
        return this;
    }

    public PipeTestScene Fitting(string id, PipeNodeKind kind,
        params (PointMm at, PipeAxis outward)[] ports)
    {
        for (int i = 0; i < ports.Length; i++)
            _ports.Add(new PipePort(id, kind, i, ports[i].at, ports[i].outward));
        return this;
    }

    public PipeTestScene Obstacle(string id, PipeObstacleKind kind,
        in PointMm minMm, in PointMm maxMm)
    {
        _obstacles.Add(new PipeObstacle(id, kind, new BoxMm(minMm, maxMm)));
        return this;
    }
}
