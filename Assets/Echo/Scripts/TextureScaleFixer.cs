using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class TextureScaleFixer : MonoBehaviour
{
    [Tooltip("Масштаб текстуры (по умолчанию 1,1)")]
    public Vector2 textureScale = new ();

    private Renderer _renderer;

    void OnEnable()
    {
        _renderer = GetComponent<Renderer>();
        UpdateTextureScale();
    }

    void OnValidate()
    {
        if (_renderer != null)
            UpdateTextureScale();
    }

    void Update()
    {
        #if UNITY_EDITOR
        if (!Application.isPlaying)
            UpdateTextureScale();
        #endif
    }

    private void UpdateTextureScale()
    {
        if (_renderer == null || _renderer.sharedMaterial == null)
            return;

        // Используем sharedMaterial вместо material
        Vector3 localScale = transform.localScale;
        Vector2 scale = new Vector2();
        
        _renderer.sharedMaterial.SetTextureScale("_MainTex", scale);
    }
}
