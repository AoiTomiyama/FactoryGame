using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

internal static class ConveyorLanePickupAdapter
{
    internal static void Start(ConnectableCellBaseAdapter owner, ConnectableCellBaseAdapter.Connection connection,
        ExportConveyorCellAdapter definition, Func<bool> canReceive, Action<int, ResourceTypeDomain> accept, CancellationToken token)
    {
        if (definition == null || connection.Neighbor is not IExportableApplication source ||
            !connection.Neighbor.HasPort(connection.NeighborPortId, CellPortUseDomain.Output)) return;
        PullAsync().Forget();

        async UniTask PullAsync()
        {
            bool IsConnected() => connection.Neighbor != null &&
                owner.Connections.TryGetValue(connection.PortId, out var current) && ReferenceEquals(current, connection);

            try
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                while (IsConnected())
                {
                    var amount = 0;
                    var type = ResourceTypeDomain.None;
                    await UniTask.WaitUntil(() =>
                    {
                        if (!IsConnected()) return true;
                        if (!canReceive()) return false;
                        var modifier = GridFieldDatabaseAdapter.Instance.StatEffects.GetModifier(
                            CellTypeDomain.ExportConveyor, EquipmentStatDomain.TransferAmount);
                        var limit = EffectiveStatsRulesDomain.Calculate(definition.BaseTransferAmount, modifier);
                        return source.TryExport(connection.NeighborPortId, limit, out amount, out type);
                    }, cancellationToken: token);
                    // 搬出が確定した資源は、送り元が切断されてもこのレーンが受け取る。
                    if (amount > 0) accept(amount, type);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        }
    }
}
