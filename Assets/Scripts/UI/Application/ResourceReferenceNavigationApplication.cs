using System.Collections.Generic;

/// <summary>現在の経路だけで循環を防ぎ、別経路からの素材参照は許可する。</summary>
public sealed class ResourceReferenceNavigationApplication
{
    private readonly Stack<ResourceTypeDomain> _history = new();
    public ResourceTypeDomain Current { get; private set; }

    public bool CanEnter(ResourceTypeDomain resource) =>
        resource != ResourceTypeDomain.None && resource != Current && !_history.Contains(resource);

    public bool TryEnter(ResourceTypeDomain resource)
    {
        if (!CanEnter(resource)) return false;
        _history.Push(Current);
        Current = resource;
        return true;
    }

    public bool TryBack()
    {
        if (!_history.TryPop(out var previous)) return false;
        Current = previous;
        return true;
    }
}
