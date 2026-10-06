using UnityEngine;

public class UILookAtCameraAdapter : MonoBehaviour
{
    private Camera _camera;

    private void Start() => _camera = Camera.main;

    private void Update()
    {
        if (_camera == null) return;

        transform.forward = _camera.transform.forward;
    }
}