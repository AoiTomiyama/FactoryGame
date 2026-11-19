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

    private static Kind Classify(DirectionsDomain inputs, DirectionsDomain outputs)
    {
        if ((inputs & outputs) != 0 || ((inputs | outputs) & ~AllSides) != 0) return Kind.Invalid;
        var inputCount = Count(inputs);
        var outputCount = Count(outputs);
        if (inputCount == 1 && (outputCount == 2 || outputCount == 3)) return Kind.Split;
        if (inputCount == 2 && outputCount == 1) return Kind.Merge;
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
