using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using KitchenDesigner.Core.UI;

public class ConsoleOverlayTests
{
    [Test]
    public void ConsoleOverlay_Canvas_SitsAboveEveryOtherUiCanvas()
    {
        var canvas = UIFactory.CreateCanvas("SortingProbe");
        try
        {
            Assert.AreEqual(UIFactory.MainUiSortingOrder, canvas.sortingOrder,
                "обычный UI собирается именно с этим порядком — иначе сравнение ниже "
                + "сравнивает число с числом, а не консоль с интерфейсом");
            Assert.Greater(ConsoleOverlay.AboveEveryOtherCanvas, canvas.sortingOrder,
                "Оверлей логов открывается ПОВЕРХ остального интерфейса: под панелями его "
                + "не прочитать, а зовут его как раз тогда, когда на экране что-то не так");
        }
        finally
        {
            Object.DestroyImmediate(canvas.gameObject);
            var es = Object.FindAnyObjectByType<EventSystem>();
            if (es != null) Object.DestroyImmediate(es.gameObject);
        }
    }
}
