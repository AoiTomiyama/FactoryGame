using UnityEngine;

public class TimeScaleTester : MonoBehaviour
{
    [SerializeField]
    private float timeScale = 1.0f;

    [ContextMenu("Set Time Scale")]
    public void SetTimeScale()
    {
        Time.timeScale = timeScale;
    }
}
