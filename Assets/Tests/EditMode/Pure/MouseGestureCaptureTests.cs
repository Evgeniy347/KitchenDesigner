using NUnit.Framework;
using KitchenDesigner.Core.Keybinding;
using KitchenDesigner.Core.UI;

/// <summary>
/// Захват жеста мыши целиком решается здесь, без сцены: Unity-сторона только опрашивает
/// мышь и отдаёт кадр, а «что из этого получился жест» — вот эта машина состояний.
///
/// Главная ловушка живёт в первой же строке: ячейку ОТКРЫВАЮТ кликом мыши, и тот же
/// клик не имеет права стать записанным жестом. Поэтому машина взводится только после
/// того, как все кнопки отпущены, и до взвода не смотрит ни на нажатия, ни на колесо.
/// Тест на это стоит первым, и он падает, если убрать взвод.
/// </summary>
public class MouseGestureCaptureTests
{
    private static MousePoll Idle(bool ctrl = false, bool alt = false, bool shift = false) =>
        new MousePoll(false, MouseButtonKind.None, MouseButtonKind.None, false, false, ctrl, alt, shift);

    private static MousePoll Down(MouseButtonKind button, bool ctrl = false, bool alt = false,
        bool shift = false) =>
        new MousePoll(true, button, MouseButtonKind.None, false, false, ctrl, alt, shift);

    private static MousePoll Up(MouseButtonKind button, bool moved = false) =>
        new MousePoll(false, MouseButtonKind.None, button, false, moved);

    private static MousePoll Held(bool moved) =>
        new MousePoll(true, MouseButtonKind.None, MouseButtonKind.None, false, moved);

    private static MousePoll Wheel(bool ctrl = false, bool alt = false, bool shift = false) =>
        new MousePoll(false, MouseButtonKind.None, MouseButtonKind.None, true, false, ctrl, alt, shift);

    [Test]
    public void TheClickThatOpenedTheCell_IsNotRecorded()
    {
        var capture = new MouseGestureCapture();

        Assert.IsNull(capture.Step(Down(MouseButtonKind.Left)),
            "кнопка ещё зажата с того клика, которым открыли ячейку");
        Assert.IsNull(capture.Step(Up(MouseButtonKind.Left)),
            "и её отпускание — это конец ТОГО клика, а не начало жеста");
        Assert.IsTrue(capture.Armed, "после отпускания машина взводится");
    }

    [Test]
    public void AfterArming_APlainClickIsCaptured()
    {
        var capture = new MouseGestureCapture();
        capture.Step(Idle());

        Assert.IsNull(capture.Step(Down(MouseButtonKind.Left)));
        var gesture = capture.Step(Up(MouseButtonKind.Left));

        Assert.IsTrue(gesture.HasValue);
        Assert.AreEqual(new MouseGesture(MouseButtonKind.Left), gesture!.Value);
        Assert.IsFalse(gesture.Value.WithMotion, "клик без движения — это клик");
    }

    [Test]
    public void AMoveBetweenPressAndRelease_MakesItAMotionGesture()
    {
        var capture = new MouseGestureCapture();
        capture.Step(Idle());

        capture.Step(Down(MouseButtonKind.Right));
        capture.Step(Held(moved: true));
        var gesture = capture.Step(Up(MouseButtonKind.Right));

        Assert.IsTrue(gesture.HasValue);
        Assert.AreEqual(new MouseGesture(MouseButtonKind.Right, withMotion: true), gesture!.Value,
            "ПКМ с движением — это поворот камеры, а ПКМ без движения — контекстное меню; "
            + "жест обязан различать их");
    }

    [Test]
    public void TheWheel_IsCapturedOnItsOwn()
    {
        var capture = new MouseGestureCapture();
        capture.Step(Idle());

        var gesture = capture.Step(Wheel());

        Assert.IsTrue(gesture.HasValue);
        Assert.IsTrue(gesture!.Value.IsWheel);
        Assert.AreEqual(MouseGesture.Wheel(), gesture.Value);
    }

    [Test]
    public void ModifiersHeldAtThePress_TravelIntoTheGesture()
    {
        var capture = new MouseGestureCapture();
        capture.Step(Idle());

        capture.Step(Down(MouseButtonKind.Left, ctrl: true, shift: true));
        var gesture = capture.Step(Up(MouseButtonKind.Left));

        Assert.IsTrue(gesture.HasValue);
        Assert.AreEqual(new MouseGesture(MouseButtonKind.Left, withMotion: false, ctrl: true, shift: true),
            gesture!.Value);
    }

    [Test]
    public void ModifiersHeldAtTheWheel_TravelIntoTheGesture()
    {
        var capture = new MouseGestureCapture();
        capture.Step(Idle());

        var gesture = capture.Step(Wheel(ctrl: true));

        Assert.IsTrue(gesture.HasValue);
        Assert.AreEqual(MouseGesture.Wheel(ctrl: true), gesture!.Value);
    }

    [Test]
    public void ReleasingADifferentButton_DoesNotEndTheGesture()
    {
        var capture = new MouseGestureCapture();
        capture.Step(Idle());

        capture.Step(Down(MouseButtonKind.Middle));
        Assert.IsNull(capture.Step(Up(MouseButtonKind.Right)),
            "отпустили не ту кнопку — жест ещё идёт");

        var gesture = capture.Step(Up(MouseButtonKind.Middle));
        Assert.IsTrue(gesture.HasValue);
        Assert.AreEqual(MouseButtonKind.Middle, gesture!.Value.Button);
    }

    [Test]
    public void AWheelWhileAButtonIsHeld_DoesNotStealTheGesture()
    {
        var capture = new MouseGestureCapture();
        capture.Step(Idle());

        capture.Step(Down(MouseButtonKind.Left));
        var stolen = capture.Step(new MousePoll(true, MouseButtonKind.None, MouseButtonKind.None,
            wheelMoved: true, pointerMoved: false));

        Assert.IsNull(stolen,
            "колесо во время зажатой кнопки — это не отдельный жест: иначе жест «ЛКМ» "
            + "записался бы как «Колесо» на первом же случайном прокруте");
    }

    [Test]
    public void MotionWithoutAPress_IsIgnored()
    {
        var capture = new MouseGestureCapture();
        capture.Step(Idle());

        Assert.IsNull(capture.Step(new MousePoll(false, MouseButtonKind.None, MouseButtonKind.None,
            wheelMoved: false, pointerMoved: true)),
            "просто провести мышью — не жест: иначе ячейка записала бы что-то сама");
    }
}
