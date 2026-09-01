using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.Handles;

/// <summary>Захват ручки по экрану. Физический луч здесь не годится: ручка стоит
/// вплотную к поверхности, внутри корпуса её перекрывает чужая геометрия, а
/// перемещённый в LateUpdate коллайдер до FixedUpdate стоит на старом месте.</summary>
public class HandleScreenPickTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    private Camera MakeCamera()
    {
        var go = new GameObject("Камера");
        _spawned.Add(go);
        go.transform.SetPositionAndRotation(new Vector3(0f, 0f, -5f), Quaternion.identity);
        return go.AddComponent<Camera>();
    }

    private Transform MakeMarker(Vector3 at)
    {
        var go = new GameObject("Ручка");
        _spawned.Add(go);
        go.transform.position = at;
        return go.transform;
    }

    private static Transform? Pick(Vector2 screenPoint, Camera? cam, List<Transform> markers,
        float radius = HandleScreenPick.DefaultRadiusPixels) =>
        HandleScreenPick.Nearest(screenPoint, cam, markers, t => t.position, radius);

    [Test]
    public void Nearest_PicksTheClosestOfSeveral()
    {
        var cam = MakeCamera();
        var near = MakeMarker(new Vector3(0f, 0f, 0f));
        var far = MakeMarker(new Vector3(2f, 0f, 0f));
        var markers = new List<Transform> { far, near };

        Assert.AreSame(near, Pick(cam.WorldToScreenPoint(near.position), cam, markers, 10000f),
            "берётся ближайшая по экрану, а не первая в списке");
    }

    [Test]
    public void Nearest_OutsideTheRadius_IsNull()
    {
        var cam = MakeCamera();
        var markers = new List<Transform> { MakeMarker(Vector3.zero) };
        Vector2 onIt = cam.WorldToScreenPoint(Vector3.zero);

        Assert.IsNotNull(Pick(onIt, cam, markers));
        Assert.IsNull(Pick(onIt + new Vector2(HandleScreenPick.DefaultRadiusPixels + 1f, 0f),
            cam, markers));
    }

    [Test]
    public void Nearest_BehindTheCamera_IsSkipped()
    {
        var cam = MakeCamera();
        var behind = MakeMarker(new Vector3(0f, 0f, -20f));
        var markers = new List<Transform> { behind };
        var projected = cam.WorldToScreenPoint(behind.position);

        Assume.That(projected.z, Is.LessThanOrEqualTo(0f));
        Assert.IsNull(Pick(new Vector2(projected.x, projected.y), cam, markers, 10000f),
            "за камерой WorldToScreenPoint выдаёт зеркальные, но правдоподобные "
            + "пиксели — без отсева по z ручка со спины ловилась бы курсором");
    }

    [Test]
    public void Nearest_DestroyedMarker_IsSkipped()
    {
        var cam = MakeCamera();
        var alive = MakeMarker(Vector3.zero);
        var dead = MakeMarker(Vector3.zero);
        var markers = new List<Transform> { dead, alive };
        Object.DestroyImmediate(dead.gameObject);

        Assert.AreSame(alive, Pick(cam.WorldToScreenPoint(Vector3.zero), cam, markers),
            "уничтоженный объект приходит как Unity fake-null: список ручек переживает "
            + "кадр, в котором выделение сменилось");
    }

    [Test]
    public void Nearest_NoCameraOrNoHandles_IsNull()
    {
        var cam = MakeCamera();

        Assert.IsNull(Pick(Vector2.zero, null, new List<Transform> { MakeMarker(Vector3.zero) }));
        Assert.IsNull(Pick(Vector2.zero, cam, new List<Transform>()));
    }
}
