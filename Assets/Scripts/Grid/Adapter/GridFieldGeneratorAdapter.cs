using System;
#if UNITY_EDITOR
using System.Security.Cryptography;
using UnityEditor;
using Random = UnityEngine.Random;
#endif
using UnityEngine;
using System.Linq;

public class GridFieldGeneratorAdapter : MonoBehaviour
{
    [SerializeField] private GridFieldDatabaseAdapter targetField;
    public GridFieldDatabaseAdapter TargetField => targetField;
    [SerializeField] private SingularityStartDefinition initialLayout;
    [SerializeField] private SkillTreeDefinition skillTree;
    [SerializeField] private EconomyPanelAdapter economyPanel;
    [SerializeField] private ResourceDefinition resources;
    public ConstructionStockApplication ConstructionStock { get; private set; }
    public SkillTreeApplication Skills { get; private set; }
    public CurrencyWalletApplication Wallet { get; private set; }
    public DeliveryProgressApplication Deliveries { get; private set; }
    public CurrencyPriceCatalogDomain Prices { get; private set; }

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

    public bool IsChoosingStart { get; private set; }
    public bool HasStarted { get; private set; }
    public System.Collections.Generic.IReadOnlyList<Vector2Int> StartSites { get; private set; } = Array.Empty<Vector2Int>();
    public int SelectedStartSite { get; private set; }
    public Vector2Int StartAnchor { get; private set; }
    private StartSiteSelectionAdapter _selection;
    public Material GridLineMaterial => lineMaterial;
    public System.Collections.Generic.IReadOnlyList<StartSiteRulesDomain.Candidate> StartCandidates { get; private set; } = Array.Empty<StartSiteRulesDomain.Candidate>();

    private void Start()
    {
        if (initialLayout == null) { GridFieldDatabaseAdapter.Instance.InitializeCells(gridSize); return; }
        PrepareStartSites(GridFieldDatabaseAdapter.Instance.transform);
        IsChoosingStart = true;
        _selection = gameObject.AddComponent<StartSiteSelectionAdapter>();
        _selection.Begin(this);
    }
    public bool SelectStartSite(int index)
    {
        if (!IsChoosingStart || index<0 || index>=StartSites.Count) return false;
        SelectedStartSite=index;
        _selection.RefreshSelection();
        return true;
    }
    public bool SelectStartSiteAt(Vector3 point)
    {
        for(var i=0;i<StartSites.Count;i++)
            if (initialLayout.SingularityPrefab.Footprint.Coordinates(StartSites[i].x,StartSites[i].y,0)
                .Contains((Mathf.RoundToInt(point.x),Mathf.RoundToInt(point.z)))) return SelectStartSite(i);
        return false;
    }
    public bool ConfirmStartSite()
    {
        if (!IsChoosingStart || HasStarted || StartSites.Count==0) return false;
        StartAnchor=StartSites[SelectedStartSite];
        initialLayout.ValidateAt(gridSize,StartAnchor);
        var grid=GridFieldDatabaseAdapter.Instance;
        PlaceInitialCells(grid.transform,StartAnchor);
        grid.InitializeCells(gridSize);
        Prices = initialLayout.CreateCatalog();
        Wallet = new CurrencyWalletApplication(Prices.InitialBalance);
        if (resources == null) throw new InvalidOperationException("Assign registered resources for construction stock.");
        ConstructionStock = new ConstructionStockApplication(resources.GetAllResources().Select(r => r.ResourceTypeDomain));
        Deliveries = new DeliveryProgressApplication(initialLayout.CreateDeliveryCatalog());
        if (skillTree == null) throw new InvalidOperationException("Skill tree definition must be assigned.");
        Skills = new SkillTreeApplication(skillTree.CreateCatalog(), Wallet, grid.StatEffects);
        FindAnyObjectByType<CellPlacerAdapter>()?.Configure(Wallet, Prices, ConstructionStock, resources);
        var singularities = grid.Owners.OfType<SingularityCellAdapter>().ToArray();
        if (singularities.Length != 1) throw new InvalidOperationException("Initial field requires exactly one singularity.");
        var singularity = singularities[0];
        singularity.Configure(Deliveries);
        // 登録と共有残高の構成後に接続を開始する。
        singularity.InitializeSystem();
        foreach (var shipping in grid.Owners.OfType<ShippingCellAdapter>())
        { shipping.Configure(Wallet, Prices); shipping.InitializeSystem(); }
        foreach (var depot in grid.Owners.OfType<ConstructionDepotCellAdapter>())
        { depot.Configure(ConstructionStock); depot.InitializeSystem(); }
        FindAnyObjectByType<CameraControllerAdapter>()?.FocusOn(singularity.WorldCenter);
        _selection.Finish();
        economyPanel.Configure(Wallet, Skills, FindAnyObjectByType<CellStatusViewAdapter>(), FindAnyObjectByType<CellPlacerAdapter>(), Deliveries);

        IsChoosingStart=false; HasStarted=true;
        return true;
    }
    public Vector2Int ShippingPreviewPosition(Vector2Int anchor)
    {
        var point=initialLayout.ShippingCell(anchor);return new Vector2Int(point.x,point.z);
    }

