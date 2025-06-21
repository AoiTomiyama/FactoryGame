using System;
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
    [Tooltip("繰り返しの回数")] [SerializeField]
    private int octaves = 5;

    [Tooltip("初期周波数")] [SerializeField]
    private float baseFrequency = 0.05f;

    [Tooltip("初期振幅")] [SerializeField]
    private float baseAmplitude = 1f;

    [Tooltip("振幅の減衰")] [SerializeField]
    private float persistence = 0.5f;

    [Tooltip("周波数の増加")] [SerializeField]
    private float lacunarity = 2f;

    [Serializable]
    private struct PropPrefab
    {
        [SerializeField] [Tooltip("プロップのプレハブ")]
        public GameObject prefab;
        
        [SerializeField] [Range(0, 1)] [Tooltip("ノイズ値の閾値")]
        public float threshold;
        
        [Tooltip("ノイズオフセットのランダム値")]
        public Vector3 NoiseOffset { get; set; }
    }

    private void Start()
    {
        GridFieldDatabase.Instance.InitializeCells(gridSize);
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
        // プロップのノイズオフセットをランダムに設定
        for (var index = 0; index < propPrefabs.Length; index++)
        {
            propPrefabs[index].NoiseOffset = new Vector3(Random.Range(0f, 100f), 0, Random.Range(0f, 100f));
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
                    var noiseValue = Fbm(x + p.NoiseOffset.x, z + p.NoiseOffset.z);

                    if (noiseValue <= p.threshold) continue;

                    // ノイズ値が閾値を超えた場合、配置するオブジェクトを確定
                    obj = p.prefab;
                    break;
                }

                // セルの生成
                var tile = Instantiate(obj, separator.transform);
                tile.transform.position = pos;
                tile.name = $"Tile_{x}_{z}";
            }
        }
    }

    /// <summary>
    /// フラクタルブラウン運動によるノイズを生成
    /// </summary>
    private float Fbm(float x, float y)
    {
        var total = 0f;
        var frequency = baseFrequency;
        var amplitude = baseAmplitude;
        var maxValue = 0f;

        for (int i = 0; i < octaves; i++)
        {
            // ノイズ値を計算
            total += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;

            // 最大値を更新
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        // 正規化して返す
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
            points[index++] = new Vector3(fx, 0, 0);
            points[index++] = new Vector3(fx, 0, gridSize * cellSize);
            points[index++] = new Vector3(fx, 0, 0);
        }

        // 横線
        for (int z = 0; z <= gridSize; z++)
        {
            var fz = z * cellSize;
            points[index++] = new Vector3(0, 0, fz);
            points[index++] = new Vector3(gridSize * cellSize, 0, fz);
            points[index++] = new Vector3(0, 0, fz);
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