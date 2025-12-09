using UnityEngine;
using System;

/// <summary>
/// Makes a text label always face the active camera (billboard effect)
/// </summary>
public class BillboardText : MonoBehaviour
{
    private Camera targetCamera;
    private Component textMeshPro; // TextMeshPro component (stored as Component for compatibility)
    private TextMesh textMesh; // Fallback for legacy TextMesh
    
    void Start()
    {
        // Try to get TextMeshPro first, then fallback to TextMesh
        Type tmpType = Type.GetType("TMPro.TextMeshPro, Assembly-CSharp");
        if (tmpType != null)
        {
            textMeshPro = GetComponent(tmpType);
        }
        if (textMeshPro == null)
        {
            textMesh = GetComponent<TextMesh>();
        }
        
        UpdateTargetCamera();
    }
    
    void LateUpdate()
    {
        // Update camera reference in case it changed
        if (targetCamera == null || !targetCamera.gameObject.activeInHierarchy || !targetCamera.enabled)
        {
            UpdateTargetCamera();
        }
        
        // Rotate to face camera
        if (targetCamera != null && IsValidTransform(transform) && IsValidTransform(targetCamera.transform))
        {
            // Make the text face the camera
            Vector3 forward = targetCamera.transform.rotation * Vector3.forward;
            Vector3 up = targetCamera.transform.rotation * Vector3.up;
            Vector3 targetPos = transform.position + forward;
            
            // Validate vectors before using LookAt
            if (IsValidVector3(transform.position) && IsValidVector3(targetPos) && IsValidVector3(up))
            {
                transform.LookAt(targetPos, up);
            }
        }
    }
    
    void UpdateTargetCamera()
    {
        // Try to find the active main camera
        targetCamera = Camera.main;
        
        if (targetCamera == null || !targetCamera.gameObject.activeInHierarchy || !targetCamera.enabled)
        {
            // Find any active camera
            Camera[] cameras = FindObjectsOfType<Camera>();
            foreach (Camera cam in cameras)
            {
                if (cam.gameObject.activeInHierarchy && cam.enabled)
                {
                    targetCamera = cam;
                    break;
                }
            }
        }
    }
    
    /// <summary>
    /// Sets the text content
    /// </summary>
    public void SetText(string text)
    {
        if (textMeshPro != null)
        {
            // Use reflection to set text property
            var textProperty = textMeshPro.GetType().GetProperty("text");
            if (textProperty != null)
            {
                textProperty.SetValue(textMeshPro, text);
                return;
            }
        }
        if (textMesh != null)
        {
            textMesh.text = text;
        }
    }
    
    /// <summary>
    /// Gets the text content
    /// </summary>
    public string GetText()
    {
        if (textMeshPro != null)
        {
            // Use reflection to get text property
            var textProperty = textMeshPro.GetType().GetProperty("text");
            if (textProperty != null)
            {
                return textProperty.GetValue(textMeshPro) as string;
            }
        }
        if (textMesh != null)
        {
            return textMesh.text;
        }
        return "";
    }
    
    /// <summary>
    /// Validates that a Vector3 doesn't contain NaN or Infinity values
    /// </summary>
    bool IsValidVector3(Vector3 v)
    {
        return !float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
               !float.IsNaN(v.y) && !float.IsInfinity(v.y) &&
               !float.IsNaN(v.z) && !float.IsInfinity(v.z);
    }
    
    /// <summary>
    /// Validates that a Transform has valid position, rotation, and scale
    /// </summary>
    bool IsValidTransform(Transform t)
    {
        if (t == null) return false;
        return IsValidVector3(t.position) && 
               IsValidVector3(t.rotation.eulerAngles) &&
               IsValidVector3(t.localScale);
    }
}

