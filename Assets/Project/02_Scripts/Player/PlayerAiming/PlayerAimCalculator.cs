using UnityEngine;

// 드래그 거리로 조준 방향과 힘을 계산
public class PlayerAimCalculator
{
    private readonly Camera _targetCamera;
    private readonly Transform _aimPivot;
    private readonly float _minDragDistance;
    private readonly float _maxDragDistance;
    private readonly float _defaultAimPower;

    public Vector3 AimDirection { get; private set; } = Vector3.forward;
    public float AimPower { get; private set; }

    public PlayerAimCalculator(
        Camera targetCamera,
        Transform aimPivot,
        float minDragDistance,
        float maxDragDistance,
        float defaultAimPower
    )
    {
        this._targetCamera = targetCamera;
        this._aimPivot = aimPivot;
        this._minDragDistance = minDragDistance;
        this._maxDragDistance = maxDragDistance;
        this._defaultAimPower = defaultAimPower;
    }

    public void UpdateAim(Vector2 pressScreenPosition, Vector2 currentScreenPosition)
    {
        Vector2 drag = currentScreenPosition - pressScreenPosition;

        if (drag.sqrMagnitude < _minDragDistance * _minDragDistance)
        {
            AimPower = Mathf.Clamp01(_defaultAimPower);
            return;
        }

        // 새총처럼 당긴 방향의 반대로 조준한다.
        Vector2 screenDirection = -drag;

        AimDirection = AimDirectionUtility.ScreenDirectionToWorldDirection(
            screenDirection,
            _targetCamera,
            AimDirection
        );

        AimPower = Mathf.Clamp01(drag.magnitude / _maxDragDistance);
        RotateAimPivot();
    }

    // aimPivot이 바라보는 방향을 기본 조준 방향으로 사용
    public void ResetToDefault()
    {
        AimDirection = Vector3.ProjectOnPlane(_aimPivot.forward, Vector3.up).normalized;
        AimPower = Mathf.Clamp01(_defaultAimPower);
    }

    private void RotateAimPivot()
    {
        Quaternion targetRotation = Quaternion.LookRotation(AimDirection, Vector3.up);
        _aimPivot.rotation = targetRotation;
    }
}
