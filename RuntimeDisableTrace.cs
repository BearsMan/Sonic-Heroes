using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RuntimeDisableTrace : MonoBehaviour
{
    private void Awake()
    {
        Debug.Log(
            $"TRACE Awake: {GetHierarchyPath()} | " +
            $"activeSelf={gameObject.activeSelf}, " +
            $"activeInHierarchy={gameObject.activeInHierarchy}",
            this);
    }

    private void OnEnable()
    {
        Debug.Log(
            $"TRACE Enabled: {GetHierarchyPath()}",
            this);
    }

    private void OnDisable()
    {
        Debug.LogError(
            $"TRACE Disabled: {GetHierarchyPath()}\n" +
            $"activeSelf={gameObject.activeSelf}, " +
            $"activeInHierarchy={gameObject.activeInHierarchy}\n" +
            $"Stack trace:\n{Environment.StackTrace}",
            this);
    }

    private void OnDestroy()
    {
        Debug.LogWarning(
            $"TRACE Destroyed: {GetHierarchyPath()}\n" +
            $"Stack trace:\n{Environment.StackTrace}",
            this);
    }

    private string GetHierarchyPath()
    {
        string path = name;
        Transform current = transform.parent;

        while (current != null)
        {
            path = $"{current.name}/{path}";
            current = current.parent;
        }

        return path;
    }
}