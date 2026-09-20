mergeInto(LibraryManager.library, {

    GetCurrentProjectId: function () {
        var projectId = firebaseConfig.projectId;
        var bufferSize = lengthBytesUTF8(projectId) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(projectId, buffer, bufferSize);
        return buffer;
    },

    CopyTextToClipboard: function (textPtr) {
        var text = UTF8ToString(textPtr);
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(text).then(function () {
                console.log("Text copied to clipboard");
            }).catch(function (err) {
                console.error("Clipboard copy failed", err);
            });
        } else {
            console.warn("Clipboard API not supported");
        }
    }
});