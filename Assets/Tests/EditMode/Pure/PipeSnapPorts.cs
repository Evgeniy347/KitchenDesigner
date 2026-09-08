using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Plumbing;

/// <summary>Устья портов для стендов снэпа — из ТОГО ЖЕ источника, что и у элемента.
///
/// `PipeFittingElement.SnapPortAt` и `PipeElement.SnapPortAt` считают устье по
/// `PipeFittingSpec.PortOffsetMm`/`PortAxis` и по длине трубы; здесь повторяется
/// только ПЕРЕВОД в мировые координаты, а сами числа берутся оттуда же. Если завести
/// в стенде своё описание порта, стенд станет мерить собственную ошибку — ровно то,
/// от чего предостерегает AGENTS.md → «Prove the harness before you trust what it
/// measures».
///
/// Сцена-версия того же самого — `PipeFittingSnapSceneProbeTests`: она едет через
/// настоящие элементы и `ScenePipeSnapshot`, и расхождение между ней и быстрыми
/// тестами и будет находкой.</summary>
public static class PipeSnapPorts
{
    private const float ToU = AppConstants.MM_TO_UNITS;

    public static Vector3 FittingMouthLocalUnits(PipeNodeKind kind, int port)
    {
        var offset = PipeFittingSpec.PortOffsetMm(kind, PipeSpec.DEFAULT_SIZE, port);
        return new Vector3(offset.XMm * ToU, offset.YMm * ToU, offset.ZMm * ToU);
    }

    public static Vector3 FittingMouthAxisLocal(PipeNodeKind kind, int port)
    {
        var axis = PipeFittingSpec.PortAxis(kind, port);
        return new Vector3(axis.X, axis.Y, axis.Z);
    }

    public static SnapPort[] OfFitting(PipeNodeKind kind, Quaternion rotation, Vector3 position)
    {
        int count = PipeFittingSpec.PortCount(kind);
        var ports = new SnapPort[count];
        for (int i = 0; i < count; i++)
            ports[i] = new SnapPort(
                position + rotation * FittingMouthLocalUnits(kind, i),
                rotation * FittingMouthAxisLocal(kind, i));
        return ports;
    }

    public static SnapPort[] OfPipe(int lengthMm, Quaternion rotation, Vector3 position)
    {
        var along = rotation * Vector3.up;
        var half = along * (lengthMm * 0.5f * ToU);
        return new[]
        {
            new SnapPort(position - half, -along),
            new SnapPort(position + half, along),
        };
    }
}
