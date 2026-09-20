mergeInto(LibraryManager.library, {
    ShowInterstitial: function (objectNamePtr, callbackPtr) {

        // Convert Unity pointers to JS strings
        const objectName = UTF8ToString(objectNamePtr);
        const callback = UTF8ToString(callbackPtr);

        // Reference to the global Google Publisher Tag object
        const googletag = window.googletag;

        // Validate that GPT is properly initialized
        if (!googletag || !googletag.apiReady || !googletag.enums) {
            console.warn("[Ads] GPT not ready. Interstitial cannot be shown.");
            unityInstance.SendMessage(objectName, callback, "false");
            return;
        }
        
        if (!window.interstitialId) {
            console.warn("[Ads] Interstitial ID not configured.");
            unityInstance.SendMessage(objectName, callback, "false");
            return;
        }

        console.log("[Ads] Requesting interstitial...");

        let slot = null;
        let callbackSent = false; // Prevent multiple Unity callbacks

        try {
            // Define an interstitial (out-of-page) slot
            slot = googletag.defineOutOfPageSlot(
                window.interstitialId,
                googletag.enums.OutOfPageFormat.INTERSTITIAL
            );
        } catch (err) {
            console.warn("[Ads] Failed to define interstitial slot:", err);
            unityInstance.SendMessage(objectName, callback, "false");
            return;
        }

        // Safety check: slot creation can fail silently
        if (!slot) {
            console.warn("[Ads] Interstitial slot was not created.");
            unityInstance.SendMessage(objectName, callback, "false");
            return;
        }

        // Attach the slot to the pubads service
        slot.addService(googletag.pubads());

        // Listen for the render result (this fires even if there is no fill)
        const onSlotRenderEnded = function (event) {

            // Make sure the event belongs to our slot
            if (event.slot !== slot || callbackSent) {
                return;
            }

            callbackSent = true;

            if (event.isEmpty) {
                // No ad was returned (not approved, no fill, etc.)
                console.warn("[Ads] Interstitial request returned empty.");
                unityInstance.SendMessage(objectName, callback, "false");
            } else {
                // Interstitial successfully rendered
                console.log("[Ads] Interstitial rendered successfully.");
                unityInstance.SendMessage(objectName, callback, "true");
            }

            // Clean up: remove listeners and destroy the slot
            googletag.pubads().removeEventListener("slotRenderEnded", onSlotRenderEnded);
            googletag.destroySlots([slot]);
            slot = null;
        };

        // Register the event listener
        googletag.pubads().addEventListener("slotRenderEnded", onSlotRenderEnded);

        // Trigger the ad request
        googletag.display(slot);
    },


    ShowRewarded: function (objectNamePtr, callbackPtr) {
        const objectName = UTF8ToString(objectNamePtr);
        const callback = UTF8ToString(callbackPtr);

        // Validate GPT initialization
        if (!window.googletag || !googletag.apiReady || !googletag.enums) {
            console.warn("[Ads] Unable to initialize rewarded: GPT not ready.");
            unityInstance.SendMessage(objectName, callback, "false");
            return;
        }

        console.log("[Ads] Showing rewarded...");

        let slot;
        try {
            slot = googletag.defineOutOfPageSlot(
                '/YOUR_AD_MANAGER_REWARDED_ID',
                googletag.enums.OutOfPageFormat.REWARDED
            );
        } catch (err) {
            console.warn("[Ads] Rewarded error:", err);
            unityInstance.SendMessage(objectName, callback, "false");
            return;
        }

        if (!slot) {
            console.warn("[Ads] Rewarded slot could not be created.");
            unityInstance.SendMessage(objectName, callback, "false");
            return;
        }

        slot.addService(googletag.pubads());

        googletag.pubads().addEventListener('rewardedSlotGranted', () => {
            console.log("[Ads] Reward granted!");
            unityInstance.SendMessage(objectName, callback, "true");
        });

        googletag.pubads().addEventListener('rewardedSlotClosed', () => {
            console.log("[Ads] Rewarded ad closed.");
            unityInstance.SendMessage(objectName, callback, "false");
        });

        googletag.display(slot);
    }
});