    private void PlaceInitialCells(Transform parent,Vector2Int anchor)
    {
        var occupied=initialLayout.SingularityPrefab.Footprint.Coordinates(anchor.x,anchor.y,0).ToHashSet();
        var sale=initialLayout.ShippingCell(anchor);
        var cells=parent.GetComponentsInChildren<CellBaseAdapter>().ToDictionary(c=>(Mathf.RoundToInt(c.transform.position.x),Mathf.RoundToInt(c.transform.position.z)));
        if(occupied.Any(p=>!cells.TryGetValue(p,out var c) || c is not EmptyCellAdapter && c is not ResourceCellAdapter) ||
            !cells.TryGetValue(sale,out var target) || target is not EmptyCellAdapter)
            throw new InvalidOperationException("Selected start site is no longer available.");
        // 初期配置で作るのは、この2設備だけ。範囲外の地形と資源は変更しない。
        foreach(var point in occupied.Append(sale)) RetireInitialCell(cells[point]);
        CreateInitialCell(initialLayout.SingularityPrefab,parent,anchor.x,anchor.y);
        CreateInitialCell(initialLayout.ShippingPrefab,parent,sale.x,sale.z);
    }
    private static CellBaseAdapter CreateInitialCell(CellBaseAdapter prefab, Transform parent, int x, int z)
    {
        CellBaseAdapter cell;
#if UNITY_EDITOR
        if (!Application.isPlaying) cell = ((GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, parent)).GetComponent<CellBaseAdapter>();
        else
#endif
            cell = Instantiate(prefab, parent);
        cell.transform.SetPositionAndRotation(new Vector3(x, 0, z), Quaternion.identity);
        cell.name = $"Tile_{x}_{z}";
        return cell;
    }
    private static void RetireInitialCell(CellBaseAdapter cell)
    {
        if (cell is ConnectableCellBaseAdapter connected) connected.OnDisconnect();
        cell.gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(cell.gameObject); else DestroyImmediate(cell.gameObject);
    }

    /// <summary>保存済み地形を読み取る。候補を成立させるための配置・削除は行わない。</summary>
    public void PrepareStartSites(Transform parent)
    {
        initialLayout.Validate(gridSize);
        var map=new StartSiteRulesDomain.Terrain[gridSize,gridSize];
        foreach(var cell in parent.GetComponentsInChildren<CellBaseAdapter>())
        {
            var p=Vector3Int.RoundToInt(cell.transform.position);
            if(p.x<0 || p.z<0 || p.x>=gridSize || p.z>=gridSize) continue;
            if(cell is EmptyCellAdapter) map[p.x,p.z]=new StartSiteRulesDomain.Terrain(true);
            else if(cell is ResourceCellAdapter raw) map[p.x,p.z]=new StartSiteRulesDomain.Terrain(false,raw.ResourceTypeDomain);
        }
        var prices=initialLayout.CreateCatalog();
        prices.TryGetPlacementAmount(CellTypeDomain.ExtractorWood,1,out var extractor);
        prices.TryGetPlacementAmount(CellTypeDomain.Conveyor,1,out var belt);
        var origin=initialLayout.ShippingCell(Vector2Int.zero);
        StartCandidates=StartSiteRulesDomain.Candidates(map,seedValue,initialLayout.SingularityPrefab.Footprint,origin,prices.InitialBalance,extractor,belt);
        StartSites=Array.AsReadOnly(StartCandidates.Select(p=>new Vector2Int(p.X,p.Z)).ToArray());
    }

#if UNITY_EDITOR
    // 生成処理は既存シーンのシリアライズ参照を保つため同じコンポーネントに置き、
    // プレイヤーへは初期化処理だけを含める。
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
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        if (parent.TryGetComponent<GridFieldDatabaseAdapter>(out var field)) field.InvalidateGeneratedField();
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
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        if (gridSize <= 0 || emptyCellPrefab == null) throw new InvalidOperationException("Assign terrain size and empty prefab.");
        var candidates = (propPrefabs ?? Array.Empty<PropPrefab>()).OrderBy(p =>
            p.prefab != null && p.prefab.TryGetComponent<ResourceCellAdapter>(out var resource) ? (int)resource.ResourceTypeDomain : -1).ToArray();
        var identities = new System.Collections.Generic.HashSet<ResourceTypeDomain>();
        for (var i = 0; i < candidates.Length; i++)
        {
            if (candidates[i].prefab == null || !candidates[i].prefab.TryGetComponent<ResourceCellAdapter>(out var resource) ||
                !identities.Add(resource.ResourceTypeDomain) || (float.IsNaN(candidates[i].threshold) || float.IsInfinity(candidates[i].threshold)))
                throw new InvalidOperationException("Generation requires unique resource prefabs and finite thresholds.");
            // 保存済みの資源種を使い、Editor再起動や候補の並べ替えで配置を変えない。
            candidates[i].NoiseOffset = GetHashedVector(unchecked(seedValue + (int)resource.ResourceTypeDomain * 73856093));
        }
        var octaveOffsets = Enumerable.Range(0, octaves).Select(i => GetHashedVector(unchecked(seedValue + i))).ToArray();

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
                var best = 0f;
                foreach (var p in candidates)
                {
                    // 全候補を比較し、配列の先頭だけが資源分布を占める偏りを防ぐ。
                    var score = Fbm(x + p.NoiseOffset.x, z + p.NoiseOffset.y, octaveOffsets) - p.threshold;
                    if (score <= best) continue;
                    best = score;
                    obj = p.prefab;
                }


                // セルの生成
                var tile = (GameObject)PrefabUtility.InstantiatePrefab(obj, separator.transform);
                tile.transform.position = pos;
                tile.name = $"Tile_{x}_{z}";
            }
        }
        if (parent.TryGetComponent<GridFieldDatabaseAdapter>(out var field)) field.RecordGeneratedField(gridSize, seedValue);
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
        // 値が大きすぎるとノイズの計算が不安定になるので、-10,000 ～ 10,000 の範囲に正規化
        const int NormalizeFactor = 10000;

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
    private float Fbm(float x, float y, Vector2Int[] offsets)
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
            var offset = offsets[i];
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
#endif
}
