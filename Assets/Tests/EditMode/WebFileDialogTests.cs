using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Покрытие браузерного файлового моста WebGL (WebFileDialog + приёмник):
/// разбор полезной нагрузки, имя по умолчанию для «Сохранить как», диспетчеризация
/// колбэков и сквозной прогон сохранённого JSON через разбор → десериализацию.
/// Сам вызов jslib-externa и SendMessage в редакторе не выполняются.
/// </summary>
public class WebFileDialogTests
{
    private const char Sep = '\x1F';

    // ── TryParseOpenPayload ────────────────────────────────────────────────

    [Test]
    public void TryParseOpenPayload_ValidPayload_SplitsNameAndContent()
    {
        bool ok = WebFileDialog.TryParseOpenPayload(
            "kitchen.json" + Sep + "{\"foo\":1}", out var name, out var content);

        Assert.IsTrue(ok);
        Assert.AreEqual("kitchen.json", name);
        Assert.AreEqual("{\"foo\":1}", content);
    }

    [Test]
    public void TryParseOpenPayload_ContentWithBraces_KeepsFullContentAfterFirstSeparator()
    {
        // Разделитель — только первый \x1F; фигурные скобки/переводы строк внутри JSON целы.
        string json = "{\n  \"a\": \"b\",\n  \"c\": [1,2,3]\n}";
        bool ok = WebFileDialog.TryParseOpenPayload(
            "proj.json" + Sep + json, out var name, out var content);

        Assert.IsTrue(ok);
        Assert.AreEqual("proj.json", name);
        Assert.AreEqual(json, content);
    }

    [Test]
    public void TryParseOpenPayload_EmptyContent_ReturnsTrueWithEmptyContent()
    {
        bool ok = WebFileDialog.TryParseOpenPayload("a.json" + Sep, out var name, out var content);

        Assert.IsTrue(ok);
        Assert.AreEqual("a.json", name);
        Assert.AreEqual("", content);
    }

    [Test]
    public void TryParseOpenPayload_NoSeparator_ReturnsFalse()
    {
        bool ok = WebFileDialog.TryParseOpenPayload("no-separator-here", out var name, out var content);

        Assert.IsFalse(ok);
        Assert.IsNull(name);
        Assert.IsNull(content);
    }

    [Test]
    public void TryParseOpenPayload_NullOrEmpty_ReturnsFalse()
    {
        Assert.IsFalse(WebFileDialog.TryParseOpenPayload(null!, out _, out _));
        Assert.IsFalse(WebFileDialog.TryParseOpenPayload("", out _, out _));
    }

    // ── SuggestedFileName ──────────────────────────────────────────────────

    [Test]
    public void SuggestedFileName_EmptyOrNull_ReturnsDefault()
    {
        Assert.AreEqual("kitchen.json", WebFileDialog.SuggestedFileName(null!));
        Assert.AreEqual("kitchen.json", WebFileDialog.SuggestedFileName(""));
    }

    [Test]
    public void SuggestedFileName_JsonPath_ReturnsFileName()
    {
        Assert.AreEqual("proj.json",
            WebFileDialog.SuggestedFileName("C:/saves/proj.json"));
        Assert.AreEqual("proj.json",
            WebFileDialog.SuggestedFileName("/home/user/proj.json"));
    }

    [Test]
    public void SuggestedFileName_NonJsonPath_ReturnsDefault()
    {
        // Например, серверный id проекта (GUID) — не годится как имя файла.
        Assert.AreEqual("kitchen.json",
            WebFileDialog.SuggestedFileName("3f2504e0-4f89-41d3-9a0c-0305e82c3301"));
    }

    // ── Приёмник колбэков ──────────────────────────────────────────────────

    private WebFileDialogReceiver MakeReceiver()
    {
        var go = new GameObject("TestReceiver");
        return go.AddComponent<WebFileDialogReceiver>();
    }

