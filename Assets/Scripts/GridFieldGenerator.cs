using UnityEngine;

public class GridFieldGenerator : MonoBehaviour
{
    [SerializeField] private GridFieldDatabase fieldDatabase;
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
    }
}