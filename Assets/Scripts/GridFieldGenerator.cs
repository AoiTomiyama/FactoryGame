using UnityEngine;

public class GridFieldGenerator : MonoBehaviour
{
    [SerializeField] private GridFieldDatabase fieldDatabase;
    [SerializeField] private float lineWidth = 0.1f;
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color lineColor;
    public GameObject cellPrefab;
    public int gridSize = 30;
    

    private void Start()
    {
        fieldDatabase.GridCells = new CellBase[gridSize, gridSize];
        for (int x = 0; x < gridSize; x++)
        {
            var separator = transform.GetChild(x);
            for (int z = 0; z < gridSize; z++)
            {
                var cell = separator.GetChild(z).gameObject;
                fieldDatabase.SetNewCell(x, z, cell);
            }
        }
        GenerateGrid();
    }
    void GenerateGrid()
    {
        var cellSize = cellPrefab.transform.localScale.x;
        var numLines = (gridSize + 1) + (gridSize + 1);
        var numPoints = numLines * 3;
        
        var obj = new GameObject("GridLines");
        obj.transform.SetParent(transform);
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