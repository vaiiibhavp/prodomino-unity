mergeInto(LibraryManager.library, {
    
    InitFirebase: function (configJson, objectName, callback, fallback) {
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {
            const configJsonParsed = UTF8ToString(configJson);
            const config = JSON.parse(configJsonParsed);
            firebaseApp.initializeApp(config);

            unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, "✅ Firebase initialized correctly");
        } 
        catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, "❌ Error initializing Firebase:" + error);
        }
    },
});
