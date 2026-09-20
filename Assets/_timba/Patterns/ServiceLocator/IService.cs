using UnityEngine;

public interface IService
{
    GameObject gameObject { get; }
    bool IsAlreadyInitialized { get; }
}
