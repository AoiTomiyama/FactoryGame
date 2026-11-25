using System;
using UnityEngine;

public interface IExportableApplication
{
    int ExportStock { get; }
    // 生産・搬入、または再有効化によって搬出できる在庫があることを知らせる。
    event Action ExportStockAvailable;
    public Vector3 GetPosition(int portId);
    public bool TryExport(int portId, int requestedAmount, out int amount, out ResourceTypeDomain type);
}