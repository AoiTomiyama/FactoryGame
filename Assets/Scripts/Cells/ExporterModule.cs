using System;
using UnityEngine;

public class ExporterModule : MonoBehaviour
{
    [SerializeField] private int exporterCapacity;
    public Vector3 ExportBeginPos { get; private set; }
    public ResourceType ExportResourceType { get; set; }
    public int ExportResourceAmount { get; private set; }
    public Action OnExport { get; set; }
    public int ExporterCapacity => exporterCapacity;

    private void OnEnable()
    {
        ExportBeginPos = transform.position;
    }

    public bool TryStackToExporter(int amount)
    {
        if (amount <= 0) return false;
        var available = ExporterCapacity - ExportResourceAmount;
        if (amount > available) return false;

        ExportResourceAmount += amount;
        return true;
    }

    public bool TryExport(out int amount, out ResourceType resourceType)
    {
        amount = 0;
        resourceType = ResourceType.None;
        if (ExportResourceAmount <= 0) return false;
        amount = ExportResourceAmount;
        resourceType = ExportResourceType;
        ExportResourceAmount = 0;
        OnExport?.Invoke();
        return true;
    }
}