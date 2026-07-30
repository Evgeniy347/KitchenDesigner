using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>
/// Статик <see cref="ToastNotification.Instance"/> переживает свой GameObject.
/// Unity считает уничтоженный объект «равным null» только через перегруженный
/// <c>==</c>; оператор <c>?.</c> эту перегрузку обходит и проверяет настоящую
/// C#-ссылку — она не нулевая, и <c>Instance?.Show(...)</c> вызывает Show на
/// мёртвом объекте (MissingReferenceException). Тесты держат оба конца: статик
/// обнуляется в OnDestroy, и вызывающий защищён даже если обнулиться не успел
/// (сцена перезагружена, объект уничтожен вместе с канвасом).
/// </summary>
public class ToastNotificationTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    // В edit mode Unity не рассылает сообщения MonoBehaviour (нет
    // [ExecuteAlways]), поэтому Awake и OnDestroy зовём сами — ровно там, где
    // их вызвал бы движок в живом приложении.
    private static void CallMessage(ToastNotification toast, string name) =>
        typeof(ToastNotification)
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(toast, null);

    private static void CallAwake(ToastNotification toast) => CallMessage(toast, "Awake");

    private static void CallOnDestroy(ToastNotification toast) => CallMessage(toast, "OnDestroy");

    private static void SetInstance(ToastNotification? toast) =>
        typeof(ToastNotification).GetProperty(nameof(ToastNotification.Instance))!
            .GetSetMethod(nonPublic: true)!.Invoke(null, new object?[] { toast });

    /// <summary>Тост, чей GameObject уже уничтожен: ссылка живая, объект мёртв.</summary>
    private static ToastNotification CreateDestroyedToast()
    {
        var go = new GameObject("Toast");
        var toast = go.AddComponent<ToastNotification>();
        CallAwake(toast);
        Object.DestroyImmediate(go);
        return toast;
    }

    private KitchenElement CreateWall(Vector3Int dims)
    {
        var go = new GameObject("Стена");
        _spawned.Add(go);
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();
        var element = go.AddComponent<KitchenElement>();
        element.PartName = "Стена";
        element.DimensionsMM = dims;
        go.AddComponent<Wall>();
        return element;
    }

    [TearDown]
    public void TearDown()
    {
        TextureOverlayHandles.End();
        SetInstance(null);
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    [Test]
    public void OnDestroy_ClearsInstance()
    {
        var go = new GameObject("Toast");
        var toast = go.AddComponent<ToastNotification>();
        CallAwake(toast);

        CallOnDestroy(toast);
        Object.DestroyImmediate(go);

        Assert.IsNull(ToastNotification.Instance,
            "статик обязан отпустить уничтоженный объект, иначе первый же тост после смены сцены падает");
    }

    [Test]
    public void OnDestroy_OfOldToast_KeepsCurrentInstance()
    {
        var oldGo = new GameObject("ToastOld");
        var old = oldGo.AddComponent<ToastNotification>();
        CallAwake(old);

        var freshGo = new GameObject("ToastFresh");
        _spawned.Add(freshGo);
        var fresh = freshGo.AddComponent<ToastNotification>();
        CallAwake(fresh);   // Instance = fresh

        CallOnDestroy(old);
        Object.DestroyImmediate(oldGo);

        Assert.AreSame(fresh, ToastNotification.Instance,
            "умирающий старый экземпляр не должен затирать текущий");
    }

    [Test]
    public void ShowIfAvailable_WithNoInstance_DoesNothing()
    {
        SetInstance(null);
        Assert.DoesNotThrow(() => ToastNotification.ShowIfAvailable("нет тоста — нет и падения"));
    }

    [Test]
    public void ShowIfAvailable_WithDestroyedInstance_DoesNothing()
    {
        SetInstance(CreateDestroyedToast());   // статик не успел обнулиться
        Assert.DoesNotThrow(() => ToastNotification.ShowIfAvailable("мёртвый тост молчит"));
    }

    [Test]
    public void TextureOverlayHandles_Begin_AllSide_WithDestroyedToast_DoesNotThrow()
    {
        var wall = CreateWall(new Vector3Int(3000, 2500, 100));
        wall.SetTextureOverlays(new[] { TextureOverlaySpec.FullFace(OverlaySide.All, "oak") });
        SetInstance(CreateDestroyedToast());

        Assert.DoesNotThrow(() => TextureOverlayHandles.Begin(wall, 0));
        Assert.IsFalse(TextureOverlayHandles.Active, "«(все)» по-прежнему отклоняется");
    }
}
