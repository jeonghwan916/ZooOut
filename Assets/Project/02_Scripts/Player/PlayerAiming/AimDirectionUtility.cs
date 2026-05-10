using UnityEngine;

// 화면 기준 드래그 방향을 월드 기준 이동 방향으로 변환
public static class AimDirectionUtility
{
    public static Vector3 ScreenDirectionToWorldDirection(
        Vector2 screenDirection,
        Camera targetCamera,
        Vector3 fallbackDirection
    )
    {
        if (screenDirection.sqrMagnitude <= 0f)
        {
            return fallbackDirection;
        }
        
        // 카메라 방향을 바닥 평면에 투영해 화면 입력과 월드 방향을 맞춤
        Vector3 cameraForward = Vector3.ProjectOnPlane(
            targetCamera.transform.forward,
            Vector3.up
        ).normalized;

        Vector3 cameraRight = Vector3.ProjectOnPlane(
            targetCamera.transform.right,
            Vector3.up
        ).normalized;

        Vector3 worldDirection =
            cameraRight * screenDirection.x +
            cameraForward * screenDirection.y;

        return worldDirection.sqrMagnitude > 0f
            ? worldDirection.normalized
            : fallbackDirection;
    }
}
