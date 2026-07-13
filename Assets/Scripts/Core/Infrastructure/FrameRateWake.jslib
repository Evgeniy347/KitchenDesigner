var FrameRateWakeImpl = {
    // Мгновенное пробуждение из простоя: ловим ввод в браузере и синхронно будим
    // Unity через SendMessage (выполняется сразу, не дожидаясь кадра). Троттлинг —
    // достаточно будить раз в ~200мс, дальше активный FPS держит опрос ввода в C#.
    FrameRateWake_Init: function(gameObjectNamePtr) {
        if (Module.__frameRateWakeInit) return;
        Module.__frameRateWakeInit = true;

        var name = UTF8ToString(gameObjectNamePtr);
        var last = 0;

        function wake() {
            var now = Date.now();
            if (now - last < 200) return;
            last = now;
            try { SendMessage(name, 'OnBrowserActivity'); } catch (e) { }
        }

        var opts = { passive: true, capture: true };
        var events = ['pointerdown', 'pointermove', 'pointerup', 'wheel',
                      'keydown', 'touchstart', 'touchmove'];
        for (var i = 0; i < events.length; i++)
            document.addEventListener(events[i], wake, opts);
    }
};

mergeInto(LibraryManager.library, FrameRateWakeImpl);
