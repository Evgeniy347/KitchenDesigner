using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Сторож против «Script error (X: Start) can not take parameters».
///
/// Имена вроде Start, Awake, Update — точки жизненного цикла Unity. Движок ищет
/// их по имени и требует пустой список параметров; метод с тем же именем, но с
/// аргументами, компилируется без единого предупреждения и печатает КРАСНУЮ
/// строку в консоли плеера при каждой загрузке скрипта. Компилятор её не видит,
/// ни один обычный тест её не видит — увидеть можно только глазами в консоли
/// собранного приложения. Именно так дефект в UnityWebRequestDownloader.Start
/// прожил от коммита b481a985 до 2026-09-04.
///
/// Поэтому список имён здесь выписан руками (движок его тоже держит руками), а
/// список классов берётся отражением и не устаревает.
///
/// Подсадной нарушитель НЕ является MonoBehaviour намеренно: настоящий
/// MonoBehaviour с таким методом печатал бы ровно ту ошибку, ради истребления
/// которой этот файл и написан. Поэтому сбор нарушителей отделён от фильтра по
/// MonoBehaviour, и каждая половина проверяется своим тестом.
/// </summary>
public class UnityMagicMethodSignatureTests
{
    private static readonly string[] MustTakeNoParameters =
    {
        "Awake", "Start", "Update", "FixedUpdate", "LateUpdate",
        "OnEnable", "OnDisable", "OnDestroy", "OnGUI", "OnValidate", "Reset",
        "OnApplicationQuit", "OnBecameVisible", "OnBecameInvisible",
        "OnPreCull", "OnPreRender", "OnPostRender", "OnRenderObject", "OnWillRenderObject",
        "OnDrawGizmos", "OnDrawGizmosSelected",
        "OnMouseDown", "OnMouseUp", "OnMouseUpAsButton",
        "OnMouseEnter", "OnMouseExit", "OnMouseOver", "OnMouseDrag",
        "OnTransformChildrenChanged", "OnTransformParentChanged",
        "OnBeforeTransformParentChanged", "OnRectTransformDimensionsChange",
        "OnCanvasGroupChanged", "OnAnimatorMove", "OnDidApplyAnimationProperties",
    };

    private static IEnumerable<Type> OurBehaviours()
    {
        var asm = typeof(KitchenElement).Assembly;
        return asm.GetTypes()
            .Where(t => typeof(MonoBehaviour).IsAssignableFrom(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal);
    }

    private static string CollectOffenders(IEnumerable<Type> types)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var offenders = new StringBuilder();
        foreach (var type in types)
        {
            foreach (var method in type.GetMethods(flags))
            {
                if (!MustTakeNoParameters.Contains(method.Name)) continue;
                if (method.GetParameters().Length == 0) continue;

                var args = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name));
                offenders.AppendLine($"  {type.FullName}.{method.Name}({args})");
            }
        }
        return offenders.ToString();
    }

    [Test]
    public void MonoBehaviour_MagicLifecycleName_NeverDeclaresParameters()
    {
        string offenders = CollectOffenders(OurBehaviours());

        Assert.That(offenders, Is.Empty,
            "Метод MonoBehaviour с магическим именем жизненного цикла не может брать параметры: "
            + "движок печатает «Script error (Класс: Имя) can not take parameters» в консоль "
            + "плеера при каждой загрузке скрипта, а компилятор молчит. Переименуй метод "
            + "(и объявление интерфейса, из которого пришло имя) во что-то по смыслу — "
            + $"IInstallerDownloader.Start стал BeginDownload.\n{offenders}");
    }

    [Test]
    public void Collector_MethodNamedStartWithParameters_IsReported()
    {
        string offenders = CollectOffenders(new[] { typeof(PlantedOffender) });

        Assert.That(offenders, Does.Contain("Start"),
            "сторож обязан ловить подсадной Start с параметром — иначе он не может покраснеть");
        Assert.That(offenders, Does.Contain("String"),
            "в отчёте должны быть типы параметров, иначе по нему нечего чинить");
    }

    [Test]
    public void Collector_ParameterlessMagicMethod_IsNotReported()
    {
        string offenders = CollectOffenders(new[] { typeof(PlantedInnocent) });

        Assert.That(offenders, Is.Empty,
            "нормальный Start() без параметров — не нарушение; иначе сторож красит весь проект");
    }

    [Test]
    public void Scan_CoversRealBehaviours_AndNamesTheDownloaderAmongThem()
    {
        var behaviours = OurBehaviours().ToList();

        Assert.That(behaviours.Count, Is.GreaterThan(20),
            "отражение вернуло подозрительно мало MonoBehaviour — сборка сменила имя или фильтр сломан");
        Assert.That(behaviours.Any(t => t.Name == "UnityWebRequestDownloader"), Is.True,
            "класс, из-за которого написан этот сторож, обязан попадать в область сканирования");
    }

    private sealed class PlantedOffender
    {
        public void Start(string thisWouldBreakTheEngine) { _ = thisWouldBreakTheEngine; }
    }

    private sealed class PlantedInnocent
    {
        public void Start() { }
    }
}
