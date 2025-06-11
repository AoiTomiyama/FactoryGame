using UnityEngine;

public class GridFieldGenerator : MonoBehaviour
{
    [SerializeField] private GridFieldSO _fieldSo;
    public GameObject cellPrefab;
    public int gridSize = 30;

    private void Start()
    {
        _fieldSo.GridCells = new GameObject[gridSize, gridSize];
        for (int x = 0; x < gridSize; x++)
        {
            var separator = transform.GetChild(x);
            for (int z = 0; z < gridSize; z++)
            {
                var cell = separator.GetChild(z).gameObject;
                _fieldSo.SetNewCell(x, z, cell);
            }
        }
    }
}