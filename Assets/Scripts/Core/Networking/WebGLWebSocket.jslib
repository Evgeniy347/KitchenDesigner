// NB: $wsInstance / $wsGameObjectName are jslib "library" variables. Each function
// that uses them MUST declare a __deps on them, otherwise Emscripten (Unity 6000.4)
// tree-shakes the declarations and the runtime throws "wsInstance is not defined".
var WebGLWebSocketImpl = {
    $wsInstance: null,
    $wsGameObjectName: null,

    WebSocketConnect__deps: ['$wsInstance', '$wsGameObjectName'],
    WebSocketConnect: function(urlPtr, gameObjectNamePtr) {
        var url = UTF8ToString(urlPtr);
        wsGameObjectName = UTF8ToString(gameObjectNamePtr);

        if (wsInstance) {
            console.log('[MCP-WS] Closing existing socket before reconnect');
            wsInstance.close();
            wsInstance = null;
        }

        try {
            wsInstance = new WebSocket(url);

            wsInstance.onopen = function() {
                console.log('[MCP-WS] WebGL WebSocket connected to ' + url);
                SendMessage(wsGameObjectName, 'OnWebSocketOpen', '');
            };

            wsInstance.onmessage = function(event) {
                if (typeof event.data === 'string') {
                    SendMessage(wsGameObjectName, 'OnWebSocketMessage', event.data);
                }
            };

            wsInstance.onerror = function(error) {
                console.error('[MCP-WS] WebGL WebSocket error:', error);
            };

            wsInstance.onclose = function(event) {
                console.log('[MCP-WS] WebGL WebSocket closed: code=' + event.code + ' reason=' + event.reason);
                wsInstance = null;
            };
        } catch (e) {
            console.error('[MCP-WS] WebGL WebSocket connect failed:', e);
        }
    },

    WebSocketSend__deps: ['$wsInstance'],
    WebSocketSend: function(messagePtr) {
        var message = UTF8ToString(messagePtr);
        if (wsInstance && wsInstance.readyState === WebSocket.OPEN) {
            wsInstance.send(message);
        } else {
            console.warn('[MCP-WS] WebSocket not open, cannot send');
        }
    },

    WebSocketClose__deps: ['$wsInstance'],
    WebSocketClose: function() {
        if (wsInstance) {
            wsInstance.close();
            wsInstance = null;
        }
    },

    ShowLockTakenAlert: function() {
        alert('Проект открыт на соседней вкладке, текущие изменения не будут сохранены.');
    }
};

mergeInto(LibraryManager.library, WebGLWebSocketImpl);
