using System.Runtime.InteropServices;

namespace FirebaseWebGL.Scripts.FirebaseBridge
{
    public static class FirebaseAuth
    {
        /// <summary>
        /// Creates a user with email and password
        /// </summary>
        /// <param name="email"> User email </param>
        /// <param name="password"> User password </param>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void CreateUserWithEmailAndPassword(string email, string password, string objectName, string callback,
            string fallback);
        
        /// <summary>
        /// Signs in a user with email and password
        /// </summary>
        /// <param name="email"> User email </param>
        /// <param name="password"> User password </param>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void SignInWithEmailAndPassword(string email, string password, string objectName, string callback,
            string fallback);
        
        /// <summary>
        /// Signs in a user with Google
        /// </summary>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void SignInWithGoogle(string objectName, string callback,
            string fallback);
        
        /// <summary>
        /// Signs in a user with Facebook
        /// </summary>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void SignInWithFacebook(string objectName, string callback,
            string fallback);

        /// <summary>
        /// Signs in to Firebase using a custom token
        /// </summary>
        /// <param name="refreshToken">The token used to login. It should be a refresh token</param>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void FirebaseSignInWithRefreshToken(string refreshToken, string objectName, string callback,
            string fallback);
        
        /// <summary>
        /// Signs in to Firebase using a custom token
        /// </summary>
        /// <param name="token">The token used to login. Could be id or access token</param>
        /// <param name="tokenType">The type of the token that will be used ('id_token' or 'access_token')</param>
        /// <param name="tokenType">The provider in wich the player will log-in (google or facebook)</param>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void FirebaseSignInWithIdp(string token, string tokenType, string providerId, string objectName, string callback,
            string fallback);


        /// <summary>
        /// Checks if the user is authenticated
        /// </summary>
        [DllImport("__Internal")]
        public static extern int IsUserAuthenticated();

        /// <summary>
        /// Signs out the current user
        /// </summary>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void SignOut(string objectName, string callback,
            string fallback);
        
        /// <summary>
        /// Check if the user email is already verificated
        /// </summary>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void CheckEmailVerification(string objectName, string callback,
            string fallback);

        /// <summary>
        /// Check if the email was verified<br></br><br></br>
        /// 1: Verified<br></br>
        /// 0: Not Verified<br></br>
        /// -1: Not authenticated
        /// </summary>
        [DllImport("__Internal")]
        public static extern int IsEmailVerified();

        /// <summary>
        /// Retrieves the user's Firebase profile information (displayName, photoURL, email, provider info)
        /// using a valid Firebase ID Token. 
        ///
        /// This method calls a JavaScript function inside the WebGL .jslib file, which then performs a
        /// secure REST request to the Firebase Identity Toolkit endpoint `accounts:lookup`. 
        ///
        /// This does NOT open any authentication dialogs or popups. The ID Token must be valid and 
        /// obtained from a previous authentication flow.
        /// </summary>
        /// <param name="objectName"> 
        /// Name of the GameObject that contains the method to receive callback/fallback messages. 
        /// </param>
        /// <param name="callback"> 
        /// Method name to invoke upon success. Must match signature: void Method(string jsonOutput). 
        /// Will receive a serialized JSON object containing user profile information.
        /// </param>
        /// <param name="fallback"> 
        /// Method name to invoke upon failure. Must match signature: void Method(string jsonOutput).  
        /// Will receive a serialized FirebaseError-style object with details about what went wrong.
        /// </param>
        /// <param name="idToken">
        /// A valid Firebase ID Token. This must be generated by a prior authentication process and 
        /// must not be expired. A fresh ID Token allows access to Firebase Identity Toolkit.
        /// </param>
        [DllImport("__Internal")]
        public static extern void GetUserProfile(string idToken, string objectName, string callback,
            string fallback);

        /// <summary>
        /// Listens for changes of the auth state (sign in/sign out)
        /// </summary>
        /// <param name="objectName"> Name of the gameobject to call the onUserSignedIn/onUserSignedOut of </param>
        /// <param name="onUserSignedIn"> Name of the method to call when the user signs in. Method must have signature: void Method(string output). Will return a serialized FirebaseUser object </param>
        /// <param name="onUserSignedOut"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output) </param>
        [DllImport("__Internal")]
        public static extern void OnAuthStateChanged(string objectName, string onUserSignedIn,
            string onUserSignedOut);
    }
}