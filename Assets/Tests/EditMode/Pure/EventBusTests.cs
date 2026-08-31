using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class EventBusTests
{
    private struct PingEvent { public int value; }

    [TearDown]
    public void Teardown() => EventBus.Clear();

    [Test]
    public void Subscribe_Publish_InvokesHandler()
    {
        int received = 0;
        System.Action<PingEvent> h = e => received = e.value;
        EventBus.Subscribe(h);

        EventBus.Publish(new PingEvent { value = 42 });

        Assert.AreEqual(42, received);
    }

    [Test]
    public void MultipleHandlers_AllInvoked()
    {
        int a = 0, b = 0;
        System.Action<PingEvent> ha = e => a = e.value;
        System.Action<PingEvent> hb = e => b = e.value + 1;
        EventBus.Subscribe(ha);
        EventBus.Subscribe(hb);

        EventBus.Publish(new PingEvent { value = 10 });

        Assert.AreEqual(10, a);
        Assert.AreEqual(11, b);
    }

    [Test]
    public void Unsubscribe_StopsReceiving()
    {
        int received = 0;
        System.Action<PingEvent> h = e => received = e.value;
        EventBus.Subscribe(h);
        EventBus.Unsubscribe(h);

        EventBus.Publish(new PingEvent { value = 99 });

        Assert.AreEqual(0, received, "после отписки обработчик не вызывается");
    }

    [Test]
    public void Unsubscribe_LastHandler_RemovesEventType()
    {
        System.Action<PingEvent> h = e => { };
        EventBus.Subscribe(h);
        EventBus.Unsubscribe(h);
        // Повторная публикация без подписчиков не должна бросать.
        Assert.DoesNotThrow(() => EventBus.Publish(new PingEvent { value = 1 }));
    }

    [Test]
    public void Publish_NoSubscribers_NoThrow()
    {
        Assert.DoesNotThrow(() => EventBus.Publish(new PingEvent { value = 5 }));
    }

    [Test]
    public void Clear_RemovesAllSubscribers()
    {
        int received = 0;
        EventBus.Subscribe<PingEvent>(e => received = e.value);
        EventBus.Clear();

        EventBus.Publish(new PingEvent { value = 7 });

        Assert.AreEqual(0, received);
    }

    [Test]
    public void Vector3Serializer_RoundTripsVector()
    {
        var v = new Vector3(1.5f, -2.25f, 3f);
        var s = new Vector3Serializer(v);
        Assert.AreEqual(v, s.ToVector3());
    }
}
