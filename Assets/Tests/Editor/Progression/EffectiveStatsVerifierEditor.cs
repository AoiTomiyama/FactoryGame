using System;
using UnityEditor;
using UnityEngine;

public static class EffectiveStatsVerifierEditor
{
    public static void Run() { VerifyRules(); if (Application.isBatchMode) EditorApplication.Exit(0); }
    public static void VerifyRules()
    {
        if (EffectiveStatsRulesDomain.Calculate(7, new StatModifierDomain(1.5m, 3)) != 13 ||
            EffectiveStatsRulesDomain.Calculate(50, new StatModifierDomain(0, 80)) != 80 ||
            EffectiveStatsRulesDomain.Calculate(int.MaxValue, StatModifierDomain.Default) != int.MaxValue)
            throw new InvalidOperationException("Invalid configured stat calculation.");
        Invalid<OverflowException>(() => EffectiveStatsRulesDomain.Calculate(int.MaxValue, new StatModifierDomain(2, 0)));
        Invalid<ArgumentOutOfRangeException>(() => new StatModifierDomain(-1, 0));
        Invalid<ArgumentOutOfRangeException>(() => new StatModifierDomain(1, -1));
        Invalid<ArgumentOutOfRangeException>(() => new StatModifierDomain(0, 0));
        Invalid<ArgumentOutOfRangeException>(() => EffectiveStatsRulesDomain.Calculate(0, StatModifierDomain.Default));
        Debug.Log("Configured ability boundary checks passed.");
    }
    private static void Invalid<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new InvalidOperationException("Expected " + typeof(T).Name); }
}
