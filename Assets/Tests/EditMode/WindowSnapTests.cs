using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class WindowSnapTests : SnapTestBase
{
    [Test]
    public void Window_AttachesToNearestWall()
    {
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(100, 2500, 3000), "Wall_Test", new Vector3(0, 1.25f, -1.5f));
        _spawned.Add(wallGo);
        var wall = wallGo.GetComponent<KitchenElement>();
        PartRegistry.Register(wall);
        Assert.IsNotNull(wall);

        var winGo = ElementFactory.CreateWindow(
            new Vector3Int(900, 1200, 100), "Win_Test", new Vector3(0, 0.6f, -1.5f));
        _spawned.Add(winGo);
        var window = winGo.GetComponent<WindowElement>();
        PartRegistry.Register(window);
        Assert.IsNotNull(window);

        var method = typeof(WindowElement).GetMethod("RegisterWithNearestWall",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "RegisterWithNearestWall must exist");
        method.Invoke(window, null);

        Assert.IsNotEmpty(window.AttachedWallName, "Window should attach to nearest wall");
        Assert.AreEqual("Wall_Test", window.AttachedWallName);
    }

    [Test]
    public void Window_CreatedWithoutWall_NoAttachment()
    {
        var winGo = ElementFactory.CreateWindow(
            new Vector3Int(900, 1200, 100), "Win_NoWall", Vector3.zero);
        _spawned.Add(winGo);
        var window = winGo.GetComponent<WindowElement>();
        Assert.IsNotNull(window);

        Assert.IsEmpty(window.AttachedWallName);
    }

    [Test]
    public void Window_SnapMove_StaysOnWall()
    {
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(100, 2500, 3000), "Wall_Snap", new Vector3(0, 1.25f, -1.5f));
        _spawned.Add(wallGo);
        var wall = wallGo.GetComponent<KitchenElement>();
        Assert.IsNotNull(wall);

        var winGo = ElementFactory.CreateWindow(
            new Vector3Int(900, 1200, 100), "Win_Snap", new Vector3(0, 0.6f, -1.5f));
        _spawned.Add(winGo);
        var window = winGo.GetComponent<KitchenElement>();
        Assert.IsNotNull(window);

        var result = Snap(window, wall, new Vector3(0.2f, 0.6f, -1.5f));
        Assert.IsNotNull(result);
    }
}
