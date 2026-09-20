using HelperSharedLibrary;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace Backend.AuthenticationSystem
{
    /// <summary>
    /// Provides helper methods for authenticating users with Firebase, updating user profiles, ensuring user existence
    /// in the Firebase Realtime Database, and managing related user data such as profile icons, account creation dates,
    /// and analytics streaks.
    /// </summary>
    internal class AuthenticationSystem_Helper
    {
        #region Firebase Helpers
        ///// <summary>
        ///// Ensures the Firebase user profile has a display name by updating it with the UGS username if it is missing.If the Firebase display name registered is empty, get the UGS one and register it
        ///// </summary>
        ///// <param name="executionContextData">Context information required for retrieving the UGS username.</param>
        ///// <param name="firebaseAuthResponseData">Firebase authentication response containing user profile data.</param>
        ///// <param name="_logger">Optional logger for logging warnings and errors.</param>
        ///// <returns>A task representing the asynchronous operation.</returns>
        //internal async static Task ValidateFirebaseUsername(IExecutionContext executionContextData, 
        //    FirebaseAuthResponseData firebaseAuthResponseData, string? ugsUsername = null,
        //    ILogger? _logger = null)
        //{
        //    // Validate input parameters
        //    if (firebaseAuthResponseData is null)
        //    {
        //        _logger?.LogWarning("firebaseAuthResponseData is null. Cannot validate firebase username");
        //        return;
        //    }

        //    if (string.IsNullOrEmpty(firebaseAuthResponseData.localId))
        //    {
        //        _logger?.LogError("Firebase User ID is null or empty. Cannot validate firebase username");
        //        return;
        //    }

        //    if (string.IsNullOrEmpty(firebaseAuthResponseData.idToken))
        //    {
        //        _logger?.LogError("Firebase ID Token is null or empty. Cannot validate firebase username");
        //        return;
        //    }

        //    if (string.IsNullOrEmpty(firebaseAuthResponseData.refreshToken))
        //    {
        //        _logger?.LogError("Firebase Refresh Token is null or empty. Cannot validate firebase username");
        //        return;
        //    }

        //    // If the Firebase display name registered is empty, get the UGS one and register it 
        //    if (string.IsNullOrEmpty(firebaseAuthResponseData.displayName))
        //    {
        //        // Make a GET to check if the username associated with the email is the same that the one used to sign in
        //        var registeredName = default(string);
        //        if (!string.IsNullOrEmpty(ugsUsername))
        //            registeredName = ugsUsername;
        //        else
        //            registeredName = await UGSApiHelper.GetUsername(executionContextData,
        //                alternativePlayerID: firebaseAuthResponseData.localId, alternativeAccessToken: firebaseAuthResponseData.idToken,
        //                _logger: _logger).ConfigureAwait(false);

        //        // If there is a username registered in UGS, update the Firebase user profile with that username to link it to UGS and also to have a display name in Firebase
        //        if (!string.IsNullOrEmpty(registeredName))
        //        {
        //            registeredName = FirebaseApiHelper.NormalizeName(registeredName);

        //            // Update Firebase user profile with the username ot link it to UGS
        //            try
        //            {
        //                var firebaseBasicResponse = await FirebaseApiHelper.UpdateFirebaseUserProfile(firebaseAuthResponseData.idToken, firebaseAuthResponseData.refreshToken, _logger, 
        //                    ("displayName", registeredName));

        //                if (firebaseBasicResponse is null)
        //                    _logger?.LogError("Empty response received from Firebase API. Couldn't update firebase display name");

        //            }
        //            catch (Exception ex)
        //            {
        //                _logger?.LogError("Couldn't update the firebase display name with the username from UGS at idp login. Exception: {Exception}",
        //                    ex.Message);
        //            }
        //        } else
        //            _logger?.LogError("Couldn't register the username in firebase at idp login.");
        //    }
        //}

        ///// <summary>
        ///// Ensures the Firebase user profile has a display name by updating it with the UGS username if it is missing.If the Firebase display name registered is empty, get the UGS one and register it
        ///// </summary>
        ///// <param name="executionContextData">Context information required for retrieving the UGS username.</param>
        ///// <param name="firebaseLookUpResponseData">Firebase lookup response containing user profile data.</param>
        ///// <param name="_logger">Optional logger for logging warnings and errors.</param>
        ///// <returns>A task representing the asynchronous operation.</returns>
        //internal async static Task ValidateFirebaseUsername(IExecutionContext executionContextData, 
        //    FirebaseLookUpResponseData firebaseLookUpResponseData, string? optionalAttachedUsername = null,
        //    ILogger? _logger = null)
        //{
        //    // Validate input parameters
        //    if (firebaseLookUpResponseData is null)
        //    {
        //        _logger?.LogWarning("firebaseAuthResponseData is null. Cannot validate firebase username");
        //        return;
        //    }

        //    // Get the first user from the Firebase lookup response data
        //    var user = firebaseLookUpResponseData.users.FirstOrDefault();
        //    if (user is null)
        //    {
        //        _logger?.LogError("Firebase User data is null or empty. Cannot validate firebase username");
        //        return;
        //    }

        //    if (string.IsNullOrEmpty(user.localId))
        //    {
        //        _logger?.LogError("Firebase User ID is null or empty. Cannot validate firebase username");
        //        return;
        //    }

        //    if (string.IsNullOrEmpty(firebaseLookUpResponseData.idToken))
        //    {
        //        _logger?.LogError("Firebase ID Token is null or empty. Cannot validate firebase username");
        //        return;
        //    }

        //    if (string.IsNullOrEmpty(firebaseLookUpResponseData.refreshToken))
        //    {
        //        _logger?.LogError("Firebase Refresh Token is null or empty. Cannot validate firebase username");
        //        return;
        //    }

        //    // If the Firebase display name registered is empty, get the UGS one and register it 
        //    if (string.IsNullOrEmpty(user.displayName))
        //    {
        //        // Make a GET to check if the username associated with the email is the same that the one used to sign in
        //        var registeredName = default(string);
        //        if (!string.IsNullOrEmpty(optionalAttachedUsername))
        //            registeredName = optionalAttachedUsername;
        //        else
        //            registeredName = await UGSApiHelper.GetUsername(executionContextData,
        //                alternativePlayerID: user.localId, alternativeAccessToken: firebaseLookUpResponseData.idToken,
        //                _logger: _logger).ConfigureAwait(false);

        //        // If there is a username registered in UGS, update the Firebase user profile with that username to link it to UGS and also to have a display name in Firebase
        //        if (!string.IsNullOrEmpty(registeredName))
        //        {
        //            registeredName = FirebaseApiHelper.NormalizeName(registeredName);

        //            // Update Firebase user profile with the username ot link it to UGS
        //            try
        //            {
        //                var firebaseBasicResponse = await FirebaseApiHelper.UpdateFirebaseUserProfile(firebaseLookUpResponseData.idToken, firebaseLookUpResponseData.refreshToken, _logger,
        //                    ("displayName", registeredName));

        //                if (firebaseBasicResponse is null)
        //                    _logger?.LogError("Empty response received from Firebase API. Couldn't update firebase display name");

        //            }
        //            catch (Exception ex)
        //            {
        //                _logger?.LogError("Couldn't update the firebase display name with the username from UGS at idp login. Exception: {Exception}",
        //                    ex.Message);
        //            }
        //        } else
        //            _logger?.LogError("Couldn't register the username in firebase at idp login.");
        //    }
        //}

        /// <summary>
        /// Ensures the Firebase user profile has a display name by updating it with the UGS username if it is missing.If the Firebase display name registered is empty, get the UGS one and register it
        /// </summary>
        /// <param name="executionContextData">Context information required for retrieving the UGS username.</param>
        /// <param name="firebaseLookUpResponseData">Firebase lookup response containing user profile data.</param>
        /// <param name="_logger">Optional logger for logging warnings and errors.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        internal async static Task TryToUpdateFirebaseUserProfile(IExecutionContext executionContextData, FirebaseLookUpResponseData firebaseLookUpResponseData,
            ILogger? _logger = null)
        {
            // Validate input parameters
            if (firebaseLookUpResponseData is null)
            {
                _logger?.LogWarning("firebaseLookUpResponseData is null. Cannot update firebase user profile");
                return;
            }

            // Get the first user from the Firebase lookup response data
            var user = firebaseLookUpResponseData.users.FirstOrDefault();
            if (user is null)
            {
                _logger?.LogError("Firebase User data is null or empty. Cannot update firebase user profile");
                return;
            }

            if (string.IsNullOrEmpty(user.localId))
            {
                _logger?.LogError("Firebase User ID is null or empty. Cannot update firebase user profile");
                return;
            }

            if (string.IsNullOrEmpty(firebaseLookUpResponseData.idToken))
            {
                _logger?.LogError("Firebase ID Token is null or empty. Cannot update firebase user profile");
                return;
            }

            if (string.IsNullOrEmpty(firebaseLookUpResponseData.refreshToken))
            {
                _logger?.LogError("Firebase Refresh Token is null or empty. Cannot update firebase user profile");
                return;
            }

            // If the Firebase display name registered is empty, get the UGS one and register it 
            if (string.IsNullOrEmpty(user.displayName))
            {
                // Make a GET to check if the username associated with the email is the same that the one used to sign in
                var registeredName = await UGSApiHelper.GetUsername(executionContextData,
                    alternativePlayerID: user.localId, alternativeAccessToken: firebaseLookUpResponseData.idToken,
                    _logger: _logger).ConfigureAwait(false);

                // If there is a username registered in UGS, update the Firebase user profile with that username to link it to UGS and also to have a display name in Firebase
                if (!string.IsNullOrEmpty(registeredName))
                {
                    registeredName = FirebaseApiHelper.NormalizeName(registeredName);

                    // Update Firebase user profile with the username ot link it to UGS
                    try
                    {
                        var firebaseBasicResponse = await FirebaseApiHelper.UpdateFirebaseUserProfile(firebaseLookUpResponseData.idToken, firebaseLookUpResponseData.refreshToken, _logger, 
                            ("displayName", registeredName));

                        if (firebaseBasicResponse is null)
                            _logger?.LogError("Empty response received from Firebase API. Couldn't update firebase display name");

                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError("Couldn't update the firebase display name with the username from UGS at idp login. Exception: {Exception}",
                            ex.Message);
                    }
                } else
                    _logger?.LogError("Couldn't register the username in firebase at idp login.");
            }
        }

        /// <summary>
        /// Ensures that the specified user exists in the Firebase Realtime Database, creating the user if necessary.Try to ensure the user exists in Realtime Database
        /// </summary>
        /// <param name="executionContext">The execution context for the current operation.</param>
        /// <param name="gameApiClient">The game API client used to interact with backend services.</param>
        /// <param name="firebaseAuthResponseData">The Firebase authentication response data for the user.</param>
        /// <param name="playerId">The unique identifier of the player.</param>
        /// <param name="profileIconID">The identifier for the user's profile icon.</param>
        /// <param name="optionalDisplayName">An optional display name for the user; if not provided, the display name from the authentication response is
        /// used.</param>
        /// <param name="_logger">Optional logger for logging information and errors.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        internal async static Task TryToEnsureUserExistsInRTDB(IExecutionContext executionContext, IGameApiClient gameApiClient,
            FirebaseAuthResponseData firebaseAuthResponseData, string playerId, string profileIconID, string? optionalDisplayName = null,
            ILogger? _logger = null)
        {
            // Validate input parameters
            if (firebaseAuthResponseData is null)
            {
                _logger?.LogWarning("Auth: firebaseAuthResponseData is null. Cannot ensure user exist in rtdb.");
                return;
            }

            if (string.IsNullOrEmpty(firebaseAuthResponseData.localId))
            {
                _logger?.LogError("Auth: Firebase User ID is null or empty. Cannot record Firebase access data.");
                return;
            }

            if (string.IsNullOrEmpty(firebaseAuthResponseData.idToken))
            {
                _logger?.LogError("Auth:  Firebase ID Token is null or empty. Cannot record Firebase access data.");
                return;
            }

            if (string.IsNullOrEmpty(firebaseAuthResponseData.refreshToken))
            {
                _logger?.LogError("Auth: Firebase Refresh Token is null or empty. Cannot record Firebase access data.");
                return;
            }

            // Create the path used in Firestore to get the club data
            try
            {
                var registeredName = !string.IsNullOrEmpty(optionalDisplayName) ? optionalDisplayName : firebaseAuthResponseData.displayName;

                await FirebaseApiHelper.EnsureUserExistsInRealtimeAsync
                    (executionContext: executionContext,
                    gameApiClient: gameApiClient,
                    unityUserId: playerId,
                    displayName: registeredName,
                    profileIconID: profileIconID,
                    logger: _logger);

                _logger?.LogInformation("Auth: User {DisplayName} ensured to exist in Realtime Database successfully.",
                    registeredName);
            }
            catch (Exception ex)
            {
                _logger?.LogError("Auth: Failed to ensure user exists in Realtime Database. Exception: {Exception}",
                    ex.Message);
            }
        }

        /// <summary>
        /// Ensures that the specified user exists in the Firebase Realtime Database, creating the user if necessary.Try to ensure the user exists in Realtime Database
        /// </summary>
        /// <param name="executionContext">The execution context for the current operation.</param>
        /// <param name="gameApiClient">The game API client used to interact with backend services.</param>
        /// <param name="firebaseLookUpResponseData">The Firebase lookup response data for the user.</param>
        /// <param name="playerId">The unique identifier of the player.</param>
        /// <param name="profileIconID">The identifier for the user's profile icon.</param>
        /// <param name="optionalDisplayName">An optional display name for the user; if not provided, the display name from the authentication response is
        /// used.</param>
        /// <param name="_logger">Optional logger for logging information and errors.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        internal async static Task TryToEnsureUserExistsInRTDB(IExecutionContext executionContext, IGameApiClient gameApiClient,
            FirebaseLookUpResponseData firebaseLookUpResponseData, string playerId, string profileIconID, string? optionalDisplayName = null,
            ILogger? _logger = null)
        {
            // Validate input parameters
            if (firebaseLookUpResponseData is null)
            {
                _logger?.LogWarning("firebaseAuthResponseData is null. Cannot ensure user exist in rtdb.");
                return;
            }

            // Get the first user from the Firebase lookup response data
            var user = firebaseLookUpResponseData.users.FirstOrDefault();
            if (user is null)
            {
                _logger?.LogError("Firebase User data is null or empty. Cannot record Firebase access data.");
                return;
            }

            if (string.IsNullOrEmpty(user.localId))
            {
                _logger?.LogError("Firebase User ID is null or empty. Cannot record Firebase access data.");
                return;
            }

            if (string.IsNullOrEmpty(firebaseLookUpResponseData.idToken))
            {
                _logger?.LogError("Firebase ID Token is null or empty. Cannot record Firebase access data.");
                return;
            }

            if (string.IsNullOrEmpty(firebaseLookUpResponseData.refreshToken))
            {
                _logger?.LogError("Firebase Refresh Token is null or empty. Cannot record Firebase access data.");
                return;
            }

            // Create the path used in Firestore to get the club data
            try
            {
                var registeredName = !string.IsNullOrEmpty(optionalDisplayName) ? optionalDisplayName : user.displayName;

                await FirebaseApiHelper.EnsureUserExistsInRealtimeAsync
                    (executionContext: executionContext,
                    gameApiClient: gameApiClient,
                    unityUserId: playerId,
                    displayName: registeredName,
                    profileIconID: profileIconID,
                    logger: _logger);

                _logger?.LogInformation("User {DisplayName} ensured to exist in Realtime Database successfully.",
                    registeredName);
            }
            catch (Exception ex)
            {
                _logger?.LogError("Failed to ensure user exists in Realtime Database. Exception: {Exception}",
                    ex.Message);
            }
        }

        /// <summary>
        /// Validates and updates a club member's name in Firestore if the provided username differs from the current
        /// one.
        /// </summary>
        /// <param name="executionContext">Execution context for the operation.</param>
        /// <param name="gameApiClient">Game API client used for Firestore communication.</param>
        /// <param name="firebaseAuthResponseData">Firebase authentication response data for the user.</param>
        /// <param name="userUnityId">Unity ID of the user.</param>
        /// <param name="username">New username to set for the club member.</param>
        /// <param name="clubName">Name of the club to validate and update.</param>
        /// <param name="_logger">Optional logger for logging warnings, information, and errors.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        internal async static Task ValidateClubName(IExecutionContext executionContext, IGameApiClient gameApiClient,
            FirebaseAuthResponseData firebaseAuthResponseData,
            string? userUnityId, string? username, string? clubName,
            ILogger? _logger = null)
        {
            // Validate input parameters
            if (firebaseAuthResponseData is null)
            {
                _logger?.LogWarning("Look: firebaseAuthResponseData is null. Cannot validate Firebase club name");
                return;
            }

            if (string.IsNullOrEmpty(firebaseAuthResponseData.localId))
            {
                _logger?.LogError("Look: Firebase User ID is null or empty. Cannot validate Firebase club name.");
                return;
            }

            if (string.IsNullOrEmpty(firebaseAuthResponseData.idToken))
            {
                _logger?.LogError("Look: Firebase ID Token is null or empty. Cannot validate Firebase club name.");
                return;
            }

            if (string.IsNullOrEmpty(firebaseAuthResponseData.refreshToken))
            {
                _logger?.LogError("Look: Firebase Refresh Token is null or empty. Cannot validate Firebase club name.");
                return;
            }

            // If the user doesn't have a club, there is no need to validate the club name
            if (string.IsNullOrEmpty(clubName))
                return;

            // Validate input parameters
            if (string.IsNullOrEmpty(userUnityId) || string.IsNullOrEmpty(username))
            {
                _logger?.LogWarning("Look: Unity ID, username, or club name is null or empty. Cannot change club name.");
                return;
            }

            // If the user doesn't have a club, there is no need to validate the club name
            if (string.IsNullOrEmpty(clubName))
                return;

            // Validate input parameters
            if (string.IsNullOrEmpty(userUnityId) || string.IsNullOrEmpty(username))
            {
                _logger?.LogWarning("Look: Unity ID, username, or club name is null or empty. Cannot change club name.");
                return;
            }

            // Create the path used in Firestore to get the club data
            var clubDataJson = await FirebaseApiHelper.GetDocumentAsync
                (firebaseAuthResponseData.idToken,
                firebaseAuthResponseData.refreshToken,
                collection: FirebaseApiHelper.clubsCollectionIdKey,
                documentId: clubName,
                logger: _logger);

            // Check if the club data exists
            var clubData = !string.IsNullOrEmpty(clubDataJson)
                ? FirestoreClubData.ParseClubData(clubDataJson)
                : null;

            // If the club data doesn't exist, log a warning and return
            if (clubData is null)
            {
                _logger?.LogWarning("Club {clubName} does not exist.",
                    clubName);
                return;
            }

            // Try to get the member data of the user in the club data using the firebase ID, if it doesn't exist log a warning and return
            var ourMemberData = clubData.members.FirstOrDefault(x => x.firebaseMemberId == firebaseAuthResponseData.localId);
            if (ourMemberData is null)
            {
                _logger?.LogWarning("Target member {firebaseAuthResponseData.localId} not found in club data.",
                    firebaseAuthResponseData.localId);
                return;
            }

            // Check if the username is already the same
            if (ourMemberData.memberName == username)
            {
                _logger?.LogInformation("Member {firebaseAuthResponseData.localId} already has the username {username}. No update needed.",
                    firebaseAuthResponseData.localId, username);
                return;
            }

            // Override the member name with the new username
            ourMemberData.memberName = username;

            // Build Firestore update payload (replaces entire array)
            var updatedFields = new
            {
                members = FirebaseApiHelper.BuildFirestoreMembersArray(clubData.members),
                updatedAt = new { timestampValue = DateTime.UtcNow.ToString("o") }
            };

            // Send update request to Firestore
            var updateResponse = await FirebaseApiHelper.PatchDocumentAsync(
                executionContext,
                gameApiClient,
                FirebaseApiHelper.clubsCollectionIdKey,
                clubName,
                updatedFields,
                updateFieldPaths: ["members", "updatedAt"]
            );

            // Log the result of the update operation
            if (!string.IsNullOrEmpty(updateResponse))
                _logger?.LogInformation("Club {clubName} updated member with id <b>{firebaseAuthResponseData.localId}</b> with new name {username} successfully.",
                    clubName, firebaseAuthResponseData.localId, username);
            else
                _logger?.LogError("Failed to update member with id {firebaseAuthResponseData.localId} to new name {username}.",
                    firebaseAuthResponseData.localId, username);
        }

        /// <summary>
        /// Validates and updates a club member's name in Firestore if the provided username differs from the current
        /// one.
        /// </summary>
        /// <param name="executionContext">Execution context for the operation.</param>
        /// <param name="gameApiClient">Game API client used for Firestore communication.</param>
        /// <param name="firebaseLookUpResponseData">Firebase authentication response data for the user.</param>
        /// <param name="userUnityId">Unity ID of the user.</param>
        /// <param name="username">New username to set for the club member.</param>
        /// <param name="clubName">Name of the club to validate and update.</param>
        /// <param name="_logger">Optional logger for logging warnings, information, and errors.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        internal async static Task ValidateClubName(IExecutionContext executionContext, IGameApiClient gameApiClient,
            FirebaseLookUpResponseData firebaseLookUpResponseData,
            string? userUnityId, string? username, string? clubName,
            ILogger? _logger = null)
        {
            // Validate input parameters
            if (firebaseLookUpResponseData is null)
            {
                _logger?.LogWarning("firebaseAuthResponseData is null. Cannot validate Firebase club name");
                return;
            }

            // Get the first user from the Firebase lookup response data
            var user = firebaseLookUpResponseData.users.FirstOrDefault();
            if (user is null)
            {
                _logger?.LogError("Firebase User data is null or empty. Cannot validate Firebase club name");
                return;
            }

            if (string.IsNullOrEmpty(user.localId))
            {
                _logger?.LogError("Firebase User ID is null or empty. Cannot record Firebase access data.");
                return;
            }

            if (string.IsNullOrEmpty(firebaseLookUpResponseData.idToken))
            {
                _logger?.LogError("Firebase ID Token is null or empty. Cannot record Firebase access data.");
                return;
            }

            if (string.IsNullOrEmpty(firebaseLookUpResponseData.refreshToken))
            {
                _logger?.LogError("Firebase Refresh Token is null or empty. Cannot record Firebase access data.");
                return;
            }

            // If the user doesn't have a club, there is no need to validate the club name
            if (string.IsNullOrEmpty(clubName))
                return;

            // Validate input parameters
            if (string.IsNullOrEmpty(userUnityId) || string.IsNullOrEmpty(username))
            {
                _logger?.LogWarning("Unity ID, username, or club name is null or empty. Cannot change club name.");
                return;
            }

            // Create the path used in Firestore to get the club data
            var clubDataJson = await FirebaseApiHelper.GetDocumentAsync
                (firebaseLookUpResponseData.idToken,
                firebaseLookUpResponseData.refreshToken,
                collection: FirebaseApiHelper.clubsCollectionIdKey,
                documentId: clubName,
                logger: _logger);

            // Check if the club data exists
            var clubData = !string.IsNullOrEmpty(clubDataJson)
                ? FirestoreClubData.ParseClubData(clubDataJson)
                : null;

            // If the club data doesn't exist, log a warning and return
            if (clubData is null)
            {
                _logger?.LogWarning("Club {clubName} does not exist.",
                    clubName);
                return;
            }

            // Try to get the member data of the user in the club data using the firebase ID, if it doesn't exist log a warning and return
            var ourMemberData = clubData.members.FirstOrDefault(x => x.firebaseMemberId == user.localId);
            if (ourMemberData is null)
            {
                _logger?.LogWarning("Target member {UserID} not found in club data.",
                    user.localId);
                return;
            }

            // Check if the username is already the same
            if (ourMemberData.memberName == username)
            {
                _logger?.LogInformation("Member {UserID} already has the username {username}. No update needed.",
                    user.localId, username);
                return;
            }

            // Override the member name with the new username
            ourMemberData.memberName = username;

            // Build Firestore update payload (replaces entire array)
            var updatedFields = new
            {
                members = FirebaseApiHelper.BuildFirestoreMembersArray(clubData.members),
                updatedAt = new { timestampValue = DateTime.UtcNow.ToString("o") }
            };

            // Send update request to Firestore
            var updateResponse = await FirebaseApiHelper.PatchDocumentAsync(
                executionContext,
                gameApiClient,
                FirebaseApiHelper.clubsCollectionIdKey,
                clubName,
                updatedFields,
                updateFieldPaths: ["members", "updatedAt"]
            );

            // Log the result of the update operation
            if (!string.IsNullOrEmpty(updateResponse))
                _logger?.LogInformation("Club {clubName} updated member with id <b>{firebaseAuthResponseData.localId}</b> with new name {username} successfully.",
                    clubName, user.localId, username);
            else
                _logger?.LogError("Failed to update member with id {firebaseAuthResponseData.localId} to new name {username}.",
                    user.localId, username);
        }
        #endregion

        #region UGS Helpers
        /*
            Consider that those helpers are trying to avoid save data for theyselves, they are just trying to update the payload 
            that will be used in the main function to save the data in UGS, so they will only update the payload if it's necessary
            to avoid unnecessary updates and potential conflicts with other data that might be updated at the same time in the main function.
         */

        /// <summary>
        /// Checks if the Firebase user has the same email as the UGS-associated email and updates it in Firebase if
        /// necessary.
        /// </summary>
        /// <param name="firebaseLookUpResponseData">The Firebase lookup response data containing user information.</param>
        /// <param name="emailAssociatedToUGS">The email address associated with UGS to compare and register in Firebase.</param>
        /// <param name="idToken">The Firebase ID token used for authentication.</param>
        /// <param name="refreshToken">The Firebase refresh token used for authentication.</param>
        /// <param name="_logger">Optional logger for logging information, warnings, and errors.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        internal static async Task CheckIfFirebaseUserHasSameUGSEmail(FirebaseLookUpResponseData? firebaseLookUpResponseData,
            string emailAssociatedToUGS,
            ILogger? _logger = null)
        {
            // Validate input parameters
            if (firebaseLookUpResponseData is null or { users: null or { Length: 0 } })
            {
                _logger?.LogWarning("firebaseLookUpResponseData is null or its users are null or empty. Cannot check email registration.");
                return;
            }

            // Get the first user from the Firebase lookup response data
            var user = firebaseLookUpResponseData.users.FirstOrDefault();
            if (user is null)
            { 
                _logger?.LogError("Firebase User data is null or empty. Cannot check email registration.");
                return;
            }

            if (string.IsNullOrEmpty(user.localId))
            { 
                _logger?.LogError("Firebase User ID is null or empty. Cannot check email registration.");
                return;
            }

            if (string.IsNullOrEmpty(firebaseLookUpResponseData.idToken))
            { 
                _logger?.LogError("Firebase ID Token is null or empty. Cannot check email registration.");
                return;
            }

            if (string.IsNullOrEmpty(firebaseLookUpResponseData.refreshToken))
            { 
                _logger?.LogError("Firebase Refresh Token is null or empty. Cannot check email registration.");
                return;
            }

            // Register the email into Firebase if it was got from UGS and if not registered yet send the verification email
            if (!string.IsNullOrEmpty(emailAssociatedToUGS) && 
                (string.IsNullOrEmpty(user.email) || emailAssociatedToUGS != user.email))
            {
                _logger?.LogInformation("Registering email {EmailDataResponse} into Firebase...", emailAssociatedToUGS);

                // Register the email into Firebase
                await FirebaseApiHelper.UpdateFirebaseUserProfile(
                    firebaseLookUpResponseData.idToken, firebaseLookUpResponseData.refreshToken, 
                    _logger, 
                    ("email", emailAssociatedToUGS));

                firebaseLookUpResponseData.users[0].email = emailAssociatedToUGS;
            }
        }
        
        /// <summary>
        /// Checks if the Firebase user has the same email as the UGS-associated email and updates it in Firebase if
        /// necessary.
        /// </summary>
        /// <param name="firebaseAuthResponseData">The Firebase lookup response data containing user information.</param>
        /// <param name="emailAssociatedToUGS">The email address associated with UGS to compare and register in Firebase.</param>
        /// <param name="idToken">The Firebase ID token used for authentication.</param>
        /// <param name="refreshToken">The Firebase refresh token used for authentication.</param>
        /// <param name="_logger">Optional logger for logging information, warnings, and errors.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        internal static async Task CheckIfFirebaseUserHasSameUGSEmail(FirebaseAuthResponseData? firebaseAuthResponseData,
            string? emailAssociatedToUGS,
            ILogger? _logger = null)
        {
            // Validate input parameters
            if (string.IsNullOrEmpty(emailAssociatedToUGS))
            {
                _logger?.LogError("Email associated to UGS is null or empty. Cannot check email registration.");
                return;
            }

            if (firebaseAuthResponseData is null)
            {
                _logger?.LogWarning("Firebase authentication response data is null. Cannot check email registration.");
                return;
            }

            if (string.IsNullOrEmpty(firebaseAuthResponseData.localId))
            { 
                _logger?.LogError("Firebase User ID is null or empty. Cannot check email registration.");
                return;
            }

            if (string.IsNullOrEmpty(firebaseAuthResponseData.idToken))
            {
                _logger?.LogError("Firebase ID Token is null or empty. Cannot check email registration.");
                return;
            }

            if (string.IsNullOrEmpty(firebaseAuthResponseData.refreshToken))
            {
                _logger?.LogError("Firebase Refresh Token is null or empty. Cannot check email registration.");
                return;
            }

            // Register the email into Firebase if it was got from UGS and if not registered yet send the verification email
            if (!string.IsNullOrEmpty(emailAssociatedToUGS) && 
                (string.IsNullOrEmpty(firebaseAuthResponseData.email) || emailAssociatedToUGS != firebaseAuthResponseData.email))
            {
                _logger?.LogInformation("Registering email {EmailDataResponse} into Firebase...", emailAssociatedToUGS);

                // Register the email into Firebase
                await FirebaseApiHelper.UpdateFirebaseUserProfile(
                    firebaseAuthResponseData.idToken, firebaseAuthResponseData.refreshToken, 
                    _logger, 
                    ("email", emailAssociatedToUGS));

                firebaseAuthResponseData.email = emailAssociatedToUGS;
            }
        }

        /// <summary>
        /// Records Firebase authentication data into the provided payload dictionary using information from the
        /// Firebase Auth response
        /// </summary>
        /// <param name="payload">The dictionary to be updated with Firebase access data.</param>
        /// <param name="firebaseAuthResponseData">The Firebase authentication response data containing user credentials.</param>
        /// <param name="firebaseIDKey">The key used to store the Firebase user ID in the payload.</param>
        /// <param name="firebaseIDTokenKey">The key used to store the Firebase ID token in the payload.</param>
        /// <param name="firebaseProviderIDKey">The key used to store the Firebase provider ID in the payload.</param>
        /// <param name="firebaseRefreshTokenKey">The key used to store the Firebase refresh token in the payload.</param>
        /// <param name="_logger">Optional logger for error reporting.</param>
        /// <returns>A task representing the asynchronous save operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the contextData parameter is null.</exception>
        /// <exception cref="ArgumentException">Thrown when required fields in firebaseAuthResponseData are null or empty.</exception>
        internal static void TryToRecordFirebaseAccessData(ref Dictionary<string, object> payload,
            FirebaseAuthResponseData? firebaseAuthResponseData,
            string? firebaseID, string? firebaseIDToken, string? firebaseRefreshToken,
            string firebaseIDKey, string firebaseIDTokenKey, string firebaseProviderIDKey, string firebaseRefreshTokenKey,
            ILogger? _logger = null)
        {
            // Validate input parameters
            if (firebaseAuthResponseData is null)
            {
                _logger?.LogWarning("Firebase authentication response data is null. Cannot record Firebase access data.");
                return;
            }

            if (string.IsNullOrEmpty(firebaseAuthResponseData.localId))
            {
                _logger?.LogError("Firebase User ID is null or empty. Cannot record Firebase access data.");
                return;
            }

            if (string.IsNullOrEmpty(firebaseAuthResponseData.idToken))
            {
                _logger?.LogError("Firebase ID Token is null or empty. Cannot record Firebase access data.");
                return;
            }

            if (string.IsNullOrEmpty(firebaseAuthResponseData.refreshToken))
            {
                _logger?.LogError("Firebase Refresh Token is null or empty. Cannot record Firebase access data.");
                return;
            }

            // Check if the acces data is update or not
            if (firebaseAuthResponseData.localId == firebaseID &&
                firebaseAuthResponseData.idToken == firebaseIDToken &&
                firebaseAuthResponseData.refreshToken == firebaseRefreshToken)
            {
                _logger?.LogInformation("Firebase access data is already up to date. No changes needed.");
                return;
            }

            // If the payload is null, initialize it
            if (payload is null)
                payload = [];

            // If there is no provider ID in the response, assign an empty string to avoid null values in the payload
            var providerId = string.Empty;
            if (!string.IsNullOrEmpty(firebaseAuthResponseData.providerId))
                providerId = firebaseAuthResponseData.providerId;

            // Update the payload with the Firebase authentication data
            payload[firebaseIDKey] = firebaseAuthResponseData.localId;
            payload[firebaseIDTokenKey] = firebaseAuthResponseData.idToken;
            payload[firebaseProviderIDKey] = providerId;
            payload[firebaseRefreshTokenKey] = firebaseAuthResponseData.refreshToken;
        }

        /// <summary>
        /// Records Firebase authentication data into the provided payload dictionary using information from the
        /// Firebase lookup response.
        /// </summary>
        /// <param name="payload">The dictionary to be updated with Firebase access data.</param>
        /// <param name="firebaselookUpResponseData">The Firebase lookup response containing user authentication information.</param>
        /// <param name="firebaseIDKey">The key used to store the Firebase user ID in the payload.</param>
        /// <param name="firebaseIDTokenKey">The key used to store the Firebase ID token in the payload.</param>
        /// <param name="firebaseProviderIDKey">The key used to store the Firebase provider ID in the payload.</param>
        /// <param name="firebaseRefreshTokenKey">The key used to store the Firebase refresh token in the payload.</param>
        /// <param name="_logger">Optional logger for error reporting.</param>
        /// <exception cref="ArgumentException">Thrown when the Firebase user data is null or empty.</exception>
        internal static void TryToRecordFirebaseAccessData(ref Dictionary<string, object> payload,
            FirebaseLookUpResponseData? firebaselookUpResponseData,
            string? firebaseID, string? firebaseIDToken, string? firebaseRefreshToken,
            string firebaseIDKey, string firebaseIDTokenKey, string firebaseProviderIDKey, string firebaseRefreshTokenKey,
            ILogger? _logger = null)
        {
            // Validate input parameters
            if (firebaselookUpResponseData is null or { users: null or { Length: 0 } })
            {
                _logger?.LogError("Firebase lookup response data is null or empty. Cannot record Firebase access data.");
                return;
            }

            // Get the first user from the Firebase lookup response data
            var user = firebaselookUpResponseData.users.FirstOrDefault();
            if (user is null)
            { 
                _logger?.LogError("Firebase User data is null or empty. Cannot record Firebase access data.");
                return;
            }

            if (string.IsNullOrEmpty(user.localId))
            { 
                _logger?.LogError("Firebase User ID is null or empty. Cannot record Firebase access data.");
                return;
            }

            if (string.IsNullOrEmpty(firebaselookUpResponseData.idToken))
            { 
                _logger?.LogError("Firebase ID Token is null or empty. Cannot record Firebase access data.");
                return;
            }

            if (string.IsNullOrEmpty(firebaselookUpResponseData.refreshToken))
            { 
                _logger?.LogError("Firebase Refresh Token is null or empty. Cannot record Firebase access data.");
                return;
            }

            // Check if the acces data is update or not
            if (user.localId == firebaseID &&
                firebaselookUpResponseData.idToken == firebaseIDToken &&
                firebaselookUpResponseData.refreshToken == firebaseRefreshToken)
            {
                _logger?.LogInformation("Firebase access data is already up to date. No changes needed.");
                return;
            }

            // If the payload is null, initialize it
            if (payload is null)
                payload = [];

            // If there is no provider ID in the response, assign an empty string to avoid null values in the payload
            var providerId = string.Empty;
            var providerUserInfo = user.providerUserInfo?.FirstOrDefault();
            if (providerUserInfo is not null and { providerId: not null and not "" })
                providerId = providerUserInfo.providerId;

            // Update the payload with the Firebase authentication data
            payload[firebaseIDKey] = user.localId;
            payload[firebaseIDTokenKey] = firebaselookUpResponseData.idToken;
            payload[firebaseProviderIDKey] = providerId;
            payload[firebaseRefreshTokenKey] = firebaselookUpResponseData.refreshToken;
        }

        /// <summary>
        /// Registers the profile icon ID in the payload and player profile data if it is not already set and an optional
        /// icon ID is provided.If there is no profile icon ID registered in UGS but there is an optional one provided in the parameters, save it in the payload to update it in UGS and also save it in the player profile data to ensure it's updated in Realtime Database as well
        /// </summary>
        /// <param name="payload">The dictionary to update with the player profile data.</param>
        /// <param name="playerProfileData">The player profile data to update with the icon ID.</param>
        /// <param name="profileKey">The key used to store the player profile data in the payload.</param>
        /// <param name="optionalIconId">The optional profile icon ID to register if none is set.</param>
        /// <param name="_logger">Optional logger for logging information.</param>
        internal static void TryToRegisterProfileIconID(ref Dictionary<string, object> payload,
            PlayerProfileData playerProfileData, string profileKey, string? optionalIconId,
            ILogger? _logger = null)
        {
            // If there is no profile icon ID registered in UGS but there is an optional one provided in the parameters, save it in the payload to update it in UGS and also save it in the player profile data to ensure it's updated in Realtime Database as well
            if (string.IsNullOrEmpty(playerProfileData.profileIconID) && !string.IsNullOrEmpty(optionalIconId))
            {
                playerProfileData.profileIconID = optionalIconId;
                (payload ??= [])[profileKey] = playerProfileData;

                _logger?.LogInformation($"Profile icon ID updated with the optional icon ID provided in the parameters: {optionalIconId}");
            }
        }

        /// <summary>
        /// Registers the current UTC date and time as the account creation date in the payload if it is not already
        /// defined.Try to register creation date if it wasn't defined yet
        /// </summary>
        /// <param name="payload">Reference to the payload dictionary where the creation date will be registered.</param>
        /// <param name="accountCreatedAt">The existing account creation date, if any.</param>
        /// <param name="accountCreationKey">The key under which the creation date should be stored in the payload.</param>
        /// <param name="_logger">Optional logger for logging information.</param>
        internal static void TryToRegisterCreationDate(ref Dictionary<string, object> payload,
            string? accountCreatedAt, string accountCreationKey,
            ILogger? _logger = null)
        {
            // If there is no record of account creation date, register it 
            if (string.IsNullOrEmpty(accountCreatedAt))
            {
                _logger?.LogInformation("No record of account creation date. Registering it...");

                // Ensure payload exists and assign creation date
                (payload ??= [])[accountCreationKey] = DateTime.UtcNow.ToString("o");
            }
        }

        /// <summary>
        /// Attempts to update the days streak in the analytics data and reflects the change in the provided payload.Try to update the days streak in analytics data
        /// </summary>
        /// <param name="payload">Reference to the payload dictionary to update with the new analytics data.</param>
        /// <param name="analyticsData">The current analytics data to evaluate and update the days streak.</param>
        /// <param name="analyticsKey">The key under which the updated analytics data should be stored in the payload.</param>
        /// <param name="_logger">Optional logger for logging information about the update process.</param>
        internal static void TryToUpdateDaysStreak(ref Dictionary<string, object> payload, 
            AnalyticsData? analyticsData, string analyticsKey,
            ILogger? _logger = null)
        {
            var updatedAnalyticsData = UGSApiHelper.UpdateDaysStreak(analyticsData);
            if (updatedAnalyticsData is not null)
            { 
                _logger?.LogInformation("Days streak updated successfully.");

                // If payload is null, initialize it
                if (payload is null)
                    payload = [];

                // Update the analytics data in the payload
                (payload ??= [])[analyticsKey] = updatedAnalyticsData;
            }
        }

        /// <summary>
        /// Validates that the username used to sign in matches the username associated in the UGS,
        /// updating it if necessary.
        /// 
        /// It's necessary clarify, one thing is de username that the user uses as credential. 
        /// This is linked to credentials in UGS, not directly the entire UGS account
        /// </summary>
        /// <param name="executionContext">The execution context for the operation.</param>
        /// <param name="gameApiClient">The game API client used for communication.</param>
        /// <param name="playerAuthResponseData">Authentication response data from UGS.</param>
        /// <param name="usernameToCheck">The username used during sign-in.</param>
        /// <param name="_logger">Optional logger for logging warnings and information.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        internal async static Task ValidateUGSUsername(IExecutionContext executionContext, IGameApiClient gameApiClient,
            string playerId, string idToken, string? usernameToCheck, string? optionalAttachedUsername = null,
            ILogger? _logger = null)
        {
            // Validate input parameters
            if (string.IsNullOrEmpty(usernameToCheck))
            {
                _logger?.LogError("Player username is null or empty. Cannot validate username.");
                return;
            }

            var registeredUsername = default(string);

            // If there is an optional attached username provided in the parameters, use it directly to compare with the username used to sign in to avoid unnecessary GET request to UGS API.
            // This optional attached username should be used in scenarios where you already have the username associated with the email from a previous GET request to UGS API,
            // so you can attach it to the parameters and avoid doing another GET request just to get the username for the validation.
            if (!string.IsNullOrEmpty(optionalAttachedUsername))
                registeredUsername = optionalAttachedUsername;
            
            // Make a GET to check if the username associated with the email is the same that the one used to sign in
            else
                registeredUsername = await UGSApiHelper.GetUsername(executionContext, 
                    alternativePlayerID: playerId, alternativeAccessToken: idToken, 
                    _logger).ConfigureAwait(false);

            // Check if the username associated with the email matches with the username used to sign in. Consider it a match if the registered username
            // contains the username used to sign in to avoid issues with special characters or formatting differences
            var areUsernamesMatching = !string.IsNullOrEmpty(registeredUsername) 
                && (registeredUsername == usernameToCheck || registeredUsername.Contains(usernameToCheck));
            
            _logger?.LogInformation("[Test] Registered Username: {RegisteredUsername}, Username to Check: {UsernameToCheck}, Are Usernames Matching: {AreUsernamesMatching}", 
                registeredUsername, usernameToCheck, areUsernamesMatching);

            // If the username associated with the email doesn't match with the username used to sign in, change it
            if (!areUsernamesMatching)
            {
                _logger?.LogWarning("The username doesn't match with the one associated with the email in UGS. Attempting to change the username to {PlayerUsername}...", 
                    usernameToCheck);

                // Change the username to the one used to sign in
                var updatedUsername = await UGSApiHelper.SetUsername(executionContext, 
                    newName: usernameToCheck,
                    alternativePlayerID: playerId, alternativeAccessToken: idToken, 
                    _logger: _logger).ConfigureAwait(false);

                // Log the result of the username update operation
                if (string.IsNullOrEmpty(updatedUsername))
                    _logger?.LogWarning("Failed to change the username. Response is empty.");
                else
                    _logger?.LogInformation("Username successfully changed to {UpdatedUsername}.", updatedUsername);
            }
        }     
        #endregion
    }
}
