using System;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

public class GridFieldGenerator : MonoBehaviour
{
    [Header("基本設定")]
    [SerializeField] private GameObject emptyCellPrefab;
    [SerializeField] private int gridSize = 30;

    [Header("グリッドライン設定")]
    [SerializeField] private float lineWidth = 0.1f;
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color lineColor;

    [Header("プロップ設定")]
    [SerializeField] private PropPrefab[] propPrefabs;

    [Header("ノイズの設定")]
    [Tooltip("繰り返しの回数")] [SerializeField] [Range(1, 10)]
    private int octaves = 5;

    [Tooltip("初期周波数")] [SerializeField] [Range(0.01f, 0.2f)]
    private float baseFrequency = 0.05f;

    [Tooltip("振幅の減衰。0に近づけるほどオクターブの上昇における振れ幅の変化が緩やかになる。")] [SerializeField] [Range(0f, 1f)]
    private float persistence = 0.5f;

    [Tooltip("周波数の増加。高くするとオクターブの上昇における周波数の上昇が加速する。")] [SerializeField] [Range(1f, 4f)]
    private float lacunarity = 2f;

    [Tooltip("ノイズ生成のシード値。")] [SerializeField]
    private int seedValue;
    
    private int _oldSeedValue;

    [Serializable]
    private struct PropPrefab
    {
        [SerializeField] [Tooltip("プロップのプレハブ")]
        public GameObject prefab;

        [SerializeField] [Range(0, 1)] [Tooltip("ノイズ値の閾値")]
        public float threshold;

        [Tooltip("ノイズオフセットのランダム値")]
        public Vector2Int NoiseOffset { get; set; }
    }

    private void Start()
    {
        GridFieldDatabase.Instance.InitializeCells(gridSize);
    }

    /// <summary>
    /// シード値をランダムに設定
    /// </summary>
    public void GenerateRandomSeedValue()
    {
        seedValue = Random.Range(int.MinValue, int.MaxValue);
    }

