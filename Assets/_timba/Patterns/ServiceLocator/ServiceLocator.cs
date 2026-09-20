using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Timba.Patterns {
    public class ServiceLocator : SingletonMonoBehaviour<ServiceLocator> {
        public ServiceLocatorConfig config;
        private Dictionary<Type, IService> services;

        protected override void Initialize() {
            config = Resources.Load<ServiceLocatorConfig>("ServiceLocatorConfig");
            if(config == null) {
                Debug.LogError("No ServiceLocatorConfig asset found in resources. Create it Using the Menu: Timba/Patterns/Service Locator Config");
            }
            services = new Dictionary<Type, IService>();
        }

        /// <summary>
        /// Finds a service of type T. The search is done in the following order:
        /// 1 - Look for a registered service in the Service Locator
        /// 2 - Find an existing game object of type T in the scene
        /// 3 - Find a default service of type T in ServiceLocatorConfig
        /// This call can be slow. Always keep a local reference of the service you find
        /// </summary>
        /// <typeparam name="T">Type of the service to find</typeparam>
        /// <returns></returns>
        public T GetService<T>() where T : Component, IService {
            T service;
            if (services.ContainsKey(typeof(T))) {
                service = (T)services[typeof(T)];
            } else {
                // Search in the current scene
                service = FindObjectsByType<T>(FindObjectsSortMode.None)?.FirstOrDefault();
                if (service != null) {
                    service.gameObject.transform.SetParent(transform);
                } else { 
                    // Search in the configuration
                    GameObject servicePrefab = config.defaultServicesPrefabs.Where(x => x.GetComponentInChildren<T>() != null).FirstOrDefault();
                    if (servicePrefab != null) {
                        GameObject newServiceInstance = Instantiate(servicePrefab, transform);
                        service = newServiceInstance.GetComponentInChildren<T>();
                    } else { 
                        Debug.LogErrorFormat("ServiceLocator could not find an existing or default service for type {0}", typeof(T).FullName);
                    }
                }
                services[typeof(T)] = service;
            }
            return service;
        }
    }
}
