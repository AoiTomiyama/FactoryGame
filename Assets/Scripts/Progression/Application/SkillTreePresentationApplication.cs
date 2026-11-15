using System;
using System.Collections.Generic;

/// <summary>購入Applicationの判定を表示状態へ写し、表示側で前提や残高を再計算しない。</summary>
public sealed class SkillTreePresentationApplication
{
    public readonly struct NodeState
    {
        public SkillNodeDomain Node { get; }
        public bool Owned { get; }
        public SkillPurchaseResultApplication PurchaseResult { get; }
        public bool Visible => Owned || PurchaseResult is not (SkillPurchaseResultApplication.PrerequisiteMissing or SkillPurchaseResultApplication.DeliveryStageMissing);
        public bool Highlighted => !Owned && PurchaseResult == SkillPurchaseResultApplication.Success;
        public NodeState(SkillNodeDomain node, bool owned, SkillPurchaseResultApplication result)
        { Node = node; Owned = owned; PurchaseResult = result; }
    }
    private readonly SkillTreeApplication _skills;
    public SkillTreePresentationApplication(SkillTreeApplication skills)
        => _skills = skills ?? throw new ArgumentNullException(nameof(skills));
    public IReadOnlyList<NodeState> Capture()
    {
        var states = new List<NodeState>();
        foreach (var node in _skills.Catalog.Nodes)
            states.Add(new NodeState(node, _skills.IsOwned(node.Uid), _skills.CanPurchase(node.Uid)));
        return states.AsReadOnly();
    }
}
