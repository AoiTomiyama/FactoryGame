using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "PipeColorMapping", menuName = "Scriptable Objects/PipeColorMapping")]
public class PipeColorMapping : ScriptableObject
{
    [SerializeField] private ColorMapping[] colorMappings;
    
    [Serializable]
    private struct ColorMapping
    {
        [SerializeField] private PipeColorEnum color;
        [SerializeField] private Material material;
        public PipeColorEnum Color => color;
        public Material Material => material;
    }

    private Dictionary<PipeColorEnum, Material> _colorDict;

    private void OnValidate()
    {
        _colorDict = colorMappings.ToDictionary(map => map.Color, map => map.Material);
    }

    public Material GetPipeColor(PipeColorEnum color)
    {
        if (_colorDict == null || _colorDict.Count == 0) return null;

        return _colorDict.GetValueOrDefault(color);
    }
}