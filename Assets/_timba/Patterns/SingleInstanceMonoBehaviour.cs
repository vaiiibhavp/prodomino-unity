using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Timba.Patterns {
    public class SingleInstanceMonoBehaviour<T> : MonoBehaviour where T: SingleInstanceMonoBehaviour<T> {
        private static T _instance;

        protected virtual void Awake() {
            //check if instance already exists when reloading original scene
            if (_instance == null) {
                _instance = this as T;
            } else {
                DestroyImmediate(this);
            }
        }
        
        protected bool initialized;

        protected virtual void Initialize() { }
    }
}
