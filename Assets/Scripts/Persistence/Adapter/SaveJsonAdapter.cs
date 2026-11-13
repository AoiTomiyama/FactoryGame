using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public static class SaveJsonAdapter
{
    [Serializable] private sealed class Envelope { public int version; public string payload, sha256; }
    public static string Encode(SaveGameApplication data)
    {
        data.ValidateShape();
        var payload = JsonUtility.ToJson(data);
        return JsonUtility.ToJson(new Envelope {version = 1, payload = payload, sha256 = Digest(payload)});
    }
    public static SaveGameApplication Decode(string json)
    {
        var envelope = JsonUtility.FromJson<Envelope>(json);
        if (envelope == null || envelope.version != 1 || string.IsNullOrEmpty(envelope.payload) ||
            envelope.sha256 != Digest(envelope.payload)) throw new InvalidOperationException("Save file is incomplete, corrupt or unsupported.");
        var data = JsonUtility.FromJson<SaveGameApplication>(envelope.payload);
        if (data == null) throw new InvalidOperationException("Missing save payload.");
        data.ValidateShape(); return data;
    }
    private static string Digest(string value)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
    }
}
