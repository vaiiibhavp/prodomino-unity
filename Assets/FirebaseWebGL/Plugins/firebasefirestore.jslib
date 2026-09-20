mergeInto(LibraryManager.library, {

    GetDocument: function (collectionPath, documentId, objectName, callback, fallback) {
        var parsedPath = UTF8ToString(collectionPath);
        var parsedId = UTF8ToString(documentId);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {
            firebaseFirestore.collection(parsedPath).doc(parsedId).get().then(function (doc) {

                if (doc.exists) {
                    unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, JSON.stringify(doc.data()));
                } else {
                    unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, "null");
                }
            }).catch(function(error) {
                unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
            });

        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    GetDocumentsInCollection: function (collectionPath, objectName, callback, fallback) {
        var parsedPath = UTF8ToString(collectionPath);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {
            firebaseFirestore.collection(parsedPath).get().then(function (querySnapshot) {

                var docs = {};
                querySnapshot.forEach(function(doc) {
                    docs[doc.id] = doc.data();
                });

                unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, JSON.stringify(docs));
            }).catch(function(error) {
                unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
            });

        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    SetDocument: function (collectionPath, documentId, value, objectName, callback, fallback) {
        var parsedPath = UTF8ToString(collectionPath);
        var parsedId = UTF8ToString(documentId);
        var parsedValue = UTF8ToString(value);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {

            firebaseFirestore.collection(parsedPath).doc(parsedId).set(JSON.parse(parsedValue)).then(function() {
                unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, "Success: document " + parsedId + " was set");
            })
                .catch(function(error) {
                    unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
                });

        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    AddDocument: function (collectionPath, value, objectName, callback, fallback) {
        var parsedPath = UTF8ToString(collectionPath);
        var parsedValue = UTF8ToString(value);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {

            firebaseFirestore.collection(parsedPath).add(JSON.parse(parsedValue)).then(function(unused) {
                unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, "Success: document added in collection " + parsedPath);
            })
                .catch(function(error) {
                    unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
                });

        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    UpdateDocument: function (collectionPath, documentId, value, objectName, callback, fallback) {
        var parsedPath = UTF8ToString(collectionPath);
        var parsedId = UTF8ToString(documentId);
        var parsedValue = UTF8ToString(value);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {

            firebaseFirestore.collection(parsedPath).doc(parsedId).update(JSON.parse(parsedValue)).then(function() {
                unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, "Success: document " + parsedId + " was updated");
            })
                .catch(function(error) {
                    unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
                });

        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    DeleteDocument: function (collectionPath, documentId, objectName, callback, fallback) {
        var parsedPath = UTF8ToString(collectionPath);
        var parsedId = UTF8ToString(documentId);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {

            firebaseFirestore.collection(parsedPath).doc(parsedId).delete().then(function() {
                unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, "Success: document " + parsedId + " was deleted");
            })
                .catch(function(error) {
                    unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
                });

        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    DeleteField: function (collectionPath, documentId, field, objectName, callback, fallback) {
        var parsedPath = UTF8ToString(collectionPath);
        var parsedId = UTF8ToString(documentId);
        var parsedField = UTF8ToString(field);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {

            var value = {};
            value[parsedField] = firebaseApp.firestore.FieldValue.delete();

            firebaseFirestore.collection(parsedPath).doc(parsedId).update(value).then(function() {
                unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, "Success: field " + parsedField + " was deleted");
            })
                .catch(function(error) {
                    unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
                });

        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    AddElementInArrayField: function (collectionPath, documentId, field, value, objectName, callback, fallback) {
        var parsedPath = UTF8ToString(collectionPath);
        var parsedId = UTF8ToString(documentId);
        var parsedField = UTF8ToString(field);
        var parsedValue = UTF8ToString(value);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {

            var value = {};
            value[parsedField] = firebaseApp.firestore.FieldValue.arrayUnion(JSON.parse(parsedValue));

            firebaseFirestore.collection(parsedPath).doc(parsedId).update(value).then(function() {
                unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, "Success: element " + parsedValue + " was added in " + parsedField);
            })
                .catch(function(error) {
                    unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
                });

        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    RemoveElementInArrayField: function (collectionPath, documentId, field, value, objectName, callback, fallback) {
        var parsedPath = UTF8ToString(collectionPath);
        var parsedId = UTF8ToString(documentId);
        var parsedField = UTF8ToString(field);
        var parsedValue = UTF8ToString(value);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {

            var value = {};
            value[parsedField] = firebaseApp.firestore.FieldValue.arrayRemove(JSON.parse(parsedValue));

            firebaseFirestore.collection(parsedPath).doc(parsedId).update(value).then(function() {
                unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, "Success: element " + parsedValue + " was removed in " + parsedField);
            })
                .catch(function(error) {
                    unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
                });

        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    IncrementFieldValue: function (collectionPath, documentId, field, increment, objectName, callback, fallback) {
        var parsedPath = UTF8ToString(collectionPath);
        var parsedId = UTF8ToString(documentId);
        var parsedField = UTF8ToString(field);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {

            var value = {};
            value[parsedField] = firebaseApp.firestore.FieldValue.increment(increment);

            firebaseFirestore.collection(parsedPath).doc(parsedId).update(value).then(function() {
                unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, "Success: incremented " + parsedField + " by " + increment);
            })
                .catch(function(error) {
                    unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
                });

        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    ListenForDocumentChange: function (collectionPath, documentId, includeMetadataChanges, objectName, callback, fallback) {
        var parsedPath = UTF8ToString(collectionPath);
        var parsedId = UTF8ToString(documentId);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {

            if (typeof firestorelisteners === 'undefined') firestorelisteners = {};

            this.firestorelisteners[parsedPath + "/" + parsedId] = firebaseFirestore.collection(parsedPath).doc(parsedId)
                .onSnapshot({
                    includeMetadataChanges: (includeMetadataChanges == 1)
                }, function(doc) {
                    unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, JSON.stringify(doc.data()));
                }, function(error) {
                    unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
                });

        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    StopListeningForDocumentChange: function (collectionPath, documentId, objectName, callback, fallback) {
        var parsedPath = UTF8ToString(collectionPath);
        var parsedId = UTF8ToString(documentId);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {

            if (typeof firestorelisteners === 'undefined') firestorelisteners = {};

            this.firestorelisteners[parsedPath + "/" + parsedId]();
            unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, "Success: listener was removed");
        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    ListenForCollectionChange: function (collectionPath, includeMetadataChanges, objectName, callback, fallback) {
        var parsedPath = UTF8ToString(collectionPath);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {

            if (typeof firestorelisteners === 'undefined') firestorelisteners = {};

            this.firestorelisteners[parsedPath + "/collection/"] = firebaseFirestore.collection(parsedPath)
                .onSnapshot({
                    includeMetadataChanges: (includeMetadataChanges == 1)
                }, function(querySnapshot) {

                    var docs = {};
                    querySnapshot.forEach(function(doc) {
                        docs[doc.id] = doc.data();
                    });

                    unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, JSON.stringify(docs));

                }, function(error) {
                    unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
                });

        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    StopListeningForCollectionChange: function (collectionPath, objectName, callback, fallback) {
        var parsedPath = UTF8ToString(collectionPath);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {

            if (typeof firestorelisteners === 'undefined') firestorelisteners = {};

            this.firestorelisteners[parsedPath + "/collection/"]();
            unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, "Success: listener was removed");
        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    GetClubRank: function (collectionPath, documentId, objectName, callback, fallback) {
        var parsedPath = UTF8ToString(collectionPath);
        var parsedId = UTF8ToString(documentId);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {
            firebaseFirestore
                .collection(parsedPath)
                .orderBy("score", "desc") // 🔽 Orden descendente
                .get()
                .then(function (querySnapshot) {

                    if (querySnapshot.empty) {
                        unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, "No clubs found");
                        return;
                    }

                    var position = 1;
                    var found = false;

                    querySnapshot.forEach(function (doc) {
                        if (doc.id === parsedId) {
                            // ✅ Found the club
                            found = true;
                            unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, position.toString());
                        }
                        position++;
                    });

                    if (!found) {
                        unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, "Club not found");
                    }
                })
                .catch(function (error) {
                    unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
                });

        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    SearchClubsByName: function (collectionPath, searchTerm, objectName, callback, fallback) {
        var parsedPath = UTF8ToString(collectionPath);
        var parsedTerm = UTF8ToString(searchTerm);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {
            // Normalize the search term (same logic as backend)
            var normalized = parsedTerm
                .toLowerCase()
                .normalize("NFD")                      // Decompose accented characters
                .replace(/[\u0300-\u036f]/g, "")       // Remove diacritics
                .replace(/[^a-z0-9\s]/g, "")           // Remove special characters
                .trim();

            if (normalized.length === 0) {
                unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, "Invalid search term");
                return;
            }

            // Determine if it's a multi-word search (contains spaces)
            var isMultiWord = normalized.includes(" ");

            var query;
            if (isMultiWord) {
                // --- Case 1: Multi-word search using normalizedName prefix ---
                var endRange = normalized + "\uf8ff";

                query = firebaseFirestore
                    .collection(parsedPath)
                    .orderBy("normalizedName")
                    .startAt(normalized)
                    .endAt(endRange)
                    .limit(10);
                console.log(`[Firestore] Multi-word search for '${normalized}' using normalizedName prefix`);
            } else {
                // --- Case 2: Single-word search using searchTokens array ---
                query = firebaseFirestore
                    .collection(parsedPath)
                    .where("searchTokens", "array-contains", normalized)
                    .limit(10);
                console.log(`[Firestore] Single-word search for '${normalized}' using searchTokens array`);
            }

            // Execute query
            query.get()
                .then(function (querySnapshot) {
                    if (querySnapshot.empty) {
                        unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, "No matches found");
                        return;
                    }

                    var clubs = [];
                    querySnapshot.forEach(function (doc) {
                        var data = doc.data();
                        data.id = doc.id;
                        clubs.push(data);
                    });

                    // Send results back to Unity as JSON
                    unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, JSON.stringify(clubs));
                })
                .catch(function (error) {
                    unityInstance.Module.SendMessage(
                        parsedObjectName,
                        parsedFallback,
                        JSON.stringify(error, Object.getOwnPropertyNames(error))
                    );
                });

        } catch (error) {
            unityInstance.Module.SendMessage(
                parsedObjectName,
                parsedFallback,
                JSON.stringify(error, Object.getOwnPropertyNames(error))
            );
        }
    },

    GetTopClubsByScore: function (collectionPath, objectName, callback, fallback) {
        var parsedPath = UTF8ToString(collectionPath);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {
            // Perform Firestore query to get the top 10 clubs ordered by score (descending)
            firebaseFirestore
                .collection(parsedPath)
                .orderBy("score", "desc")
                .limit(10)
                .get()
                .then(function (querySnapshot) {
                    if (querySnapshot.empty) {
                        unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, "No clubs found");
                        return;
                    }

                    var clubs = [];
                    querySnapshot.forEach(function (doc) {
                        var data = doc.data();
                        data.id = doc.id;
                        clubs.push(data);
                    });

                    // Send the top 10 clubs as JSON to Unity
                    unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, JSON.stringify(clubs));
                })
                .catch(function (error) {
                    unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
                });

        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    // Subscribe to a club chat document in Firestore
    // - collectionPath: e.g. "clubChats"
    // - clubId: e.g. "myClub123"
    // - objectName: Unity GameObject that receives callbacks
    // - onSubscribeSuccess / onSubscribeError: fire once when listener starts/fails
    // - onMessagesUpdated / onMessagesError: fire every time snapshot updates or fails
    SubscribeToClubChat: function (
        collectionPath,
        clubId,
        objectName,
        onSubscribeSuccess,
        onSubscribeError,
        onMessagesUpdated,
        onMessagesError
    ) {
        var parsedCollectionPath = UTF8ToString(collectionPath);
        var parsedClubId = UTF8ToString(clubId);
        var parsedObjectName = UTF8ToString(objectName);
        var cbSubscribeSuccess = UTF8ToString(onSubscribeSuccess);
        var cbSubscribeError = UTF8ToString(onSubscribeError);
        var cbMessagesUpdated = UTF8ToString(onMessagesUpdated);
        var cbMessagesError = UTF8ToString(onMessagesError);

        try {
            if (typeof firestorelisteners === 'undefined')
                firestorelisteners = {};

            // Compose full doc path
            var docPath = parsedCollectionPath + "/" + parsedClubId;
            var listenerKey = docPath + "/listener/";

            // Clean previous listener if exists
            if (this.firestorelisteners[listenerKey] && typeof this.firestorelisteners[listenerKey] === "function") {
                try {
                    this.firestorelisteners[listenerKey]();
                } catch (e) {
                    console.warn("Error while cleaning previous listener for", listenerKey, e);
                }
                delete this.firestorelisteners[listenerKey];
            }

            // Reference the Firestore document
            var docRef = firebaseFirestore.collection(parsedCollectionPath).doc(parsedClubId);

            // Subscribe to document changes
            var unsubscribe = docRef.onSnapshot(
                async function (snapshot) {
                    try {
                        if (!snapshot.exists) {
                            unityInstance.Module.SendMessage(parsedObjectName, cbMessagesUpdated, "");
                            return;
                        }


                        var data = snapshot.data();
                        var messages = data && data.messages ? data.messages : [];

                        // Archive overflow messages if needed
                        var MAX_MESSAGES = 100;
                        if (messages.length > MAX_MESSAGES) {
                            var overflowCount = messages.length - MAX_MESSAGES;
                            var overflow = messages.slice(0, overflowCount);
                            var recent = messages.slice(overflowCount);

                            var batch = firebaseFirestore.batch();
                            var historyRef = docRef.collection("history");

                            overflow.forEach(function (msg) {
                                var ts = msg.timestamp || Date.now();
                                var uniqueId = "msg_" + ts + "_" + Math.floor(Math.random() * 1000000);
                                batch.set(historyRef.doc(uniqueId), msg);
                            });

                            batch.update(docRef, { messages: recent });

                            try {
                                await batch.commit();
                                messages = recent;
                                
                                // Update the data messages
                                data.messages = messages;
                            } 
                            catch (err) {
                                console.error("Error archiving messages for", docPath, err);
                            }
                        }

                        // Send updated messages to Unity
                        unityInstance.Module.SendMessage(parsedObjectName, cbMessagesUpdated, JSON.stringify(data));
                    } catch (innerErr) {
                        unityInstance.Module.SendMessage(parsedObjectName, cbMessagesError, JSON.stringify(innerErr, Object.getOwnPropertyNames(innerErr)));
                    }
                },
                function (error) {
                    unityInstance.Module.SendMessage(parsedObjectName, cbMessagesError, JSON.stringify(error, Object.getOwnPropertyNames(error)));
                }
            );

            // Store unsubscribe handle
            this.firestorelisteners[listenerKey] = unsubscribe;

            // Notify Unity subscription success
            unityInstance.Module.SendMessage(
                parsedObjectName,
                cbSubscribeSuccess,
                JSON.stringify({ success: true, message: "Subscribed", path: docPath })
            );

        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, cbSubscribeError, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },


    /*
    * Unsubscribe from a previously created SubscribeToClubChat.
    * The collectionPath argument must match exactly the collectionPath used on SubscribeToClubChat.
    */
    UnsubscribeFromClubChat: function (
        collectionPath,
        clubId,
        objectName,
        onUnsubscribeSuccess,
        onUnsubscribeError
    ) {
        var parsedCollectionPath = UTF8ToString(collectionPath);
        var parsedClubId = UTF8ToString(clubId);
        var parsedObjectName = UTF8ToString(objectName);
        var cbSuccess = UTF8ToString(onUnsubscribeSuccess);
        var cbError = UTF8ToString(onUnsubscribeError);

        try {
            if (typeof firestorelisteners === 'undefined')
                firestorelisteners = {};

            var docPath = parsedCollectionPath + "/" + parsedClubId;
            var listenerKey = docPath + "/listener/";

            if (this.firestorelisteners[listenerKey]) {
                this.firestorelisteners[listenerKey](); // Unsubscribe
                delete this.firestorelisteners[listenerKey];
                unityInstance.Module.SendMessage(parsedObjectName, cbSuccess, JSON.stringify({ success: true, message: "Unsubscribed", path: docPath }));
            } else {
                unityInstance.Module.SendMessage(parsedObjectName, cbError, JSON.stringify({ success: false, message: "No active listener for " + docPath }));
            }
        } 
        catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, error.message || "Unknown Firestore error");
        }
    },

    AddClubMessage: function (collectionPathPtr, clubNamePtr, senderIdPtr, senderNamePtr, profileIconIdPtr, messageContentPtr, objectNamePtr, callbackPtr, fallbackPtr) {
        var parsedCollection = UTF8ToString(collectionPathPtr);
        var parsedClubName = UTF8ToString(clubNamePtr);
        var parsedSenderId = UTF8ToString(senderIdPtr);
        var parsedSenderName = UTF8ToString(senderNamePtr);
        var parsedProfileIconId = UTF8ToString(profileIconIdPtr);
        var parsedMessage = UTF8ToString(messageContentPtr);
        var parsedObjectName = UTF8ToString(objectNamePtr);
        var parsedCallback = UTF8ToString(callbackPtr);
        var parsedFallback = UTF8ToString(fallbackPtr);

        try {
            var docRef = firebaseFirestore.collection(parsedCollection).doc(parsedClubName);
            var timestamp = firebase.firestore.Timestamp.now();

            docRef.get()
                .then(function (docSnapshot) {
                    var count = 0;
                    var messages = [];
                    if (docSnapshot.exists) {
                        var data = docSnapshot.data() || {};
                        count = data.historyCount || 0;
                        messages = data.messages || [];
                    }

                    // Generate ID based on count
                    var newCount = count + 1;
                    var padded = String(newCount).padStart(6, "0");
                    var messageId = "msg_" + padded;

                    var newMessage = {
                        messageId: messageId,
                        senderId: parsedSenderId,
                        senderName: parsedSenderName,
                        profileIconId: parsedProfileIconId,
                        content: parsedMessage,
                        timestamp: timestamp
                    };

                    messages.push(newMessage);
                    if (messages.length > 100) 
                        messages = messages.slice(messages.length - 100);

                    var payload = {
                        clubName: parsedClubName,
                        lastMessage: parsedMessage,
                        updatedAt: timestamp,
                        historyCount: newCount,
                        messages: messages
                    };

                    var operation = docSnapshot.exists ? docRef.update(payload) : docRef.set(payload);
                    operation.then(function () {
                        unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, "Message saved successfully.");
                    }).catch(function (error) {
                        unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
                    });
                })
                .catch(function (error) {
                    unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
                });
        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },
});