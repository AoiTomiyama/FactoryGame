using UnityEngine;

[CreateAssetMenu(fileName = "ResourceProviderDefinition", menuName = "Scriptable Objects/Provider/ResourceProviderDefinition")]
public class ResourceProviderDefinition : ProviderBaseDefinition
{
    [SerializeField] private ResourceDefinition resourceDatabase;
    public ResourceDefinition ResourceDatabase => resourceDatabase;
}
