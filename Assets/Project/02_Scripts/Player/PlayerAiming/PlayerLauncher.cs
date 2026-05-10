using UnityEngine;

public class PlayerLauncher : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerAimController _aimController;

    [SerializeField] private Rigidbody _targetRigidbody;

    [Header("Launch")]
    [SerializeField] private float _maxLaunchForce = 25f;

    // 조준 종료 시 저장한 값. 라운드 발사 타이밍에 TriggerLaunch에서 사용
    private Vector3 _queuedDirection = Vector3.forward;

    private float _queuedPower;

    private bool _hasQueuedLaunch;

    public bool HasQueuedLaunch => _hasQueuedLaunch;

    public Vector3 QueuedDirection => _queuedDirection;

    public float QueuedPower => _queuedPower;

    private void Reset()
    {
        _aimController = GetComponent<PlayerAimController>();
        _targetRigidbody = GetComponent<Rigidbody>();
    }

    private void Awake()
    {
        if (_aimController == null)
        {
            _aimController = GetComponent<PlayerAimController>();
        }

        if (_targetRigidbody == null)
        {
            _targetRigidbody = GetComponent<Rigidbody>();
        }
    }

    private void OnEnable()
    {
        if (_aimController == null)
        {
            return;
        }

        _aimController.AimReleased += QueueLaunch;
    }

    private void OnDisable()
    {
        if (_aimController == null)
        {
            return;
        }

        _aimController.AimReleased -= QueueLaunch;
    }

    public void TriggerLaunch()
    {
        if (_targetRigidbody == null)
        {
            return;
        }

        Vector3 launchDirection = GetLaunchDirection();
        float launchPower = GetLaunchPower();

        float launchForce = _maxLaunchForce * launchPower;

        _targetRigidbody.AddForce(launchDirection * launchForce, ForceMode.Impulse);

        _hasQueuedLaunch = false;
        _aimController.ResetAimToDefault();
    }

    public void QueueLaunch(Vector3 direction, float power)
    {
        _queuedDirection = direction.normalized;

        _queuedPower = Mathf.Clamp01(power);

        _hasQueuedLaunch = true;
    }

    private Vector3 GetLaunchDirection()
    {
        Vector3 launchDirection = _hasQueuedLaunch
            ? _queuedDirection
            : _aimController.AimDirection;

        return launchDirection.normalized;
    }

    private float GetLaunchPower()
    {
        return _hasQueuedLaunch
            ? _queuedPower
            : _aimController.AimPower;
    }
}
