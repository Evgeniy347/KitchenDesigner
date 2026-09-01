using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core.UI;

public class StatusBarLifetimeTests
{
    [UnityTest]
    public IEnumerator StatusBar_Destroyed_ReleasesTheStaticInstance()
    {
        var go = new GameObject("StatusBar");
        var bar = go.AddComponent<StatusBarUI>();
        yield return null;

        Assume.That(StatusBarUI.Instance, Is.SameAs(bar), "Awake обязан выставить статик");

        Object.Destroy(go);
        yield return null;

        Assert.IsNull(StatusBarUI.Instance,
            "Статик обязан отпустить уничтоженный объект: иначе после смены сцены или "
            + "загрузки другого проекта первый же ShowTransient падает с "
            + "MissingReferenceException. Проверка живёт в PlayMode, потому что вне Play "
            + "mode Unity не зовёт ни Awake, ни OnDestroy — под EditMode такой тест зелен "
            + "всегда и не проверяет ничего");
    }

    [UnityTest]
    public IEnumerator StatusBar_DestroyingAnOldBar_KeepsTheCurrentOne()
    {
        var oldGo = new GameObject("StatusBarOld");
        oldGo.AddComponent<StatusBarUI>();
        yield return null;

        var freshGo = new GameObject("StatusBarFresh");
        var fresh = freshGo.AddComponent<StatusBarUI>();
        yield return null;

        Assume.That(StatusBarUI.Instance, Is.SameAs(fresh));

        Object.Destroy(oldGo);
        yield return null;

        Assert.AreSame(fresh, StatusBarUI.Instance,
            "Сравнение в OnDestroy идёт по ReferenceEquals: умирающий старый экземпляр не "
            + "должен обнулять текущий");

        Object.Destroy(freshGo);
        yield return null;
    }
}
