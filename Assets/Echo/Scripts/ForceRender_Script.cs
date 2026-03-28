using UnityEngine;

public class ForceRender : MonoBehaviour
{
    public Camera targetCamera;
    public Renderer targetRenderer;

    void OnWillRenderObject()
    {
        if (targetCamera == null || targetRenderer == null)
            return;

        // Принудительно отрисовать объект
        targetCamera.RenderWithShader(targetRenderer.sharedMaterial.shader, "");
    }
}
