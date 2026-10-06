using UnityEngine;

public interface IExportableApplication
{
    public Vector3 GetPosition();
    public bool TryExport(Vector3 from, int requestedAmount, out int amount, out ResourceTypeDomain type);
}