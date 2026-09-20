using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ServiceLocatorConfig", menuName = "Timba/Patterns/Service Locator Config")]
public class ServiceLocatorConfig : ScriptableObject {
    public GameObject[] defaultServicesPrefabs;
}
