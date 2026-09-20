using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Timba.Patterns {
    /// <summary>
    /// Avoid using singletons. 
    /// If you need a class that guarantees a single instance consider using SingleInstanceMonoBehaviour or simmilar
    /// If you must have a singleton, consider registering it as a service instead using ServiceLocator
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SingletonMonoBehaviour<T> : MonoBehaviour where T: SingletonMonoBehaviour<T> {
        private static T _instance;

        public static T Instance {
            get {
                CreateInstance();
                return _instance;
            }
        }

        private static void CreateInstance() {
            if (_instance == null) {
                //find existing instance
                _instance = FindFirstObjectByType<T>();
                if (_instance == null) {
                    //create new instance
                    var go = new GameObject(typeof(T).Name);
                    _instance = go.AddComponent<T>();
                }
                //initialize instance if necessary
                if (!_instance.initialized) {
                    _instance.Initialize();
                    _instance.initialized = true;
                }
            }
        }

        protected virtual void Awake() {
            if (Application.isPlaying) {
                DontDestroyOnLoad(this);
            }

            //check if instance already exists when reloading original scene
            if (_instance != null) {
                DestroyImmediate(gameObject);
            }
        }

        protected bool initialized;

        protected virtual void Initialize() { }
    }
}
