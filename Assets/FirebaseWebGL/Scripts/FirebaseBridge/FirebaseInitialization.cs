using System.Runtime.InteropServices;
using UnityEditor;

namespace FirebaseWebGL.Scripts.FirebaseBridge
{
    public static class FirebaseInitialization
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
        public static extern void InitFirebase(string configJson, string objectName, string callback,
            string fallback);
    }
}