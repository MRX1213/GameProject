using UnityEngine;

/// <summary>
/// Simple outline component for chess pieces.
/// Attach this to your chess piece GameObjects and it will be automatically enabled/disabled when selected.
/// </summary>
public class PieceOutline : MonoBehaviour
{
    [Header("Outline Settings")]
    public Color outlineColor = Color.yellow;
    public float glowIntensity = 2f;
    public float scaleMultiplier = 1.1f;
    
    private Renderer[] pieceRenderers;
    private Material[] originalMaterials;
    private Vector3 originalScale;
    private bool isHighlighted = false;

    void Start()
    {
        // Find all renderers (including children)
        pieceRenderers = GetComponentsInChildren<Renderer>();
        originalScale = transform.localScale;
        
        // Store original materials
        if (pieceRenderers != null && pieceRenderers.Length > 0)
        {
            originalMaterials = new Material[pieceRenderers.Length];
            for (int i = 0; i < pieceRenderers.Length; i++)
            {
                if (pieceRenderers[i] != null && pieceRenderers[i].material != null)
                {
                    originalMaterials[i] = pieceRenderers[i].material;
                }
            }
        }
        
        // Start with outline disabled
        SetOutlineEnabled(false);
    }

    public void SetOutlineEnabled(bool enabled)
    {
        if (isHighlighted == enabled) return; // Already in the desired state
        
        isHighlighted = enabled;
        
        // Method 1: Scale up slightly for visibility
        if (enabled)
        {
            transform.localScale = originalScale * scaleMultiplier;
        }
        else
        {
            transform.localScale = originalScale;
        }
        
        // Method 2: Add emission/glow to materials
        if (pieceRenderers != null)
        {
            for (int i = 0; i < pieceRenderers.Length; i++)
            {
                if (pieceRenderers[i] == null) continue;
                
                if (enabled)
                {
                    // Enable emission on the material
                    Material mat = pieceRenderers[i].material;
                    if (mat != null)
                    {
                        // Try to enable emission
                        if (mat.HasProperty("_EmissionColor"))
                        {
                            mat.EnableKeyword("_EMISSION");
                            mat.SetColor("_EmissionColor", outlineColor * glowIntensity);
                        }
                        // Alternative: try _GlowColor or _GlowIntensity
                        else if (mat.HasProperty("_GlowColor"))
                        {
                            mat.SetColor("_GlowColor", outlineColor);
                        }
                        // If using Standard shader, enable emission
                        else if (mat.shader.name.Contains("Standard"))
                        {
                            mat.EnableKeyword("_EMISSION");
                            mat.SetColor("_EmissionColor", outlineColor * glowIntensity);
                        }
                    }
                }
                else
                {
                    // Disable emission
                    Material mat = pieceRenderers[i].material;
                    if (mat != null && originalMaterials != null && i < originalMaterials.Length)
                    {
                        if (mat.HasProperty("_EmissionColor"))
                        {
                            mat.DisableKeyword("_EMISSION");
                            mat.SetColor("_EmissionColor", Color.black);
                        }
                        else if (mat.HasProperty("_GlowColor"))
                        {
                            mat.SetColor("_GlowColor", Color.black);
                        }
                    }
                }
            }
        }
        
        // Method 3: Enable/disable any child objects named "Outline", "Halo", or "Highlight"
        Transform[] children = GetComponentsInChildren<Transform>();
        foreach (var child in children)
        {
            if (child != transform)
            {
                string childName = child.name.ToLower();
                if (childName.Contains("outline") || childName.Contains("halo") || childName.Contains("highlight"))
                {
                    child.gameObject.SetActive(enabled);
                }
            }
        }
    }

    void OnEnable()
    {
        // Component enabled - piece might be selected
        // But we'll let SetOutlineEnabled be called explicitly
    }

    void OnDisable()
    {
        SetOutlineEnabled(false);
    }
    
    void OnDestroy()
    {
        // Restore original scale
        if (originalScale != Vector3.zero)
        {
            transform.localScale = originalScale;
        }
    }
}

