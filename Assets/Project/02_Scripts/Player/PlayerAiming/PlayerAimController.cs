using System;
using Unity.Cinemachine;
using UnityEngine;

// 플레이어 클릭 드래그는 조준으로, 플레이어 밖 드래그는 카메라 회전으로 연결
[RequireComponent(typeof(CameraOrbitController))]
[RequireComponent(typeof(PlayerAimInputHandler))]
[RequireComponent(typeof(PlayerAimHitDetector))]
public class PlayerAimController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera _targetCamera;

    [SerializeField] private Transform _aimPivot;

    [SerializeField] private Transform _orbitCameraTransform;

    [SerializeField] private float _playerRaycastDistance = 1000f;

    [SerializeField] private LayerMask _aimRaycastMask = ~0;

    [Header("Aim")]
    [SerializeField] private float _minDragDistance = 15f;

    [SerializeField] private float _maxDragDistance = 350f;

    [SerializeField] private float _defaultAimPower = 0.1f;

    private CameraOrbitController _cameraOrbitController;

    private PlayerAimInputHandler _inputHandler;

    private PlayerAimHitDetector _hitDetector;

    private PlayerAimCalculator _aimCalculator;

    private Vector2 _pressScreenPosition;

    private DragMode _dragMode;

    public event Action<float> AimChanged;

    public event Action<Vector3, float> AimReleased;

    public bool IsAiming => _dragMode == DragMode.Aim;

    public Vector3 AimDirection => _aimCalculator.AimDirection;

    public float AimPower => _aimCalculator.AimPower;

    private enum DragMode
    {
        None,

        Aim,

        Orbit
    }

    private void Awake()
    {
        _cameraOrbitController = GetComponent<CameraOrbitController>();
        _inputHandler = GetComponent<PlayerAimInputHandler>();
        _hitDetector = GetComponent<PlayerAimHitDetector>();
        
        CreateAimCalculator();
        ConfigureHitDetector();
        ResetAimToDefault();
    }

    private void OnEnable()
    {
        if (_inputHandler == null)
        {
            return;
        }

        _inputHandler.PressStarted += BeginDrag;
        _inputHandler.PressCanceled += EndDrag;
    }

    private void OnDisable()
    {
        if (_inputHandler != null)
        {
            _inputHandler.PressStarted -= BeginDrag;
            _inputHandler.PressCanceled -= EndDrag;
        }

        _dragMode = DragMode.None;
    }

    private void Update()
    {
        if (_dragMode == DragMode.Aim)
        {
            UpdateAim(_inputHandler.ReadPointerPosition());
            return;
        }

        if (_dragMode == DragMode.Orbit)
        {
            _cameraOrbitController.UpdateOrbit(
                _inputHandler.ReadPointerPosition(),
                _orbitCameraTransform,
                _aimPivot.position
            );
        }
    }

    private void BeginDrag()
    {
        _pressScreenPosition = _inputHandler.ReadPointerPosition();

        if (!_hitDetector.IsPointerOverThisPlayer(_pressScreenPosition))
        {
            _dragMode = DragMode.Orbit;
            _cameraOrbitController.BeginOrbit(_pressScreenPosition);
            return;
        }

        _dragMode = DragMode.Aim;
        UpdateAim(_pressScreenPosition);
    }

    private void EndDrag()
    {
        if (_dragMode != DragMode.Aim)
        {
            _dragMode = DragMode.None;
            return;
        }

        UpdateAim(_inputHandler.ReadPointerPosition());
        _dragMode = DragMode.None;
        AimReleased?.Invoke(AimDirection, AimPower);
    }

    private void UpdateAim(Vector2 currentScreenPosition)
    {
        _aimCalculator.UpdateAim(_pressScreenPosition, currentScreenPosition);
        AimChanged?.Invoke(AimPower);
    }

    public void ResetAimToDefault()
    {
        _aimCalculator.ResetToDefault();
        AimChanged?.Invoke(AimPower);
    }

    public void SetCamTarget(Camera cam, CinemachineCamera cineCam, Transform pivot = null)
    {
        // 네트워크 스폰 이후 로컬 카메라와 플레이어 Pivot 참조를 다시 연결
        _targetCamera = cam;
        _orbitCameraTransform = cineCam.transform;

        if (pivot != null)
        {
            _aimPivot = pivot;
        }

        CreateAimCalculator();
        ConfigureHitDetector();
        ResetAimToDefault();
    }

    private void CreateAimCalculator()
    {
        _aimCalculator = new PlayerAimCalculator(
            _targetCamera,
            _aimPivot,
            _minDragDistance,
            _maxDragDistance,
            _defaultAimPower
        );
    }

    private void ConfigureHitDetector()
    {
        if (_hitDetector == null)
        {
            return;
        }

        _hitDetector.Configure(
            this,
            _targetCamera,
            _playerRaycastDistance,
            _aimRaycastMask
        );
    }
}
