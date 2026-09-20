using System;
using System.Runtime.InteropServices;

namespace FirebaseWebGL.Scripts.FirebaseBridge
{
    public static class FirebaseFirestore
    {
        /// <summary>
        /// Gets a document from a specified collection path and id
        /// Will return the document in JSON form in the callback output
        /// </summary>
        /// <param name="collectionPath"> Collection path </param>
        /// <param name="documentId"> Document id </param>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void GetDocument(string collectionPath, string documentId, string objectName,
            string callback, string fallback);

        /// <summary>
        /// Gets all documents from a specified collection path
        /// Will return the documents in JSON array form in the callback output
        /// </summary>
        /// <param name="collectionPath"> Collection path </param>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void GetDocumentsInCollection(string collectionPath, string objectName, string callback,
            string fallback);

        /// <summary>
        /// Sets document content to a specified collection path and document id
        /// </summary>
        /// <param name="collectionPath"> Collection path </param>
        /// <param name="documentId"> Document id </param>
        /// <param name="value"> JSON document content </param>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void SetDocument(string collectionPath, string documentId, string value, string objectName,
            string callback,
            string fallback);

        /// <summary>
        /// Adds a document to a specified collection path with a firebase generated id
        /// </summary>
        /// <param name="collectionPath"> Collection path </param>
        /// <param name="value"> JSON document content </param>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void AddDocument(string collectionPath, string value, string objectName, string callback,
            string fallback);

        /// <summary>
        /// Updates document content in a specified collection path and document id
        /// </summary>
        /// <param name="collectionPath"> Collection path </param>
        /// <param name="documentId"> Document id </param>
        /// <param name="value"> JSON document content </param>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void UpdateDocument(string collectionPath, string documentId, string value,
            string objectName, string callback,
            string fallback);

        /// <summary>
        /// Deletes document in a specified collection path and document id
        /// </summary>
        /// <param name="collectionPath"> Collection path </param>
        /// <param name="documentId"> Document id </param>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void DeleteDocument(string collectionPath, string documentId, string objectName,
            string callback, string fallback);

        /// <summary>
        /// Deletes a field in a specified collection path and document id
        /// </summary>
        /// <param name="collectionPath"> Collection path </param>
        /// <param name="documentId"> Document id </param>
        /// <param name="field"> Field to delete </param>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void DeleteField(string collectionPath, string documentId, string field, string objectName,
            string callback, string fallback);

        /// <summary>
        /// Adds an element in an array field in a specified collection path and document id
        /// Note: If the element is already in the array, it won't do anything
        /// </summary>
        /// <param name="collectionPath"> Collection path </param>
        /// <param name="documentId"> Document id </param>
        /// <param name="field"> Array field </param>
        /// <param name="value"> Element to add to the array field </param>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void AddElementInArrayField(string collectionPath, string documentId, string field,
            string value, string objectName, string callback, string fallback);

        /// <summary>
        /// Removes an element in an array field in a specified collection path and document id
        /// </summary>
        /// <param name="collectionPath"> Collection path </param>
        /// <param name="documentId"> Document id </param>
        /// <param name="field"> Array field </param>
        /// <param name="value"> Element to remove from the array field </param>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void RemoveElementInArrayField(string collectionPath, string documentId, string field,
            string value, string objectName, string callback, string fallback);

        /// <summary>
        /// Increments a numeric field in a specified collection path and document id by a certain amount
        /// </summary>
        /// <param name="collectionPath"> Collection path </param>
        /// <param name="documentId"> Document id </param>
        /// <param name="field"> Field to increment </param>
        /// <param name="increment"> Increment amount </param>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void IncrementFieldValue(string collectionPath, string documentId, string field,
            int increment, string objectName, string callback, string fallback);

        /// <summary>
        /// Listens for document content changes in a specified collection path and document id
        /// </summary>
        /// <param name="collectionPath"> Collection path </param>
        /// <param name="documentId"> Document id </param>
        /// <param name="includeMetadataChanges"> Whether the listener should trigger for metadata changes </param>
        /// <param name="objectName"> Name of the gameobject to call the onChildChanged/fallback of </param>
        /// <param name="onDocumentChange"> Name of the method to call when the listener is triggered. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void ListenForDocumentChange(string collectionPath, string documentId,
            bool includeMetadataChanges, string objectName, string onDocumentChange,
            string fallback);

        /// <summary>
        /// Stops listening for document changes
        /// </summary>
        /// <param name="collectionPath"> Collection path </param>
        /// <param name="documentId"> Document id </param>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void StopListeningForDocumentChange(string collectionPath, string documentId,
            string objectName, string callback, string fallback);

        /// <summary>
        /// Listens for collection changes in a specified collection path
        /// </summary>
        /// <param name="collectionPath"> Collection path </param>
        /// <param name="includeMetadataChanges"> Whether the listener should trigger for metadata changes </param>
        /// <param name="objectName"> Name of the gameobject to call the onChildChanged/fallback of </param>
        /// <param name="onCollectionChange"> Name of the method to call when the listener is triggered. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void ListenForCollectionChange(string collectionPath, bool includeMetadataChanges,
            string objectName, string onCollectionChange, string fallback);

        /// <summary>
        /// Stops listening for collection changes
        /// </summary>
        /// <param name="collectionPath"> Collection path </param>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void StopListeningForCollectionChange(string collectionPath, string objectName,
            string callback, string fallback);

        /// <summary>
        /// Gets a club rank from a specified collection path and document id
        /// </summary>
        /// <param name="collectionPath"></param>
        /// <param name="documentId"></param>
        /// <param name="objectName"></param>
        /// <param name="callback"></param>
        /// <param name="fallback"></param>
        [DllImport("__Internal")]
        public static extern void GetClubRank(string collectionPath, string documentId, string objectName, string callback, string fallback);

        /// <summary>
        /// Searches clubs by name in a specified collection path
        /// </summary>
        /// <param name="collectionPath"></param>
        /// <param name="searchTerm"></param>
        /// <param name="objectName"></param>
        /// <param name="callback"></param>
        /// <param name="fallback"></param>
        [DllImport("__Internal")]
        public static extern void SearchClubsByName(string collectionPath, string searchTerm, string objectName, string callback, string fallback);

        /// <summary>
        /// Requests the top clubs from Firestore ordered by score (descending).
        /// </summary>
        /// <param name="collectionPath">The Firestore collection path, usually "clubs".</param>
        /// <param name="objectName">The name of the Unity GameObject that will receive the callback.</param>
        /// <param name="callback">The callback method to handle success (must take a single string argument).</param>
        /// <param name="fallback">The callback method to handle failure (must take a single string argument).</param>
        [DllImport("__Internal")]
        public static extern void GetTopClubsByScore(string collectionPath, string objectName, string callback, string fallback);

        /// <summary>
        /// Sends a new chat message to a club chat document in Firestore.
        /// If the document does not exist, it will be created automatically
        /// with the first message and basic metadata.
        /// </summary>
        /// <param name="collectionPath">Path to the collection (e.g. "clubChats").</param>
        /// <param name="clubName">Display name of the club.</param>
        /// <param name="senderId">The player ID of the message sender.</param>
        /// <param name="senderName">The display name of the sender.</param>
        /// <param name="profileIconId">Icon ID of the sender’s profile.</param>
        /// <param name="messageContent">The text content of the message.</param>
        /// <param name="objectName">The Unity GameObject name that receives callbacks.</param>
        /// <param name="callback">Method name called on success (string parameter with success info).</param>
        /// <param name="fallback">Method name called on error (string parameter with error JSON).</param>
        [DllImport("__Internal")]
        public static extern void AddClubMessage(
            string collectionPath,
            string clubName,
            string senderId,
            string senderName,
            string profileIconId,
            string messageContent,
            string objectName,
            string callback,
            string fallback
        );

        /// <summary>
        /// Subscribes to a Firestore document listener for a specific club chat.<br></br><br></br>
        /// Once subscribed, the listener will automatically trigger whenever the
        /// chat document changes — for example, when new messages are added or
        /// existing ones are modified. <br></br><br></br>
        /// This function separates subscription lifecycle callbacks (success/error)
        /// from message event callbacks (snapshot updates/errors).
        /// </summary> 
        /// <param name="collectionPath"> Path to the Firestore collection (e.g. "clubChats"). </param>
        /// <param name="clubId"> The ID of the club document to listen to (e.g. the specific club's Firestore document ID).</param>
        /// <param name="objectName"> The Unity GameObject that will receive all event callbacks through SendMessage().</param>
        /// <param name="onSubscribeSuccess">
        /// The method name in the GameObject called once the listener is successfully registered.
        /// Receives a single string parameter containing JSON with subscription info.
        /// Example:
        /// { "success": true, "message": "Subscribed", "path": "clubChats/club123" }.
        /// </param>
        /// <param name="onSubscribeError"> The method name called when the listener could not be created or failed during setup. Receives a single string parameter (error JSON).</param>
        /// <param name="onMessagesUpdated"> The method name called every time Firestore sends a new snapshot for the chat document. Receives a single string parameter containing the JSON array of chat messages. </param>
        /// <param name="onMessagesError"> The method name called if an error occurs while processing an update event from Firestore. Receives a single string parameter (error JSON).</param>
        [DllImport("__Internal")]
        public static extern void SubscribeToClubChat(
            string collectionPath,
            string clubId,
            string objectName,
            string onSubscribeSuccess,
            string onSubscribeError,
            string onMessagesUpdated,
            string onMessagesError
        );


        /// <summary>
        /// Unsubscribes from a Firestore listener previously created by SubscribeToClubChat.
        /// <br></br><br></br>
        /// This stops receiving updates from the Firestore document and frees resources.
        /// </summary>
        /// <param name="collectionPath">Path to the Firestore collection (e.g. "clubChats").</param>
        /// <param name="clubId">The ID of the club document that was being listened to.</param>
        /// <param name="objectName">The Unity GameObject that receives callbacks.</param>
        /// <param name="onUnsubscribeSuccess">
        /// The method name in the GameObject called when the unsubscribe succeeds.
        /// Receives a single string parameter (confirmation message).
        /// </param>
        /// <param name="onUnsubscribeError">
        /// The method name in the GameObject called when an error occurs during unsubscribe.
        /// Receives a single string parameter (the error JSON).
        /// </param>
        [DllImport("__Internal")]
        public static extern void UnsubscribeFromClubChat(
            string collectionPath,
            string clubId,
            string objectName,
            string onUnsubscribeSuccess,
            string onUnsubscribeError
        );

        public static void SubscribeToClubChat(string collectionPath, string clubId, string objectName, string onSubscribeSuccess, string onSubscribeError, object onMessagesUpdated, object onMessagesError)
        {
            throw new NotImplementedException();
        }
    }
}