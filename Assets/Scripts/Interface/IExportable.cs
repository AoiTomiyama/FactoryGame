using UnityEngine;

public interface IExportable
{
    public Vector3 GetPosition();
    public bool TryExport(Vector3 from, int requestedAmount, out int amount, out ResourceType type);
}