using System;
using System.IO;

/// <summary>通常枠と保存先を一対一で対応付ける。</summary>
public sealed class SaveSlotCatalogApplication
{
    public const int FirstSlot = 1;
    public const int NormalSlotCount = 3;
    private readonly SaveFileApplication[] _files;

    public SaveSlotCatalogApplication(string directory)
    {
        _files = new SaveFileApplication[NormalSlotCount];
        for (var i = 0; i < NormalSlotCount; i++)
            _files[i] = new SaveFileApplication(Path.Combine(directory, i == 0 ? "factory.json" : $"factory-{i + 1}.json"));
    }

    public static bool IsNormal(int slot) => slot >= FirstSlot && slot <= NormalSlotCount;
    public SaveFileApplication FileFor(int slot) => IsNormal(slot)
        ? _files[slot - FirstSlot] : throw new ArgumentOutOfRangeException(nameof(slot));
    public DateTime? LastSaved(int slot)
    {
        var file = FileFor(slot);
        return file.Exists ? File.GetLastWriteTimeUtc(file.Path) : null;
    }
}
