
public interface IResourceReusableApplication
{
    /// <summary>
    /// リソースIDを再利用します。
    /// </summary>
    /// <param name="dir">受け取り側のポートID</param>
    /// <param name="id">再利用するリソースID</param>
    public void Reuse(int dir, int id);
}