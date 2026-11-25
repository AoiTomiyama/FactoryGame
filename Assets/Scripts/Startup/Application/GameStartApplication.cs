using System;

/// <summary>開始設定と、開始処理の一回性を管理する。Scene操作は呼出側が担当する。</summary>
public sealed class GameStartApplication
{
    public enum Stage { Uninitialized, Menu, Configuring, Preparing, Starting, Running, Failed, SelectingSlot, SwitchingSlot }
    public Stage CurrentStage { get; private set; }
    private Stage _setupOrigin;
    private int _newSlot;

    public bool TryOpenNewGame(int slot)
    {
        if (CurrentStage is not (Stage.Menu or Stage.SelectingSlot)) return false;
        _setupOrigin = CurrentStage;
        _newSlot = slot;
        CurrentStage = Stage.Configuring;
        return true;
    }

    public bool TryBeginNewGame(int slot)
    {
        if (CurrentStage != Stage.Configuring || _newSlot != slot) return false;
        CurrentStage = Stage.Preparing;
        return true;
    }

    public bool TryCancelNewGame()
    {
        if (CurrentStage is not (Stage.Configuring or Stage.Preparing)) return false;
        CurrentStage = _setupOrigin;
        return true;
    }

    public void NewGameNeedsAnotherSeed()
    {
        if (CurrentStage != Stage.Preparing) throw new InvalidOperationException("Game is not preparing terrain.");
        CurrentStage = Stage.Configuring;
    }

    public void BeginPreparedGame()
    {
        if (CurrentStage != Stage.Preparing) throw new InvalidOperationException("Game is not preparing terrain.");
        CurrentStage = Stage.Starting;
    }
    public void ShowMenu()
    {
        if (CurrentStage != Stage.Uninitialized) throw new InvalidOperationException("Start menu has already been prepared.");
        CurrentStage = Stage.Menu;
    }
    public bool TryBeginRestore()
    {
        if (CurrentStage is not (Stage.Menu or Stage.SwitchingSlot)) return false;
        CurrentStage = Stage.Starting; return true;
    }

    public void Complete()
    {
        if (CurrentStage != Stage.Starting) throw new InvalidOperationException("Game is not starting.");
        CurrentStage = Stage.Running;
    }
    public void Fail() => CurrentStage = Stage.Failed;
    public bool TryShowSlotSelection()
    {
        if (CurrentStage is not (Stage.Running or Stage.Failed)) return false;
        CurrentStage = Stage.SelectingSlot;
        return true;
    }

    public bool TryBeginSlotSwitch()
    {
        if (CurrentStage != Stage.SelectingSlot) return false;
        CurrentStage = Stage.SwitchingSlot;
        return true;
    }
}
