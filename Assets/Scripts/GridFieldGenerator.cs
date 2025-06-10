using System.Threading.Tasks;
using UnityEngine;

public class GridFieldGenerator : MonoBehaviour
{
    public GameObject cellPrefab;
    public int gridSize = 30;
    public GameObject[,] Grid { get; set; }
}
