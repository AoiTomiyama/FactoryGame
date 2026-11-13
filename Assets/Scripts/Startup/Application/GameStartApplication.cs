using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>開始地点の選択と、開始処理の一回性を管理する。Scene操作は呼出側が担当する。</summary>
public sealed class GameStartApplication
{
    public enum Stage { Uninitialized, Choosing, Starting, Running, Failed }
    public Stage CurrentStage { get; private set; }
    public IReadOnlyList<StartSiteRulesDomain.Candidate> Candidates { get; private set; } = Array.Empty<StartSiteRulesDomain.Candidate>();
    public int SelectedIndex { get; private set; }
    public StartSiteRulesDomain.Candidate Selected => Candidates.Count == 0 ? null : Candidates[SelectedIndex];
    public bool CanChoose => CurrentStage == Stage.Choosing && Candidates.Count > 0;

    public void Prepare(IEnumerable<StartSiteRulesDomain.Candidate> candidates)
    {
        if (CurrentStage != Stage.Uninitialized) throw new InvalidOperationException("Start session has already been prepared.");
        var copy = (candidates ?? throw new ArgumentNullException(nameof(candidates))).ToArray();
        if (copy.Any(c => c == null)) throw new ArgumentException("Start candidates must be valid.");
        Candidates = Array.AsReadOnly(copy);
        CurrentStage = Stage.Choosing;
    }
    public bool TrySelect(int index)
    {
        if (!CanChoose || index < 0 || index >= Candidates.Count) return false;
        SelectedIndex = index; return true;
    }
    public bool TryBegin(out StartSiteRulesDomain.Candidate candidate)
    {
        candidate = null;
        if (!CanChoose) return false;
        // 通知から再入されても、Sceneの変更を二度始めない。
        CurrentStage = Stage.Starting; candidate = Selected; return true;
    }
    public void Complete()
    {
        if (CurrentStage != Stage.Starting) throw new InvalidOperationException("Game is not starting.");
        CurrentStage = Stage.Running;
    }
    public void Fail() => CurrentStage = Stage.Failed;
}
