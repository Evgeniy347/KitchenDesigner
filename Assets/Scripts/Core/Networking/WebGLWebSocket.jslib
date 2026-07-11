var WebGLWebSocketImpl = {
    $wsInstance: null,
    $wsGameObjectName: null,

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

    WebSocketSend: function(messagePtr) {
        var message = UTF8ToString(messagePtr);
        if (wsInstance && wsInstance.readyState === WebSocket.OPEN) {
            wsInstance.send(message);
        } else {
            console.warn('[MCP-WS] WebSocket not open, cannot send');
        }
    },

    WebSocketClose: function() {
        if (wsInstance) {
            wsInstance.close();
            wsInstance = null;
        }
    }
};

mergeInto(LibraryManager.library, WebGLWebSocketImpl);
