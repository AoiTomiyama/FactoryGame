using UnityEngine;

public interface IExportableApplication
{
    public Vector3 GetPosition(int portId);
    public bool TryExport(int portId, int requestedAmount, out int amount, out ResourceTypeDomain type);
}