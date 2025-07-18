using UnityEngine;

public class PlaceholderCell : MonoBehaviour
{
    [SerializeField] private Renderer[] placeholderRenderers;

    public void SetMaterial(Material material)
    {
        foreach (var modelRenderer in placeholderRenderers)
        {
            modelRenderer.material = material;
        }
    }
}