    [Test]
    public void Receiver_OnWebGLFileOpened_InvokesPendingOpenWithParsedParts()
    {
        var r = MakeReceiver();
        string? gotName = null, gotContent = null;
        int calls = 0;
        r.PendingOpen = (n, c) => { gotName = n; gotContent = c; calls++; };

        r.OnWebGLFileOpened("kitchen.json" + Sep + "{\"x\":1}");

        Assert.AreEqual(1, calls);
        Assert.AreEqual("kitchen.json", gotName);
        Assert.AreEqual("{\"x\":1}", gotContent);

        // Колбэк одноразовый: повторный вызов ничего не делает.
        r.OnWebGLFileOpened("other.json" + Sep + "{}");
        Assert.AreEqual(1, calls);

        Object.DestroyImmediate(r.gameObject);
    }

    [Test]
    public void Receiver_OnWebGLFileOpened_MalformedPayload_DoesNotInvoke()
    {
        var r = MakeReceiver();
        int calls = 0;
        r.PendingOpen = (n, c) => calls++;

        r.OnWebGLFileOpened("no-separator");

        Assert.AreEqual(0, calls);
        Object.DestroyImmediate(r.gameObject);
    }

    [Test]
    public void Receiver_OnWebGLFileSaved_InvokesPendingSavedOnce()
    {
        var r = MakeReceiver();
        string? saved = null;
        int calls = 0;
        r.PendingSaved = n => { saved = n; calls++; };

        r.OnWebGLFileSaved("kitchen.json");

        Assert.AreEqual(1, calls);
        Assert.AreEqual("kitchen.json", saved);

        r.OnWebGLFileSaved("again.json");
        Assert.AreEqual(1, calls);

        Object.DestroyImmediate(r.gameObject);
    }

    // ── Сквозной прогон загрузки ───────────────────────────────────────────

    [Test]
    public void OpenedFileContent_RoundTripsThroughDeserialize()
    {
        var spawned = new List<GameObject>();
        try
        {
            // Чистая сцена — снимок должен содержать только нашу деталь.
            foreach (var el in Object.FindObjectsByType<KitchenElement>())
                if (el != null) Object.DestroyImmediate(el.gameObject);

            var go = new GameObject("RoundTripBoard");
            var e = go.AddComponent<KitchenElement>();
            e.PartName = "RoundTripBoard";
            e.DimensionsMM = new Vector3Int(800, 400, 18);
            PartRegistry.Register(e);
            spawned.Add(go);

            // Как это делает SaveAs: снимок сцены → JSON.
            string json = SaveLoadManager.CaptureCurrentJson();

            // Как это приходит из браузера: "имя\x1Fсодержимое".
            string payload = "RoundTrip.json" + Sep + json;
            Assert.IsTrue(WebFileDialog.TryParseOpenPayload(payload, out var name, out var content));
            Assert.AreEqual("RoundTrip.json", name);

            // Как это делает LoadDialog на WebGL: разобранное содержимое → сцена.
            var data = SaveLoadManager.Deserialize(content!);
            Assert.IsNotNull(data);

            foreach (var g in spawned)
            {
                PartRegistry.Unregister(g.GetComponent<KitchenElement>());
                Object.DestroyImmediate(g);
            }
            spawned.Clear();

            var restored = SaveLoadManager.RestoreScene(data!);
            Assert.AreEqual(1, restored.Count);
            Assert.AreEqual("RoundTripBoard",
                restored[0].GetComponent<KitchenElement>().PartName);
        }
        finally
        {
            foreach (var g in spawned)
            {
                if (g == null) continue;
                var el = g.GetComponent<KitchenElement>();
                if (el != null) PartRegistry.Unregister(el);
                Object.DestroyImmediate(g);
            }
            foreach (var el in Object.FindObjectsByType<KitchenElement>())
                if (el != null) Object.DestroyImmediate(el.gameObject);
        }
    }
}
