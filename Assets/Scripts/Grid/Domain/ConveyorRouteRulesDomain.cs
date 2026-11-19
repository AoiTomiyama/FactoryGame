using System.Collections.Generic;

/// <summary>ポートの向きを保ったままドラッグで追加できる経路を判定する。</summary>
public static class ConveyorRouteRulesDomain
{
    public enum Kind { Invalid, Crossing, Split, Merge }
    public const DirectionsDomain AllSides = DirectionsDomain.Forward | DirectionsDomain.Back |
        DirectionsDomain.Right | DirectionsDomain.Left;

    public readonly struct Route
    {
        public readonly DirectionsDomain Inputs, Outputs;
        public Kind Type => Classify(Inputs, Outputs);
        public Route(DirectionsDomain inputs, DirectionsDomain outputs)
        { Inputs = inputs; Outputs = outputs; }
    }

    public static DirectionsDomain Opposite(DirectionsDomain side) => side switch
    {
        DirectionsDomain.Forward => DirectionsDomain.Back,
        DirectionsDomain.Back => DirectionsDomain.Forward,
        DirectionsDomain.Right => DirectionsDomain.Left,
        DirectionsDomain.Left => DirectionsDomain.Right,
        _ => 0
    };

    public static DirectionsDomain Side(int x, int z) => (x, z) switch
    {
        (0, 1) => DirectionsDomain.Forward, (0, -1) => DirectionsDomain.Back,
        (1, 0) => DirectionsDomain.Right, (-1, 0) => DirectionsDomain.Left,
        _ => 0
    };

    public static DirectionsDomain Rotate(DirectionsDomain mask, int turns)
    {
        turns = (turns % 4 + 4) % 4;
        var bits = (int)mask;
        for (var i = 0; i < turns; i++)
            bits = ((bits & 3) << 2) | ((bits & 4) >> 1) | ((bits & 8) >> 3);
        return (DirectionsDomain)bits;
    }

    public static Route Rotate(Route route, int turns) => new(Rotate(route.Inputs, turns), Rotate(route.Outputs, turns));

    public static Route Extend(Route current, DirectionsDomain entry, DirectionsDomain exit)
    {
        var added = entry | exit;
        if (added == 0 || ((current.Inputs | current.Outputs) & added) != 0) return default;
        return new Route(current.Inputs | entry, current.Outputs | exit);
    }

    public static (List<(int X, int Z, Route Before, Route After)> Replacements, bool Overlap, bool Invalid)
        Plan(IReadOnlyList<(int X, int Z, Route Existing, DirectionsDomain ConnectedSides)> line, DirectionsDomain travel)
    {
        var replacements = new List<(int X, int Z, Route Before, Route After)>();
        var overlap = false;
        var invalid = false;
        var entry = Opposite(travel);
        for (var i = 0; i < line.Count; i++)
        {
            var (x, z, existing, connectedSides) = line[i];
            if (existing.Inputs == 0) continue;
            var incoming = i == 0 ? 0 : entry;
            var outgoing = i == line.Count - 1 ? 0 : travel;
            var proposed = Extend(existing, incoming, outgoing);
            var type = proposed.Type;
            if (type != Kind.Invalid)
            {
                if ((proposed.Inputs | proposed.Outputs) == AllSides)
                {
                    // 横の枝がそろったら未接続の直進口を外す。直進口を足すドラッグでは残す。
                    var forward = Opposite(proposed.Inputs);
                    var rear = Opposite(proposed.Outputs);
                    if (type == Kind.Split && outgoing != 0 && outgoing != forward && (connectedSides & forward) == 0)
                        proposed = new Route(proposed.Inputs, proposed.Outputs & ~forward);
                    if (type == Kind.Merge && incoming != 0 && incoming != rear && (connectedSides & rear) == 0)
                        proposed = new Route(proposed.Inputs & ~rear, proposed.Outputs);
                }
                replacements.Add((x, z, existing, proposed));
                continue;
            }
            overlap = true;
            // 同じ向きの既設路は通常の重なりとして残す。逆向きなど接続できない経路は成立しない。
            invalid |= (existing.Inputs & incoming) != incoming || (existing.Outputs & outgoing) != outgoing;
        }
        if (invalid) replacements.Clear();
        return (replacements, overlap, invalid);
    }

    private static Kind Classify(DirectionsDomain inputs, DirectionsDomain outputs)
    {
        if ((inputs & outputs) != 0 || ((inputs | outputs) & ~AllSides) != 0) return Kind.Invalid;
        var inputCount = Count(inputs);
        var outputCount = Count(outputs);
        if (inputCount == 1 && (outputCount == 2 || outputCount == 3)) return Kind.Split;
        if ((inputCount == 2 || inputCount == 3) && outputCount == 1) return Kind.Merge;
        if (inputCount != 2 || outputCount != 2) return Kind.Invalid;
        return Rotate(inputs, 2) == outputs ? Kind.Crossing : Kind.Invalid;
    }

    private static int Count(DirectionsDomain mask)
    {
        var bits = (int)mask;
        var count = 0;
        while (bits != 0) { bits &= bits - 1; count++; }
        return count;
    }
}
