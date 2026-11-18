using System.Collections.Generic;

/// <summary>一覧から選び直し、素材の参照経路では循環を防ぐ。</summary>
public sealed class ResourceReferenceNavigationApplication
{
    private readonly HashSet<ResourceTypeDomain> _path = new();
    public ResourceTypeDomain Current { get; private set; }

    public void Select(ResourceTypeDomain resource)
    {
        _path.Clear();
        Current = resource;
        if (resource != ResourceTypeDomain.None) _path.Add(resource);
    }

    public bool CanFollow(ResourceTypeDomain resource) =>
        Current != ResourceTypeDomain.None && resource != ResourceTypeDomain.None && !_path.Contains(resource);

    public bool TryFollow(ResourceTypeDomain resource)
    {
        if (!CanFollow(resource)) return false;
        _path.Add(resource);
        Current = resource;
        return true;
    }
}
