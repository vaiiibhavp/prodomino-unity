mergeInto(LibraryManager.library, {

    CreateUserWithEmailAndPassword: function (email, password, objectName, callback, fallback) {
        var parsedEmail = UTF8ToString(email);
        var parsedPassword = UTF8ToString(password);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {

            firebaseAuth.createUserWithEmailAndPassword(parsedEmail, parsedPassword).then(function (unused) {
                unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, "Success: signed up for " + parsedEmail);
            }).catch(function (error) {
                unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
            });

        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    SignInWithEmailAndPassword: function (email, password, objectName, callback, fallback) {
        var parsedEmail = UTF8ToString(email);
        var parsedPassword = UTF8ToString(password);
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {

            firebaseAuth.signInWithEmailAndPassword(parsedEmail, parsedPassword).then(function (unused) {
                unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, "Success: signed in for " + parsedEmail);
            }).catch(function (error) {
                unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
            });

        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

   SignInWithGoogle: function (objectName, callback, fallback) {
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {
            // --------------------------------------------------
            // Create Google Auth provider
            // --------------------------------------------------
            var provider = window.createGoogleProvider();

            // Email scope is optional, Google may still not return it immediately
            provider.addScope("email");

            // Force account chooser even if a session exists
            provider.setCustomParameters({ prompt: "select_account" });

            console.log("Google provider created:", provider);

            // --------------------------------------------------
            // Start Google Sign-In popup
            // --------------------------------------------------
            window.firebaseAuth.signInWithPopup(provider)
                .then(function (result) {
                    console.log("Google sign-in result:", result);

                    // --------------------------------------------------
                    // IMPORTANT:
                    // - Email may be null even for valid Google accounts
                    // - Do NOT use email to validate Google login
                    // --------------------------------------------------

                    var providerId = null;

                    // Detect which provider authenticated the user
                    if (result.user &&
                        result.user.providerData &&
                        result.user.providerData.length > 0) {
                        providerId = result.user.providerData[0].providerId;
                    }

                    // --------------------------------------------------
                    // If provider is Google, trust it
                    // --------------------------------------------------
                    if (providerId === "google.com") {
                        return handleSuccessfulGoogleLogin(result);
                    }

                    // --------------------------------------------------
                    // Any other provider is unexpected here
                    // --------------------------------------------------
                    console.warn("Unexpected provider after Google sign-in:", providerId);

                    return window.firebaseAuth.signOut().then(function () {
                        unityInstance.Module.SendMessage(
                            parsedObjectName,
                            parsedFallback,
                            JSON.stringify({
                                error: "UNEXPECTED_PROVIDER",
                                message: "Authenticated with an unexpected provider.",
                                providerUsed: providerId
                            })
                        );
                    });
                })
                .catch(function (error) {
                    console.error("Google authentication error:", error);

                    // --------------------------------------------------
                    // Firebase specific error: account exists with another provider
                    // --------------------------------------------------
                    if (error.code === "auth/account-exists-with-different-credential") {
                        unityInstance.Module.SendMessage(
                            parsedObjectName,
                            parsedFallback,
                            JSON.stringify({
                                error: "ACCOUNT_EXISTS_WITH_DIFFERENT_CREDENTIAL",
                                message: "Email already registered with a different provider.",
                                email: (error &&
                                        error.customData &&
                                        error.customData.email)
                                    ? error.customData.email
                                    : null
                            })
                        );
                        return;
                    }

                    // --------------------------------------------------
                    // Generic error handling
                    // --------------------------------------------------
                    if (window.firebaseAuth.currentUser) {
                        window.firebaseAuth.signOut();
                    }

                    unityInstance.Module.SendMessage(
                        parsedObjectName,
                        parsedFallback,
                        JSON.stringify(error, Object.getOwnPropertyNames(error))
                    );
                });

            // --------------------------------------------------
            // Shared success handler
            // --------------------------------------------------
            function handleSuccessfulGoogleLogin(result) {

                // Extract Google credential
                var credential = result.credential;
                if (!credential) {
                    throw new Error("Google did not return a credential.");
                }

                var idToken = credential.idToken;
                if (!idToken) {
                    throw new Error("Google did not return an ID token.");
                }

                // --------------------------------------------------
                // Validate token issuer (UGS requirement)
                // --------------------------------------------------
                return fetch("https://oauth2.googleapis.com/tokeninfo?id_token=" + idToken)
                    .then(function (response) {
                        return response.json();
                    })
                    .then(function (data) {

                        if (!data.iss || data.iss !== "accounts.google.com") {
                            throw new Error("Invalid Google token issuer for UGS.");
                        }

                        // --------------------------------------------------
                        // Prepare payload for Unity
                        // Email is intentionally NOT required
                        // --------------------------------------------------
                        var googleAuthDataCollection = {
                            idToken: idToken,
                            tokenType: "id_token",
                            displayName: result.user.displayName || null,
                            photoURL: result.user.photoURL || null,
                            expToken: data.exp * 1000
                        };

                        unityInstance.Module.SendMessage(
                            parsedObjectName,
                            parsedCallback,
                            JSON.stringify(googleAuthDataCollection)
                        );
                    });
            }

        } catch (error) {
            console.error("Unexpected Google auth error:", error);

            if (window.firebaseAuth.currentUser) {
                window.firebaseAuth.signOut();
            }

            unityInstance.Module.SendMessage(
                parsedObjectName,
                parsedFallback,
                JSON.stringify(error, Object.getOwnPropertyNames(error))
            );
        }
    },

    SignInWithFacebook: function (objectName, callback, fallback) {
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);

        try {
            var provider = new firebaseAuth.FacebookAuthProvider();
            firebaseAuth.signInWithRedirect(provider).then(function (unused) {
                unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, "Success: signed in with Facebook!");
            }).catch(function (error) {
                // If the user has a signing error (closed popUp abruptly for example) and is already signed,force the signOut
                if (firebaseAuth.currentUser)
                    firebaseAuth.signOut();
                
                unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
            });

        } catch (error) {
            // If the user has a signing error (closed popUp abruptly for example) and is already signed,force the signOut
            if (firebaseAuth.currentUser)
                firebaseAuth.signOut();

            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    FirebaseSignInWithRefreshToken: async function (refreshToken, objectName, callback, fallback) {
        const refreshTokenParsed = UTF8ToString(refreshToken);
        const objParsed = UTF8ToString(objectName);
        const callbackParsed = UTF8ToString(callback);
        const fallbackParsed = UTF8ToString(fallback);

        try {
            const body = new URLSearchParams();
            body.append("grant_type", "refresh_token");
            body.append("refresh_token", refreshTokenParsed);

            const response = await fetch(
                `https://securetoken.googleapis.com/v1/token?key=${window.apiKey}`,
                {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/x-www-form-urlencoded"
                    },
                    body: body.toString()
                }
            );

            const data = await response.json();

            if (!response.ok) {
                throw data;
            }

            /*
                data.id_token        -> NEW Firebase ID Token
                data.refresh_token   -> NEW Refresh Token
                data.expires_in      -> seconds (string)
                data.user_id         -> UID
            */
            unityInstance.Module.SendMessage(
                objParsed,
                callbackParsed,
                JSON.stringify(data)
            );
        }
        catch (err) {
            unityInstance.Module.SendMessage(
                objParsed,
                fallbackParsed,
                JSON.stringify(err)
            );
        }
    },


    FirebaseSignInWithIdp: async function (token, tokenType, providerId, objectName, callback, fallback) {
        const tokenParsed = UTF8ToString(token);
        const tokenTypeParsed = UTF8ToString(tokenType);
        const providerParsed = UTF8ToString(providerId);
        const objParsed = UTF8ToString(objectName);
        const callbackParsed = UTF8ToString(callback);
        const fallbackParsed = UTF8ToString(fallback);

        try {
            const response = await fetch(
            `https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key=${window.apiKey}`,
                {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json"
                    },
                    body: JSON.stringify({
                        postBody: `${tokenTypeParsed}=${tokenParsed}&providerId=${providerParsed}.com`,
                        requestUri: "https://playprodomino.com",
                        returnIdpCredential: true,
                        returnSecureToken: true
                    })
                }
            );

            const data = await response.json();

            if (!response.ok) {
                throw data;
            }

            // data.idToken === Firebase ID Token
            // data.refreshToken === Firebase Refresh Token
            unityInstance.Module.SendMessage(objParsed, callbackParsed, JSON.stringify(data));
        }
        catch (err) {
            unityInstance.Module.SendMessage(objParsed, fallbackParsed, JSON.stringify(err));
        }
    },


    SignOut: function (objectName, callback, fallback) {
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);
    
        try {
            firebaseAuth.signOut().then(function () {
                unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, "Success: signed out");
            }).catch(function (error) {
                unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
            });
        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    IsUserAuthenticated: function () {
        return firebaseAuth.currentUser ? 1 : 0; // Retorna 1 (true) o 0 (false)
    },

    CheckEmailVerification: function (objectName, callback, fallback) {
        var parsedObjectName = UTF8ToString(objectName);
        var parsedCallback = UTF8ToString(callback);
        var parsedFallback = UTF8ToString(fallback);
    
        try {
            var user = firebaseAuth.currentUser;
    
            if (user) {
                user.reload().then(function () {
                    if (user.emailVerified) {
                        unityInstance.Module.SendMessage(parsedObjectName, parsedCallback, "Email verified: " + user.email);
                    } else {
                        unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, "Email not verified.");
                    }
                }).catch(function (error) {
                    unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
                });
            } else {
                unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, "No user signed in.");
            }
        } catch (error) {
            unityInstance.Module.SendMessage(parsedObjectName, parsedFallback, JSON.stringify(error, Object.getOwnPropertyNames(error)));
        }
    },

    IsEmailVerified: function () {
        var user = firebaseAuth.currentUser;
        
        if (user) {
            return user.emailVerified ? 1 : 0; // Return 1 if the email is verified, 0 if not
        }
    
        return -1; // Return -1 if the user is not authenticated
    },  

    GetTokenExpiration: function (accessToken) {
        try {
            const base64Url = accessToken.split('.')[1]; // Extrae el Payload
            const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
            const payload = JSON.parse(atob(base64));
    
            if (payload.exp) {
                const expirationDate = new Date(payload.exp * 1000); // Convierte UNIX timestamp a fecha
                return expirationDate.toISOString(); // Formato legible
            }
    
            return "Expiración no encontrada";
        } catch (error) {
            console.error("Error al extraer expirationTime:", error);
            return null;
        }
    },

   GetUserProfile: function (idToken, objectName, callback, fallback) {

        // Convert Unity strings to JS strings
        const googleIdToken = UTF8ToString(idToken);
        const objectNameParsed = UTF8ToString(objectName);
        const callbackParsed = UTF8ToString(callback);
        const fallbackParsed = UTF8ToString(fallback);

        try {

            // Step 1: Sign in silently to Firebase using Google ID Token
            const credential = firebase.auth.GoogleAuthProvider.credential(googleIdToken);

            window.firebaseAuth.signInWithCredential(credential)
                .then(userCredential => {

                    const user = userCredential.user;
                    if (!user) {
                        throw new Error("Firebase user is null.");
                    }

                    // Step 2: Extract Firebase profile data
                    const displayName = user.displayName || "";
                    const photoURL = user.photoURL || "";

                    // Step 3: Decode exp from Google ID Token (no HTTP call)
                    const payloadBase64 = googleIdToken.split(".")[1];
                    const payloadJson = atob(payloadBase64);
                    const payload = JSON.parse(payloadJson);

                    const expToken = payload.exp ? payload.exp * 1000 : 0;

                    // Step 4: Build payload identical to SignInWithGoogle
                    const profile = {
                        idToken: googleIdToken,
                        tokenType: "id_token",
                        displayName: displayName,
                        photoURL: photoURL,
                        expToken: expToken
                    };

                    // Send data back to Unity
                    unityInstance.Module.SendMessage(
                        objectNameParsed,
                        callbackParsed,
                        JSON.stringify(profile)
                    );
                })
                .catch(error => {
                    throw error;
                });

        } catch (error) {

            // Error handling
            unityInstance.Module.SendMessage(
                objectNameParsed,
                fallbackParsed,
                JSON.stringify({
                    error: error.message,
                    code: error.code || "UNKNOWN"
                })
            );
        }
    },



    OnAuthStateChanged: function (objectName, onUserSignedIn, onUserSignedOut) {
        var parsedObjectName = UTF8ToString(objectName);
        var parsedOnUserSignedIn = UTF8ToString(onUserSignedIn);
        var parsedOnUserSignedOut = UTF8ToString(onUserSignedOut);

        firebaseAuth.onAuthStateChanged(function(user) {
            if (user) {
                unityInstance.Module.SendMessage(parsedObjectName, parsedOnUserSignedIn, JSON.stringify(user));
            } else {
                unityInstance.Module.SendMessage(parsedObjectName, parsedOnUserSignedOut, "User signed out");
            }
        });

    }
});