    /// <summary>
    /// グリット情報のクリア
    /// </summary>
    public static void ClearGrid(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var target = parent.GetChild(i).gameObject;

            // エディター上でも操作することを想定して、即時に削除
            DestroyImmediate(target);
        }
    }

    /// <summary>
    /// シーン上にグリッドを生成
    /// </summary>
    public void GenerateGrid(Transform parent)
    {
        // プロップのノイズオフセットをシード値とインスタンスIDから決定
        // これにより、同じシード値であれば常に同じ配置になるが、異なるプレハブを使うと異なる配置になる
        for (var i = 0; i < propPrefabs.Length; i++)
        {
            propPrefabs[i].NoiseOffset = GetHashedVector(seedValue + propPrefabs[i].prefab.GetInstanceID());
        }

        if (parent != null)
        {
            ClearGrid(parent);
        }

        var cellScale = emptyCellPrefab.transform.localScale;
        for (int x = 0; x < gridSize; x++)
        {
            var separator = new GameObject($"Separator_{x}");
            separator.transform.SetParent(parent);
            for (int z = 0; z < gridSize; z++)
            {
                // セルの位置を計算
                var pos = new Vector3(x * cellScale.x, 0, z * cellScale.z);

                var obj = emptyCellPrefab;
                foreach (var p in propPrefabs)
                {
                    var noiseValue = Fbm(x + p.NoiseOffset.x, z + p.NoiseOffset.y, seedValue);

                    if (noiseValue <= p.threshold) continue;

                    // ノイズ値が閾値を超えた場合、配置するオブジェクトを確定
                    obj = p.prefab;
                    break;
                }

                // セルの生成
                var tile = (GameObject)PrefabUtility.InstantiatePrefab(obj, separator.transform);
                tile.transform.position = pos;
                tile.name = $"Tile_{x}_{z}";
            }
        }
    }

    /// <summary>
    /// SHA256でハッシュ化した値を取得
    /// </summary>
    /// <param name="input">元になる数値</param>
    /// <returns>ハッシュ化したベクトル(x, y)</returns>
    private static Vector2Int GetHashedVector(int input)
    {
        using var sha = SHA256.Create();
    
        // int → byte[4] に変換
        var data = BitConverter.GetBytes(input);

        // SHA256でハッシュ化
        var hash = sha.ComputeHash(data);

        // ハッシュ値の一部を取り出す
        // 値が大きすぎるとノイズの計算が不安定になるので、-10,000,000 ～ 10,000,000 の範囲に正規化
        const int NormalizeFactor = 10000000;

        var x = BitConverter.ToInt32(hash, 0) % NormalizeFactor;
        var y = BitConverter.ToInt32(hash, 4) % NormalizeFactor;

        return new(x, y);
    }

    /// <summary>
    /// フラクタルブラウン運動によるノイズを生成
    /// </summary>
    /// <param name="x">ノイズ生成用のX座標</param>
    /// <param name="y">ノイズ生成用のY座標</param>
    /// <param name="seed">オクターブ毎に加えるオフセットに使うシード値</param>
    /// <returns>0.0～1.0 の範囲に正規化されたfBm値</returns>
    private float Fbm(float x, float y, int seed)
    {
        // 各オクターブのノイズ値を累積
        var total = 0f;

        // 最初の周波数（スケール感を決める）
        var frequency = baseFrequency;

        // 最初の振幅（寄与の大きさを決める）
        var amplitude = 1f;

        // 正規化用に、各オクターブの振幅の合計を保持
        var maxValue = 0f;

        // 指定したオクターブ数だけ繰り返す
        for (int i = 0; i < octaves; i++)
        {
            // 周波数を掛けた座標で Perlin ノイズをサンプリングし、振幅を掛けて合算
            total += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;
            
            // シード値に基づくオフセットを加算して、各オクターブで異なるノイズになるようにする
            var offset = GetHashedVector(seed + i);
            x += offset.x;
            y += offset.y;

            // 正規化用に最大値を更新
            maxValue += amplitude;

            // 次のオクターブのために、振幅と周波数を更新
            amplitude *= persistence; // 振幅を減衰させる（通常 0～1）
            frequency *= lacunarity; // 周波数を上げる（通常 >1）
        }

        // 振幅合計で割ることで [0,1] の範囲に正規化して返す
        return total / maxValue;
    }


    /// <summary>
    /// フィールドに合わせたグリッドラインを生成
    /// </summary>
    public void GenerateGridLine(Transform parent)
    {
        var cellSize = emptyCellPrefab.transform.localScale.x;
        var numLines = (gridSize + 1) + (gridSize + 1);
        var numPoints = numLines * 3;

        var obj = new GameObject("GridLines");
        obj.transform.SetParent(parent);
        obj.transform.position = new Vector3(-0.5f, 0.51f, -0.5f) * cellSize;

        var points = new Vector3[numPoints];
        var index = 0;

        // 縦線
        for (int x = 0; x <= gridSize; x++)
        {
            var fx = x * cellSize;
            points[index++] = new(fx, 0, 0);
            points[index++] = new(fx, 0, gridSize * cellSize);
            points[index++] = new(fx, 0, 0);
        }

        // 横線
        for (int z = 0; z <= gridSize; z++)
        {
            var fz = z * cellSize;
            points[index++] = new(0, 0, fz);
            points[index++] = new(gridSize * cellSize, 0, fz);
            points[index++] = new(0, 0, fz);
        }

        var lr = obj.AddComponent<LineRenderer>();
        lr.positionCount = points.Length;
        lr.SetPositions(points);
        lr.widthMultiplier = lineWidth;
        lr.material = lineMaterial;
        lr.useWorldSpace = false;
        lr.loop = false;
        lr.startColor = lineColor;
        lr.endColor = lineColor;
    }
}