using NUnit.Framework;
using KitchenDesigner.Core;

public class ElementFactorySandboxScopeTests
{
    [Test]
    public void IsActive_FalseOutsideAnyScope()
    {
        Assert.IsFalse(ElementFactorySandbox.IsActive);
    }

    [Test]
    public void IsActive_TrueInsideScope_FalseAfterDispose()
    {
        Assert.IsFalse(ElementFactorySandbox.IsActive);
        using (ElementFactorySandbox.Enter())
        {
            Assert.IsTrue(ElementFactorySandbox.IsActive);
        }
        Assert.IsFalse(ElementFactorySandbox.IsActive);
    }

    [Test]
    public void NestedScopes_StayActiveUntilOutermostDisposes()
    {
        using (ElementFactorySandbox.Enter())
        {
            using (ElementFactorySandbox.Enter())
            {
                Assert.IsTrue(ElementFactorySandbox.IsActive);
            }
            Assert.IsTrue(ElementFactorySandbox.IsActive,
                "внутренний Dispose закрыл ВНЕШНИЙ scope: счётчик не считает вложенность");
        }
        Assert.IsFalse(ElementFactorySandbox.IsActive);
    }

    [Test]
    public void DoubleDispose_DoesNotUnderflowBelowZero()
    {
        var scope = ElementFactorySandbox.Enter();
        scope.Dispose();
        scope.Dispose();
        Assert.IsFalse(ElementFactorySandbox.IsActive);

        using (ElementFactorySandbox.Enter())
        {
            Assert.IsTrue(ElementFactorySandbox.IsActive);
        }
        Assert.IsFalse(ElementFactorySandbox.IsActive,
            "двойной Dispose увёл счётчик в минус — следующий Enter/Dispose не закрывает scope");
    }
}
