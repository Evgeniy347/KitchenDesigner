var FileDialogImpl = {
    FileDialogOpen: function(gameObjectNamePtr) {
        var gameObjectName = UTF8ToString(gameObjectNamePtr);

        if (window.showOpenFilePicker) {
            window.showOpenFilePicker({
                types: [{
                    description: 'Kitchen Project',
                    accept: { 'application/json': ['.json'] }
                }],
                multiple: false
            }).then(function(handles) {
                handles[0].getFile().then(function(file) {
                    file.text().then(function(content) {
                        SendMessage(gameObjectName, 'OnWebGLFileOpened', file.name + '\x1F' + content);
                    });
                });
            }).catch(function(err) {
                console.log('[FileDialog] Open cancelled:', err.name);
            });
        } else {
            var input = document.createElement('input');
            input.type = 'file';
            input.accept = '.json';
            input.style.display = 'none';
            document.body.appendChild(input);
            input.onchange = function(e) {
                var file = e.target.files[0];
                if (!file) { document.body.removeChild(input); return; }
                var reader = new FileReader();
                reader.onload = function() {
                    SendMessage(gameObjectName, 'OnWebGLFileOpened', file.name + '\x1F' + reader.result);
                    document.body.removeChild(input);
                };
                reader.readAsText(file);
            };
            input.click();
        }
    },

    FileDialogSave: function(gameObjectNamePtr, contentPtr, defaultNamePtr) {
        var gameObjectName = UTF8ToString(gameObjectNamePtr);
        var content = UTF8ToString(contentPtr);
        var defaultName = UTF8ToString(defaultNamePtr) || 'kitchen.json';

        if (window.showSaveFilePicker) {
            window.showSaveFilePicker({
                suggestedName: defaultName,
                types: [{
                    description: 'Kitchen Project',
                    accept: { 'application/json': ['.json'] }
                }]
            }).then(function(handle) {
                handle.createWritable().then(function(writable) {
                    writable.write(content).then(function() {
                        writable.close().then(function() {
                            SendMessage(gameObjectName, 'OnWebGLFileSaved', defaultName);
                        });
                    });
                });
            }).catch(function(err) {
                console.log('[FileDialog] Save cancelled:', err.name);
            });
        } else {
            var blob = new Blob([content], { type: 'application/json' });
            var url = URL.createObjectURL(blob);
            var a = document.createElement('a');
            a.href = url;
            a.download = defaultName;
            a.style.display = 'none';
            document.body.appendChild(a);
            a.click();
            setTimeout(function() {
                document.body.removeChild(a);
                URL.revokeObjectURL(url);
            }, 200);
            SendMessage(gameObjectName, 'OnWebGLFileSaved', defaultName);
        }
    }
};

mergeInto(LibraryManager.library, FileDialogImpl);
