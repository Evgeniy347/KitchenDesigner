window.downloadFile = function (fileName, base64Data, contentType) {
    var bytes = Uint8Array.from(atob(base64Data), function (c) { return c.charCodeAt(0); });
    var blob = new Blob([bytes], { type: contentType });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};
