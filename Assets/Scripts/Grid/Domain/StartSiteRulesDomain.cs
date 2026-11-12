using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>開始候補の位置と最低限の建設領域。候補の確定前に地形を整えるための規則。</summary>
public static class StartSiteRulesDomain
{
    // 5×5の特異点、南側の初回換金経路、2×2・3×2設備と搬送の余地を含める。
    public static IEnumerable<(int x, int z)> Area(int x, int z)
    {
        for (var dx=-6; dx<=6; dx++) for (var dz=-7; dz<=6; dz++) yield return (x+dx,z+dz);
    }
    public static IReadOnlyList<(int x, int z)> Candidates(int size, int seed, Func<int,int,bool> available)
    {
        var result=new List<(int x,int z)>();
        void TryAdd(int x,int z)
        {
            if (result.Count==3 || x<6 || x+6>=size || z<7 || z+6>=size ||
                result.Any(p=>Math.Abs(p.x-x)<=12 && Math.Abs(p.z-z)<=13)) return;
            if (Area(x,z).All(p=>available(p.x,p.z))) result.Add((x,z));
        }
        var center=size/2;
        TryAdd(center,center);
        var shift=(int)(Hash(unchecked((uint)seed)) % (uint)Math.Max(1,size/6+1))-size/12;
        TryAdd(size/4,center+shift); TryAdd(size-size/4-1,center-shift);
        // 小さいフィールドや保護する手配置設備がある場合も、成立する候補だけを提示する。
        for(var z=7;z+6<size && result.Count<3;z++)
        for(var x=6;x+6<size && result.Count<3;x++) TryAdd(x,z);
        return result.AsReadOnly();
    }
    private static uint Hash(uint value)
    {
        unchecked { value^=value>>16; value*=0x7feb352d; value^=value>>15; value*=0x846ca68b; return value^(value>>16); }
    }
}
