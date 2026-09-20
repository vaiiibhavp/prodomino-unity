mergeInto(LibraryManager.library, {

    // ------------------------------------------------------------
    // Opens a PayPal window and keeps a reference to it
    // ------------------------------------------------------------
    OpenPayPalWindow: function (urlPtr, objectNamePtr, callbackPtr) {

        // Convert C string to JS string
        const url = UTF8ToString(urlPtr);
        const objectName = UTF8ToString(objectNamePtr);
        const callback = UTF8ToString(callbackPtr);

        // Open PayPal window and keep reference
        window._paypalWindow = window.open(
            url,
            "_blank",
            "width=500,height=700,resizable=yes,scrollbars=yes"
        );

        if (!window._paypalWindow) {
            console.error("Failed to open PayPal window (popup blocked?)");
            return;
        }

        // Register message listener once
        if (!window._paypalListenerRegistered) {

            window.addEventListener("message", function (event) {

                if (!event || !event.data || !event.data.type) {
                    return;
                }

                // Expected messages from redirect page
                if (
                    event.data.type === "PAYPAL_RETURN" ||
                    event.data.type === "PAYPAL_CANCEL"
                ) {

                    console.log("Received PayPal message:", event.data.type);

                    // Close PayPal window if still open
                    if (window._paypalWindow && !window._paypalWindow.closed) {
                        window._paypalWindow.close();
                    }

                    window._paypalWindow = null;

                    // Notify Unity
                    if (typeof unityInstance !== "undefined") {
                        unityInstance.SendMessage(
                            objectName,
                            callback,
                            event.data.type
                        );
                    }
                }

            }, false);

            window._paypalListenerRegistered = true;
        }
    }
});
