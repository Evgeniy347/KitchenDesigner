using System.Collections.Generic;
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

        window.SnapToWall();

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

    [Test]
    public void Window_SnapToWall_CentersAndInheritsThickness()
    {
        // Стена толщиной 200 мм по оси X (габарит X меньше Z).
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(200, 2500, 3000), "Wall_Thick", new Vector3(-1.5f, 1.25f, 0f));
        _spawned.Add(wallGo);

        // Окно создано рядом со стеной, но не на ней и с «чужой» толщиной.
        var winGo = ElementFactory.CreateWindow(
            new Vector3Int(900, 1200, 100), "Win_Align", new Vector3(-1.3f, 0.9f, 0.4f));
        _spawned.Add(winGo);
        var window = winGo.GetComponent<WindowElement>();
        Assert.IsNotNull(window);

        window!.SnapToWall();

        Assert.AreEqual("Wall_Thick", window.AttachedWallName);
        // Центр окна — в срединной плоскости стены.
        Assert.AreEqual(-1.5f, window.transform.position.x, Tol, "окно должно встать в середину толщины стены");
        // Остальные координаты не трогаем.
        Assert.AreEqual(0.9f, window.transform.position.y, Tol);
        Assert.AreEqual(0.4f, window.transform.position.z, Tol);
        // Глубина окна = толщина стены, ось глубины — вдоль толщины (±X).
        Assert.AreEqual(200, window.DimensionsMM.z, "глубина окна должна наследовать толщину стены");
        Assert.AreEqual(1f, Mathf.Abs(Vector3.Dot(window.transform.forward, Vector3.right)), 1e-3f,
            "глубина окна должна смотреть вдоль толщины стены");
    }

    [Test]
    public void Window_Sill_IsHorizontalAndIndependentOfDepth()
    {
        var winGo = ElementFactory.CreateWindow(
            new Vector3Int(900, 1200, 100), "Win_Sill", Vector3.zero, GlassTint.Clear, 150);
        _spawned.Add(winGo);
        var window = winGo.GetComponent<WindowElement>();
        Assert.IsNotNull(window);

        var sill = winGo.transform.Find("_Static/Sill");
        Assert.IsNotNull(sill, "Sill child must exist under _Static");

        float toU = AppConstants.MM_TO_UNITS;
        // Толщина плиты постоянная, вылет — по горизонтали (ось Z).
        Assert.AreEqual(AppConstants.WINDOW_SILL_THICKNESS_MM * toU, sill!.localScale.y, 1e-5f,
            "толщина подоконника не должна зависеть от вылета");
        Assert.AreEqual(150 * toU, sill.localScale.z, 1e-5f,
            "вылет подоконника — горизонтально от стены");
        float yAt150 = sill.localPosition.y;

        // Меняем вылет — вертикальное положение не меняется.
        window!.SillProtrusionMM = 50;
        sill = winGo.transform.Find("_Static/Sill");
        Assert.AreEqual(yAt150, sill!.localPosition.y, 1e-5f,
            "подоконник не должен «уезжать вверх» при изменении вылета");
        Assert.AreEqual(50 * toU, sill.localScale.z, 1e-5f);

        // Меняем толщину окна — размеры подоконника прежние.
        window.DimensionsMM = new Vector3Int(900, 1200, 250);
        sill = winGo.transform.Find("_Static/Sill");
        Assert.AreEqual(AppConstants.WINDOW_SILL_THICKNESS_MM * toU, sill!.localScale.y, 1e-5f);
        Assert.AreEqual(50 * toU, sill.localScale.z, 1e-5f,
            "вылет подоконника не должен зависеть от толщины окна");
    }

    [Test]
    public void Window_Open_FrameStays_SashRotates()
    {
        var winGo = ElementFactory.CreateWindow(
            new Vector3Int(900, 1200, 100), "Win_Open", new Vector3(0f, 0.6f, 0f));
        _spawned.Add(winGo);
        var window = winGo.GetComponent<WindowElement>();
        Assert.IsNotNull(window);

        var pos0 = winGo.transform.position;
        var rot0 = winGo.transform.rotation;
        var frame = winGo.transform.Find("_Static/FrameLeft");
        var framePos0 = frame!.position;

        window!.SetOpen(true);
        window.StepDoor(1f); // мгновенно до конца анимации

        Assert.AreEqual(0f, (winGo.transform.position - pos0).magnitude, 1e-6f,
            "корень окна не должен двигаться при открывании");
        Assert.AreEqual(0f, Quaternion.Angle(winGo.transform.rotation, rot0), 1e-4f,
            "корень окна не должен поворачиваться при открывании");
        Assert.AreEqual(0f, (frame.position - framePos0).magnitude, 1e-6f,
            "коробка (рама) должна оставаться на месте");

        var sash = winGo.transform.Find("_Sash");
        Assert.IsNotNull(sash, "Sash group must exist");
        Assert.AreEqual(90f, Quaternion.Angle(sash!.localRotation, Quaternion.identity), 0.5f,
            "створка должна распахнуться на 90°");
    }

    [Test]
    public void Window_AtWallCorner_DoesNotFlipBetweenWalls()
    {
        // Две стены под прямым углом; окно на стене A почти в углу — чуть ближе
        // к стене B. Без гистерезиса привязка перещёлкивалась бы на B.
        var wallA = ElementFactory.CreateWall(
            new Vector3Int(3000, 2500, 100), "Wall_A", new Vector3(0f, 1.25f, -1.5f));
        _spawned.Add(wallA);
        var wallB = ElementFactory.CreateWall(
            new Vector3Int(100, 2500, 3000), "Wall_B", new Vector3(-1.5f, 1.25f, 0f));
        _spawned.Add(wallB);

        var winGo = ElementFactory.CreateWindow(
            new Vector3Int(900, 1200, 100), "Win_Corner", new Vector3(-1.44f, 1.2f, -1.5f));
        _spawned.Add(winGo);
        var window = winGo.GetComponent<WindowElement>();
        Assert.IsNotNull(window);

        window!.SnapToWall();
        Assert.AreEqual("Wall_A", window.AttachedWallName, "первая привязка — по ближайшему боксу");

        // Окно на стене A (в углу оно «внутри» бокса B, расстояния почти равны):
        // повторные снапы не должны перекидывать его на B.
        for (int i = 0; i < 10; i++) window.SnapToWall();
        Assert.AreEqual("Wall_A", window.AttachedWallName, "гистерезис должен удерживать текущую стену");

        // Явный перенос к стене B — привязка переключается.
        winGo.transform.position = new Vector3(-1.5f, 1.2f, 0.5f);
        winGo.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        window.SnapToWall();
        Assert.AreEqual("Wall_B", window.AttachedWallName);
    }

    [Test]
    public void Window_LoweredWall_KeepsAttachment()
    {
        // Опущенная камерой стена (WallCutaway) не должна «отпугивать» привязку —
        // иначе окно скачет в зависимости от положения камеры.
        var wallGoA = ElementFactory.CreateWall(
            new Vector3Int(3000, 2500, 100), "Wall_Low", new Vector3(0f, 1.25f, -1.5f));
        _spawned.Add(wallGoA);
        // Перегородка ближе, чем «расстояние» до опущенной стены по старой
        // логике (окно на y=1.2 против верха опущенного бокса y=0.2 → ~1.0 м).
        var wallGoB = ElementFactory.CreateWall(
            new Vector3Int(3000, 2500, 100), "Wall_Far", new Vector3(0f, 1.25f, -0.9f));
        _spawned.Add(wallGoB);

        var winGo = ElementFactory.CreateWindow(
            new Vector3Int(900, 1200, 100), "Win_Low", new Vector3(0f, 1.2f, -1.5f));
        _spawned.Add(winGo);
        var window = winGo.GetComponent<WindowElement>();
        window!.SnapToWall();
        Assert.AreEqual("Wall_Low", window.AttachedWallName);

        var wall = wallGoA.GetComponent<Wall>();
        wall!.SetLowered(true, 0.1f);
        window.SnapToWall();
        Assert.AreEqual("Wall_Low", window.AttachedWallName,
            "привязка должна считаться по полной высоте опущенной стены");
        wall.RestoreFull();
    }

    [Test]
    public void Window_RestoredFromSave_RegistersInWall()
    {
        // После загрузки сцены имя стены уже записано в окно, но стена о нём не
        // знает — снап обязан зарегистрировать окно, иначе вырез не строится.
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(100, 2700, 7240), "LeftWall", new Vector3(-1.635f, 1.35f, 0f));
        _spawned.Add(wallGo);
        var wall = wallGo.GetComponent<Wall>();

        var winGo = ElementFactory.CreateWindow(
            new Vector3Int(900, 1200, 100), "Win_Loaded", new Vector3(-1.635f, 1.35f, 0f));
        _spawned.Add(winGo);
        var window = winGo.GetComponent<WindowElement>();
        window!.AttachedWallName = "LeftWall"; // как после загрузки сейва

        Assert.IsFalse(wall!.HasWindow(window), "до снапа стена окно не знает");

        window.SnapToWall();

        Assert.IsTrue(wall.HasWindow(window), "снап должен зарегистрировать окно в стене");
        var mesh = wallGo.GetComponent<MeshFilter>()!.sharedMesh;
        Assert.IsNotNull(mesh);
        Assert.Greater(mesh!.vertexCount, 24, "в мешe стены должен появиться вырез (сплошной куб — 24 вершины)");
    }

    [Test]
    public void Window_Root_HasUnitScale()
    {
        // Дети строятся в мировых единицах: масштаб корня обязан быть единичным,
        // иначе геометрия (подоконник, рама) масштабируется дважды.
        var winGo = ElementFactory.CreateWindow(
            new Vector3Int(900, 1200, 100), "Win_Scale", Vector3.zero);
        _spawned.Add(winGo);

        Assert.AreEqual(0f, (winGo.transform.localScale - Vector3.one).magnitude, 1e-6f);

        // Габарит рамы по вертикали = высоте окна (раньше рама торчала выше бокса).
        var frame = winGo.transform.Find("_Static/FrameLeft");
        Assert.AreEqual(1.2f, frame!.lossyScale.y, 1e-5f);
    }

    [Test]
    public void Window_Height_ClampedToWallHeight()
    {
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(100, 2000, 3000), "Wall_H", new Vector3(0, 1.0f, 0));
        _spawned.Add(wallGo);

        var winGo = ElementFactory.CreateWindow(
            new Vector3Int(900, 3000, 100), "Win_H", new Vector3(0, 1.0f, 0));
        _spawned.Add(winGo);
        var window = winGo.GetComponent<WindowElement>();
        Assert.IsNotNull(window);

        window!.SnapToWall();

        Assert.AreEqual("Wall_H", window.AttachedWallName);
        Assert.AreEqual(2000, window.DimensionsMM.y,
            "высота окна должна быть обрезана до высоты стены");
    }

    [Test]
    public void Window_YPosition_ClampedToWallBounds()
    {
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(100, 2500, 3000), "Wall_Pos", new Vector3(0, 1.25f, 0));
        _spawned.Add(wallGo);

        var winGo = ElementFactory.CreateWindow(
            new Vector3Int(900, 500, 100), "Win_Pos", new Vector3(0, 2.5f, 0));
        _spawned.Add(winGo);
        var window = winGo.GetComponent<WindowElement>();
        Assert.IsNotNull(window);

        window!.SnapToWall();

        float toU = AppConstants.MM_TO_UNITS;
        float wallTop = 1.25f + 2500 * toU * 0.5f;
        float winHalfH = window.DimensionsMM.y * toU * 0.5f;
        float expectedMaxY = wallTop - winHalfH;

        Assert.LessOrEqual(window.transform.position.y, expectedMaxY + 1e-5f,
            "центр окна не должен быть выше верхней границы стены");
    }

    [Test]
    public void Window_WithinWall_NotClamped()
    {
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(100, 2500, 3000), "Wall_OK", new Vector3(0, 1.25f, 0));
        _spawned.Add(wallGo);

        var winGo = ElementFactory.CreateWindow(
            new Vector3Int(900, 1200, 100), "Win_OK", new Vector3(0, 1.0f, 0));
        _spawned.Add(winGo);
        var window = winGo.GetComponent<WindowElement>();
        Assert.IsNotNull(window);

        window!.SnapToWall();

        Assert.AreEqual(1200, window.DimensionsMM.y,
            "высота окна, умещающегося в стену, не должна меняться");
        Assert.AreEqual(1.0f, window.transform.position.y, Tol,
            "позиция окна, умещающегося в стену, не должна меняться");
    }
}
