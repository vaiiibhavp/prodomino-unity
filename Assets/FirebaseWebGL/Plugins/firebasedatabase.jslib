mergeInto(LibraryManager.library, {

    GetJSON: function(path, objectName, callback, fallback) {
        var parsedPath = UTF8ToString(path);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {

            firebaseDatabase.ref(parsedPath).once('value').then(function(snapshot) {
                unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, JSON.stringify(snapshot.val()));
            });

        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    PostJSON: function(path, value, objectName, callback, fallback) {
        var parsedPath = UTF8ToString(path);
        var parsedValue = UTF8ToString(value);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {

            firebaseDatabase.ref(parsedPath).set(JSON.parse(parsedValue)).then(function(unused) {
                unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, "Success: " + parsedValue + " was posted to " + parsedPath);
            });

        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    PushJSON: function(path, value, objectName, callback, fallback) {
        var parsedPath = UTF8ToString(path);
        var parsedValue = UTF8ToString(value);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {

            firebaseDatabase.ref(parsedPath).push().set(JSON.parse(parsedValue)).then(function(unused) {
                unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, "Success: " + parsedValue + " was pushed to " + parsedPath);
            });

        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    UpdateJSON: function(path, value, objectName, callback, fallback) {
        var parsedPath = UTF8ToString(path);
        var parsedValue = UTF8ToString(value);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {

            firebaseDatabase.ref(parsedPath).update(JSON.parse(parsedValue)).then(function(unused) {
                unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, "Success: " + parsedValue + " was updated in " + parsedPath);
            });

        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    DeleteJSON: function(path, objectName, callback, fallback) {
        var parsedPath = UTF8ToString(path);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {

            firebaseDatabase.ref(parsedPath).remove().then(function(unused) {
                unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, "Success: " + parsedPath + " was deleted");
            });

        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    ListenForValueChanged: function(path, objectName, callback, fallback) {
        var parsedPath = UTF8ToString(path);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {

            firebaseDatabase.ref(parsedPath).on('value', function(snapshot) {
                unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, JSON.stringify(snapshot.val()));
            });

        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    StopListeningForValueChanged: function(path, parsedObjectName, callback, fallback) {
        var parsedPath = UTF8ToString(path);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {
            firebaseDatabase.ref(parsedPath).off('value');
            unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, "Success: listener removed");
        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    ListenForChildAdded: function(path, objectName, callback, fallback) {
        var parsedPath = UTF8ToString(path);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {

            firebaseDatabase.ref(parsedPath).on('child_added', function(snapshot) {
                unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, JSON.stringify(snapshot.val()));
            });

        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    StopListeningForChildAdded: function(path, parsedObjectName, callback, fallback) {
        var parsedPath = UTF8ToString(path);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {
            firebaseDatabase.ref(parsedPath).off('child_added');
            unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, "Success: listener removed");
        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    ListenForChildChanged: function(path, objectName, callback, fallback) {
        var parsedPath = UTF8ToString(path);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {

            firebaseDatabase.ref(parsedPath).on('child_changed', function(snapshot) {
                unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, JSON.stringify(snapshot.val()));
            });

        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    StopListeningForChildChanged: function(path, parsedObjectName, callback, fallback) {
        var parsedPath = UTF8ToString(path);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {
            firebaseDatabase.ref(parsedPath).off('child_changed');
            unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, "Success: listener removed");
        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    ListenForChildRemoved: function(path, objectName, callback, fallback) {
        var parsedPath = UTF8ToString(path);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {

            firebaseDatabase.ref(parsedPath).on('child_removed', function(snapshot) {
                unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, JSON.stringify(snapshot.val()));
            });

        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    StopListeningForChildRemoved: function(path, parsedObjectName, callback, fallback) {
        var parsedPath = UTF8ToString(path);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {
            firebaseDatabase.ref(parsedPath).off('child_removed');
            unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, "Success: listener removed");
        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    ModifyNumberWithTransaction: function(path, amount, objectName, callback, fallback) {
        var parsedPath = UTF8ToString(path);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {

            firebaseDatabase.ref(parsedPath).transaction(function(currentValue) {
                if (!isNaN(currentValue)) {
                    return currentValue + amount;
                } else {
                    return amount;
                }
            }).then(function(unused) {
                unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, "Success: transaction run in " + parsedPath);
            });

        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    ToggleBooleanWithTransaction: function(path, objectName, callback, fallback) {
        var parsedPath = UTF8ToString(path);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {

            firebaseDatabase.ref(parsedPath).transaction(function(currentValue) {
                if (typeof currentValue === "boolean") {
                    return !currentValue;
                } else {
                    return true;
                }
            }).then(function(unused) {
                unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, "Success: transaction run in " + parsedPath);
            });

        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    /**
     * Initializes the global analytics node once per day.
     * Ensures the structure exists and resets daily counters if the date changed.
     * Should be called when the player launches the game.
     */
    InitializeDailyAnalytics: function (objectName, callback, fallback) {
        const parsedObjectName = UTF8ToString(objectName);
        const parsedCallback = UTF8ToString(callback);
        const parsedFallback = UTF8ToString(fallback);

        if (typeof firebaseDatabase === "undefined" || firebaseDatabase === null) {
            console.error("firebaseDatabase reference not found in index.html");
            unityInstance.SendMessage(parsedObjectName, parsedFallback, "Database reference missing");
            return;
        }

        const analyticsRef = firebaseDatabase.ref("global_analytics");

        // Helper: get date string in YYYY-MM-DD format
        function getCurrentDateString() {
            const now = new Date();
            const year = now.getFullYear();
            const month = String(now.getMonth() + 1).padStart(2, "0");
            const day = String(now.getDate()).padStart(2, "0");
            return `${year}-${month}-${day}`;
        }

        analyticsRef.transaction(current => {
            const today = getCurrentDateString();

            // If structure doesn't exist, create it
            if (!current) {
                return {
                    gamesPlayedToday: 0,
                    usersPlayingNow: 0,
                    french: 0,
                    block: 0,
                    draw: 0,
                    five: 0,
                    concentrate: 0,
                    lastUpdatedDate: today
                };
            }

            const lastDate = current.lastUpdatedDate || today;

            // If date changed, reset daily counters
            if (today !== lastDate) {
                console.info(`Date changed from ${lastDate} → ${today}. Resetting daily counters.`);
                current.gamesPlayedToday = 0;
                current.usersPlayingNow = 0;

                current.french = 0;
                current.block = 0;
                current.draw = 0;
                current.five = 0;
                current.concentrate = 0;

                current.lastUpdatedDate = today;
            }

            return current;
        })
        .then(result => {
            if (!result.committed) {
                console.warn("Initialization transaction not committed");
                unityInstance.SendMessage(parsedObjectName, parsedFallback, "Transaction not committed");
                return;
            }

            const finalData = JSON.stringify(result.snapshot.val());
            unityInstance.SendMessage(parsedObjectName, parsedCallback, finalData);

        })
        .catch(error => {
            console.error("Initialization failed:", error);
            unityInstance.SendMessage(parsedObjectName, parsedFallback, error.message);
        });
    },

    /**
     * Performs multiple analytic updates in a single transaction.
     * Each operation is a tuple [isAdding, globalAnalytic, gameType].
     * Example input (JSON): [[true,1,0],[false,2,3]]
     */
    ModifyAnalyticsBatch: function (operationsJson, objectName, callback, fallback) {
        const parsedOperationsJson = UTF8ToString(operationsJson);
        const parsedObjectName = UTF8ToString(objectName);
        const parsedCallback = UTF8ToString(callback);
        const parsedFallback = UTF8ToString(fallback);
        
        console.info(`Parameters json: ${parsedOperationsJson}`);

        // Validate Firebase reference
        if (typeof firebaseDatabase === "undefined" || firebaseDatabase === null) {
            console.error("firebaseDatabase reference not found in index.html");
            unityInstance.SendMessage(parsedObjectName, parsedFallback, "Database reference missing");
            return;
        }
        
        let operations;
        try {
            operations = JSON.parse(parsedOperationsJson);
            if (!Array.isArray(operations)) 
                throw new Error("Parsed data is not an array");
        } 
        catch (error) {
            console.error("Invalid JSON format for operations:", error);
            unityInstance.SendMessage(parsedObjectName, parsedFallback, "Invalid operations format");
            return;
        }

        // Enum mapping - must match your C# enum order
        const globalAnalyticFieldNames = [
            "None",
            "gamesPlayedToday",
            "usersPlayingNow"
        ];

        const gameTypeFieldNames = [
            "none",
            "french",
            "block",
            "draw",
            "five",
            "concentrate"
        ];

        const analyticsRef = firebaseDatabase.ref("global_analytics");

        // Helper: get date string in YYYY-MM-DD format (local time)
        function getCurrentDateString() {
            const now = new Date();
            const year = now.getFullYear();
            const month = String(now.getMonth() + 1).padStart(2, "0");
            const day = String(now.getDate()).padStart(2, "0");
            return `${year}-${month}-${day}`;
        }

        // Execute a single atomic transaction
        analyticsRef.transaction(current => {
            // Ensure default structure
            if (!current) {
                current = {
                    gamesPlayedToday: 0,
                    usersPlayingNow: 0,
                    french: 0,
                    block: 0,
                    draw: 0,
                    five: 0,
                    concentrate: 0,
                    lastUpdatedDate: getCurrentDateString()
                };
            }

            const today = getCurrentDateString();
            const lastDate = current.lastUpdatedDate || today;

            // If date changed, reset daily counters
            if (today !== lastDate) {
                console.info(`Date changed from ${lastDate} to ${today}. Resetting daily counters.`);
                current.gamesPlayedToday = 0;
                current.usersPlayingNow = 0;
                current.french = 0;
                current.block = 0;
                current.draw = 0;
                current.five = 0;
                current.concentrate = 0;
                current.lastUpdatedDate = today;
            }

            // Apply each operation
            for (let i = 0; i < operations.length; i++) {
                const op = operations[i];

                if (!Array.isArray(op) || op.length !== 3) {
                    console.warn("Invalid operation tuple:", op);
                    continue;
                }
                
                const isAdding = op[0];
                const globalAnalytic = op[1];
                const gameType = op[2];
                
                if (typeof globalAnalytic !== "number") {
                    console.warn("Invalid globalAnalytic:", globalAnalytic);
                    continue;
                }

                const globalAnalyticField = globalAnalyticFieldNames[globalAnalytic];
                const gameField = gameTypeFieldNames[gameType];

                if (!globalAnalyticField || globalAnalyticField === "None") 
                {
                    console.warn(`Ommited due global analytic is null or invalid`);
                    continue;
                }

                // Update the main analytic field
                const currentValue = current[globalAnalyticField] || 0;
                current[globalAnalyticField] = Math.max(0, currentValue + (isAdding ? 1 : -1));

                console.info(`Global Analytic Field: ${globalAnalyticField} change from ${currentValue} to ${current[globalAnalyticField]}`);

                // If usersPlayingNow -> also modify the corresponding game type
                if (globalAnalyticField === "usersPlayingNow" && gameField && gameField !== "None") {
                    const gameValue = current[gameField] || 0;
                    current[gameField] = Math.max(0, gameValue + (isAdding ? 1 : -1));

                    console.info(`Game mode Field: ${gameField} change from ${gameValue} to ${current[gameField]}`);
                }
            }

            return current;
        })
        .then(result => {
            if (!result.committed) {
                console.warn("Transaction not committed");
                unityInstance.SendMessage(parsedObjectName, parsedFallback, "Transaction not committed");
                return;
            }

            // Send the final analytics object back to Unity (as JSON string)
            const finalData = JSON.stringify(result.snapshot.val());

            console.info(`New Updated data: ${finalData}`);
            unityInstance.SendMessage(parsedObjectName, parsedCallback, finalData);

        })
        .catch(error => {
            console.error("Transaction failed:", error);
            unityInstance.SendMessage(parsedObjectName, parsedFallback, error.message);
        });
    },

   SearchPlayersByName: function (searchPtr, objectNamePtr, callbackPtr, fallbackPtr) {

        const searchTerm = UTF8ToString(searchPtr);
        const objectName = UTF8ToString(objectNamePtr);
        const callback = UTF8ToString(callbackPtr);
        const fallback = UTF8ToString(fallbackPtr);

        try {
            if (typeof firebase === "undefined" || !firebaseDatabase) {
                unityInstance.SendMessage(objectName, fallback, "Firebase not initialized");
                return;
            }

            // Normalize exactly like backend
            let normalized = searchTerm
                .toLowerCase()
                .normalize("NFD")
                .replace(/[\u0300-\u036f]/g, "")
                .replace(/[^a-z0-9\s]/g, "")
                .trim();

            if (normalized.length === 0) {
                unityInstance.SendMessage(objectName, fallback, "Invalid search term");
                return;
            }

            const db = firebaseDatabase;
            const usersRef = db.ref("users");

            // RTDB-friendly search (always prefix search)
            const query = usersRef
                .orderByChild("normalizedName")
                .startAt(normalized)
                .endAt(normalized + "\uf8ff")
                .limitToFirst(20);

            query.once("value")
            .then(snap => {
                if (!snap.exists()) {
                    unityInstance.SendMessage(objectName, callback, "[]");
                    return;
                }

                const obj = snap.val();
                const arr = [];

                Object.keys(obj).forEach(id => {
                    const u = obj[id];
                    u.userId = id;
                    arr.push(u);
                });

                unityInstance.SendMessage(objectName, callback, JSON.stringify(arr));
            })
            .catch(err => {
                unityInstance.SendMessage(objectName, fallback, err.message || "Unknown RTDB error");
            });

        } catch (err) {
            unityInstance.SendMessage(objectName, fallback, err.toString());
        }
    },

    GetTopLeaderboards: function (
        leaderboardIdsJson, // JSON string array
        nationality,
        limit,
        objectName,
        callback,
        fallback
    ) {
        const parsedLeaderboardIdsJson = UTF8ToString(leaderboardIdsJson);
        const parsedNationality = UTF8ToString(nationality);
        const parsedObjectName = UTF8ToString(objectName);
        const parsedCallback = UTF8ToString(callback);
        const parsedFallback = UTF8ToString(fallback);

        try {
            // Parse leaderboard IDs array
            let leaderboardIds;

            try {
                leaderboardIds = JSON.parse(parsedLeaderboardIdsJson);
            } catch (e) {
                unityInstance.SendMessage(parsedObjectName, parsedFallback, "Invalid leaderboardIds JSON");
                return;
            }

            // Validate parameters
            if (typeof firebase === "undefined" || !firebaseDatabase) {
                unityInstance.SendMessage(parsedObjectName, parsedFallback, "Firebase not initialized");
                return;
            }

            if (
                !Array.isArray(leaderboardIds) ||
                leaderboardIds.length === 0 ||
                !parsedNationality ||
                isNaN(limit) ||
                limit <= 0
            ) {
                unityInstance.SendMessage(parsedObjectName, parsedFallback, "Invalid parameters");
                return;
            }

            const db = firebaseDatabase;

            // Create a promise per leaderboard
            const queries = leaderboardIds.map(id => {

                // Build reference for each leaderboard
                const leaderboardRef = db
                    .ref("leaderboards")
                    .child(id)
                    .child(parsedNationality);

                const query = leaderboardRef
                    .orderByChild("score")
                    .limitToLast(limit);

                return query.once("value")
                    .then(snapshot => {

                        if (!snapshot.exists()) {
                            return {
                                leaderboardId: id,
                                entries: []
                            };
                        }

                        const obj = snapshot.val();
                        const arr = [];

                        // Convert object to array
                        Object.keys(obj).forEach(uid => {
                            const entry = obj[uid];

                            arr.push({
                                userId: uid,
                                score: entry.score || 0,
                                username: entry.username || "",
                                updatedAt: entry.updatedAt || 0
                            });
                        });

                        // Sort descending
                        arr.sort((a, b) => b.score - a.score);

                        return {
                            leaderboardId: id,
                            entries: arr
                        };
                    });
            });

            // Wait for all leaderboards
            Promise.all(queries)
                .then(results => {

                    // results = array of { leaderboardId, entries }
                    unityInstance.SendMessage(
                        parsedObjectName,
                        parsedCallback,
                        JSON.stringify(results)
                    );
                })
                .catch(err => {
                    unityInstance.SendMessage(
                        parsedObjectName,
                        parsedFallback,
                        (err && err.message) || "RTDB leaderboard error"
                    );
                });

        } catch (err) {
            unityInstance.SendMessage(
                parsedObjectName,
                parsedFallback,
                (err && err.message) || "Unknown JS error"
            );
        }
    },


    GetOrCreateStoredSessionId: function () {
        // Ensure session ID exists
        let stored;

        if (typeof sessionStorage === "undefined") {
            // Fallback for browsers where sessionStorage is disabled
            if (!window._fallbackSessionId) {
                window._fallbackSessionId =
                    (crypto.randomUUID ? crypto.randomUUID() :
                    Math.random().toString(36).substring(2) + Date.now());
            }
            stored = window._fallbackSessionId;
        } 
        else {
            stored = sessionStorage.getItem("sessionId");
            if (!stored) {
                stored = crypto.randomUUID ?
                    crypto.randomUUID() :
                    (Math.random().toString(36).substring(2) + Date.now());
                sessionStorage.setItem("sessionId", stored);
            }
        }

        // Convert JS string → UTF8 C-string → pointer
        var length = lengthBytesUTF8(stored) + 1;
        var ptr = _malloc(length);
        stringToUTF8(stored, ptr, length);
        return ptr;
    },

    // JS (add inside mergeInto(LibraryManager.library, { ... }))
    RemoveStoredSessionId: function () {
        try {
            // Remove the per-tab session id from sessionStorage (best-effort)
            if (typeof sessionStorage !== "undefined") {
                sessionStorage.removeItem("sessionId");
                console.log("Session removed!")
            }
        } catch (e) {
            // Swallow errors, this is best-effort cleanup
            console.error("RemoveStoredSessionId failed:", e && e.toString());
        }
    },

    CheckIfSessionActive: function (userId, sessionId, objectName, callback, fallback) {

        // Convert Unity strings to JS strings
        const userIdParsed = UTF8ToString(userId);
        const sessionIdParsed = UTF8ToString(sessionId);
        const objectNameParsed = UTF8ToString(objectName);
        const callbackParsed = UTF8ToString(callback);
        const fallbackParsed = UTF8ToString(fallback);

        try {

            // Basic validation
            if (!window.firebaseDatabase) {
                unityInstance.SendMessage(objectNameParsed, fallbackParsed, "Firebase not initialized");
                return;
            }

            const db = window.firebaseDatabase;
            const ref = db.ref("users").child(userIdParsed);

            ref.once("value")
                .then(snap => {

                    if (!snap.exists()) {
                        // No session -> OK
                        unityInstance.SendMessage(objectNameParsed, callbackParsed, "ALLOW");
                        return;
                    }

                    const data = snap.val() || {};
                    const existingId = data.sessionId || "";
                    const lastBeat = data.lastHeartbeat || 0;
                    const status = data.status || 0;

                    const now = Math.floor(Date.now() / 1000);
                    const TIMEOUT = 60;

                    // Check if session is expired
                    const expired = (now - lastBeat) > TIMEOUT;

                    if (expired) {
                        unityInstance.SendMessage(objectNameParsed, callbackParsed, "ALLOW");
                        return;
                    }

                    // Different active session, DENY
                    if (existingId !== "" && existingId !== sessionIdParsed && status === 1) {
                        console.log("[RTDB] Different active session, deny access")

                        unityInstance.SendMessage(objectNameParsed, callbackParsed, "DENY");
                        return;
                    }

                    // Same session, allow reconnect
                    console.log("[RTDB] Same session, allow reconnect")
                    unityInstance.SendMessage(objectNameParsed, callbackParsed, "ALLOW");

                })
                .catch(err => {
                    unityInstance.SendMessage(objectNameParsed, fallbackParsed, err.message || "RTDB error");
                });

        } catch (err) {
            unityInstance.SendMessage(objectNameParsed, fallbackParsed, err.toString());
        }
    },

    UpdateSessionHeartbeat: function (userId, sessionId, status, objectName, callback, fallback) {

        // Convert Unity strings
        const userIdParsed = UTF8ToString(userId);
        const sessionIdParsed = UTF8ToString(sessionId);
        const objectNameParsed = UTF8ToString(objectName);
        const callbackParsed = UTF8ToString(callback);
        const fallbackParsed = UTF8ToString(fallback);

        if (!userIdParsed || userIdParsed.trim() === "") {
            unityInstance.SendMessage(objectNameParsed, fallbackParsed, "Invalid userId");
            return;
        }

        try {

            if (!window.firebaseDatabase) {
                unityInstance.SendMessage(objectNameParsed, fallbackParsed, "Firebase not initialized");
                return;
            }

            const now = Math.floor(Date.now() / 1000);
            const db = window.firebaseDatabase;

            const ref = db.ref("users").child(userIdParsed);

            const data = {
                sessionId: sessionIdParsed,
                lastHeartbeat: now,
                status: status
            };

            ref.update(data)
                .then(() => {
                    unityInstance.SendMessage(objectNameParsed, callbackParsed, "OK");
                })
                .catch(err => {
                    unityInstance.SendMessage(objectNameParsed, fallbackParsed, err.message || "RTDB error");
                });

        } catch (err) {
            unityInstance.SendMessage(objectNameParsed, fallbackParsed, err.toString());
        }
    }
});