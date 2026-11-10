using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class ConnectableCellBaseAdapter : CellBaseAdapter
{
    [SerializeField] private CellPortDefinition[] ports =
    {
        new(0, 0, 0, DirectionsDomain.Right, CellPortUseDomain.Both),
        new(1, 0, 0, DirectionsDomain.Left, CellPortUseDomain.Both),
        new(2, 0, 0, DirectionsDomain.Forward, CellPortUseDomain.Both),
        new(3, 0, 0, DirectionsDomain.Back, CellPortUseDomain.Both)
    };
    public IReadOnlyList<CellPortDefinition> Ports => ports;
    public sealed class Connection
    {
        public ConnectableCellBaseAdapter Owner, Neighbor;
        public int PortId, NeighborPortId;
        public Vector3Int Direction => Owner.PortDirection(PortId);
    }
    public IReadOnlyDictionary<int, Connection> Connections => _connections;
    private readonly Dictionary<int, Connection> _connections = new();
    private bool _connected;
    protected Action OnDisconnected;
    protected event Action<Connection> OnGetConnectedCell;
    protected event Action<CellBaseAdapter> OnLostConnectedCell;

    protected Vector3Int DirectionEnumToVector(DirectionsDomain direction) => direction switch
    {
        DirectionsDomain.Forward => transform.forward.ToCardinalDirection(),
        DirectionsDomain.Back => -transform.forward.ToCardinalDirection(),
        DirectionsDomain.Right => transform.right.ToCardinalDirection(),
        DirectionsDomain.Left => -transform.right.ToCardinalDirection(),
        _ => Vector3Int.zero
    };
    public Vector3Int PortDirection(int id) => DirectionEnumToVector(ports.First(p => p.Id == id).Facing);
    public Vector3 PortPosition(int id)
    {
        var port = ports.First(p => p.Id == id);
        return transform.TransformPoint(new Vector3(port.Cell.x, 0, port.Cell.y));
    }
    public bool HasPort(int id, CellPortUseDomain use) => ports.Any(p => p.Id == id && (p.Use & use) == use);

    // 1マスの送り元も、受け取り先の基準位置ではなく接した外周マスで入力IDを決める。
    protected int TargetPortId(CellBaseAdapter target, Vector3Int direction)
    {
        if (target is not ConnectableCellBaseAdapter connectable) return -1;
        foreach (var port in connectable.Ports)
            if (connectable.PortDirection(port.Id) == -direction &&
                (connectable.PortPosition(port.Id) - (transform.position + direction)).sqrMagnitude < .01f)
                return port.Id;
        return -1;
    }

    public void ValidatePorts()
    {
        var occupied = Footprint.Coordinates(0, 0, 0).ToHashSet();
        var ids = new HashSet<int>();
        var contacts = new HashSet<(int, int, DirectionsDomain)>();
        foreach (var port in ports)
        {
            var direction = port.Facing switch
            {
                DirectionsDomain.Forward => (0, 1), DirectionsDomain.Back => (0, -1),
                DirectionsDomain.Right => (1, 0), DirectionsDomain.Left => (-1, 0),
                _ => throw new InvalidOperationException("Port facing must be cardinal.")
            };
            if (port.Id < 0 || !ids.Add(port.Id) || !contacts.Add((port.Cell.x, port.Cell.y, port.Facing)) ||
                !occupied.Contains((port.Cell.x, port.Cell.y)) ||
                occupied.Contains((port.Cell.x + direction.Item1, port.Cell.y + direction.Item2)) ||
                port.Use == 0 || (port.Use & ~CellPortUseDomain.Both) != 0)
                throw new InvalidOperationException($"Invalid exterior port {port.Id}: {name}");
        }
    }

    public override void InitializeSystem()
    {
        ValidatePorts();
        DisconnectAdjacentCells();
        _connected = true;
        var grid = GridFieldDatabaseAdapter.Instance;
        foreach (var port in ports)
        {
            var position = Vector3Int.RoundToInt(PortPosition(port.Id));
            var direction = PortDirection(port.Id);
            var other = position + direction;
            if (!grid.IsWithinBounds(other.x, other.z) ||
                grid.GetCell(other.x, other.z) is not ConnectableCellBaseAdapter neighbor ||
                neighbor == this || !neighbor._connected) continue;
            foreach (var remote in neighbor.Ports)
            {
                if (neighbor.PortDirection(remote.Id) != -direction ||
                    Vector3Int.RoundToInt(neighbor.PortPosition(remote.Id)) != other ||
                    !Compatible(port.Use, remote.Use) || _connections.ContainsKey(port.Id) ||
                    neighbor._connections.ContainsKey(remote.Id)) continue;
                var here = new Connection { Owner = this, Neighbor = neighbor, PortId = port.Id, NeighborPortId = remote.Id };
                var there = new Connection { Owner = neighbor, Neighbor = this, PortId = remote.Id, NeighborPortId = port.Id };
                // 双方の接点を確定してから通知する。
                _connections.Add(port.Id, here);
                neighbor._connections.Add(remote.Id, there);
                OnGetConnectedCell?.Invoke(here);
                neighbor.OnGetConnectedCell?.Invoke(there);
            }
        }
    }
    private static bool Compatible(CellPortUseDomain a, CellPortUseDomain b) =>
        (a & CellPortUseDomain.Output) != 0 && (b & CellPortUseDomain.Input) != 0 ||
        (a & CellPortUseDomain.Input) != 0 && (b & CellPortUseDomain.Output) != 0;

    private void DisconnectAdjacentCells()
    {
        _connected = false;
        var previous = _connections.Values.ToArray();
        _connections.Clear();
        foreach (var link in previous)
        {
            var neighbor = link.Neighbor;
            if (neighbor == null || !neighbor._connections.TryGetValue(link.NeighborPortId, out var other) || other.Neighbor != this) continue;
            neighbor._connections.Remove(link.NeighborPortId);
            neighbor.OnLostConnectedCell?.Invoke(this);
        }
    }
    public void OnDisconnect() { DisconnectAdjacentCells(); OnDisconnected?.Invoke(); }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        foreach (var link in _connections.Values)
            if (link.Neighbor != null) Gizmos.DrawLine(PortPosition(link.PortId) + Vector3.up * 3,
                link.Neighbor.PortPosition(link.NeighborPortId) + Vector3.up * 3.2f);
    }
}
