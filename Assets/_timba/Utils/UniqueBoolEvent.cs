using UnityEngine;
using UnityEngine.Events;
using System;

/// <summary>
/// Serializable wrapper that behaves like a UnityEvent but returns a bool.
/// Only one subscriber is expected.
/// </summary>
[System.Serializable]
public class UniqueBoolEvent
{
    // Internal UnityEvent that takes a BoolResult container
    [Serializable]
    private class BoolResultEvent : UnityEvent<BoolResult> { }

    [SerializeField]
    private BoolResultEvent internalEvent = new BoolResultEvent();

    /// <summary>
    /// Invokes the assigned listener and returns its bool result.
    /// If no listener is assigned, returns false.
    /// </summary>
    public bool Invoke()
    {
        var result = new BoolResult();
        internalEvent?.Invoke(result);
        return result.value;
    }

    /// <summary>
    /// Utility container to carry a boolean value between invoker and listener.
    /// </summary>
    [Serializable]
    public class BoolResult
    {
        public bool value;
    }
}
