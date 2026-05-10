using UnityEngine;

// 플레이어 밖 드래그로 카메라를 플레이어 주변에서 좌우 회전
public class CameraOrbitController : MonoBehaviour
{
    [Header("Orbit")]
    [SerializeField] private float _orbitSensitivity = 0.2f;

    private Vector2 _previousScreenPosition;

    public void BeginOrbit(Vector2 screenPosition)
    {
        _previousScreenPosition = screenPosition;
    }

    public void UpdateOrbit(Vector2 currentScreenPosition, Transform orbitCameraTransform, Vector3 orbitCenterPosition)
    {
        Vector2 pointerDelta = currentScreenPosition - _previousScreenPosition;

        _previousScreenPosition = currentScreenPosition;

        float yaw = pointerDelta.x * _orbitSensitivity;

        orbitCameraTransform.RotateAround(orbitCenterPosition, Vector3.up, yaw);
    }
}
