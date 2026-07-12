window.editorLock = {
    _projectId: null,
    _lockGuid: null,

    registerRelease: function (projectId, lockGuid) {
        this._projectId = projectId;
        this._lockGuid = lockGuid;

        // Release lock when the user navigates away or closes the tab.
        window.addEventListener('beforeunload', function () {
            if (!editorLock._lockGuid) return;

            // fetch with keepalive is the only reliable way to send DELETE during unload.
            var url = '/api/projects/' + editorLock._projectId + '/lock/' + editorLock._lockGuid;
            fetch(url, { method: 'DELETE', keepalive: true }).catch(function () {});
            editorLock._lockGuid = null;
        });
    }
};
