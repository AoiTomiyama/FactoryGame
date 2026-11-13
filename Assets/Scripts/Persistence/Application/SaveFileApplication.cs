using System;
using System.IO;
using System.Text;

/// <summary>一つの保存枠を直列に置換する。旧ファイルを先に削除しない。</summary>
public sealed class SaveFileApplication
{
    private readonly object _gate = new();
    public string Path { get; }
    public bool Exists => File.Exists(Path);
    public SaveFileApplication(string path) => Path = System.IO.Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
    public string Read() { lock (_gate) return File.ReadAllText(Path, Encoding.UTF8); }
    public void Write(string contents)
    {
        if (string.IsNullOrEmpty(contents)) throw new ArgumentException("Save contents are empty.");
        lock (_gate)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            var temporary = Path + ".writing";
            // Flush(true)を終えてから置換する。世代バックアップは作成しない。
            using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var bytes = new UTF8Encoding(false).GetBytes(contents);
                file.Write(bytes, 0, bytes.Length); file.Flush(true);
            }
            if (File.Exists(Path)) File.Replace(temporary, Path, null);
            else File.Move(temporary, Path);
        }
    }
}
