using UnityEngine;

// 화면 클릭 위치가 이 플레이어를 가리키는지 Raycast로 판정
public class PlayerAimHitDetector : MonoBehaviour
{
    private PlayerAimController _owner;

    private Camera _targetCamera;

    private float _playerRaycastDistance = 1000f;

    private LayerMask _aimRaycastMask = ~0;

    public void Configure(
        PlayerAimController owner,
        Camera targetCamera,
        float playerRaycastDistance,
        LayerMask aimRaycastMask
    )
    {
        this._owner = owner;
        this._targetCamera = targetCamera;
        this._playerRaycastDistance = playerRaycastDistance;
        this._aimRaycastMask = aimRaycastMask;
    }

    public bool IsPointerOverThisPlayer(Vector2 screenPosition)
    {
        if (_owner == null || _targetCamera == null)
        {
            return false;
        }

        Ray ray = _targetCamera.ScreenPointToRay(screenPosition);
        return IsRaycastHitThisPlayer(ray);
    }

    private bool IsRaycastHitThisPlayer(Ray ray)
    {
        // 여러 Collider가 겹칠 수 있으므로 모든 충돌 결과에서 owner를 찾음
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            _playerRaycastDistance,
            _aimRaycastMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider.GetComponentInParent<PlayerAimController>() == _owner)
            {
                return true;
            }
        }

        return false;
    }
}
