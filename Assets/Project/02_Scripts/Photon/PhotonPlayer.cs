using System;
using Fusion;
using Unity.Cinemachine;
using UnityEngine;

public class PhotonPlayer : NetworkBehaviour
{
    // 싱글플레이로 치면 Player.cs에 해당하는 스크립트
    
    #region 변수들

    private GameManager _gameMgr;
    private PlayerDeathHandler _deathHandler;
    private PlayerEffectController _effectController;

    [Header("플레이어 정보")]
    [Networked] public string PlayerName { get; set; }
    [Networked] private NetworkBool IsDead { get; set; }

    [Header("힘과 방향")]
    [SerializeField] private float _boostPower;
    [SerializeField] private Vector3 _dir;

    [Header("화살표 / 바디")]
    [SerializeField] private GameObject _arrow;
    [SerializeField] private GameObject _body;
    [SerializeField] private Rigidbody _rb;

    [Header("발사 / 클릭 관련 컴포넌트")]
    private PlayerLauncher _launcher;
    private PlayerAimController _aimController;
    private PlayerAimCalculator _aimCalculator;
    [SerializeField] private Transform _aimPivot;

    [Header("카메라 컴포넌트")]
    private Camera _cam;
    private CinemachineCamera _cineCam;
    private CameraOrbitController _cameraOrbitLauncher;
    #endregion

    #region 죽음 여부 (프로퍼티)
    public bool IsDeadLocal => IsDead;
    #endregion

    #region 콜백 메서드들

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _launcher = GetComponent<PlayerLauncher>();
        _aimController = GetComponent<PlayerAimController>();
        _deathHandler = GetComponent<PlayerDeathHandler>();
        _effectController = GetComponent<PlayerEffectController>();
        _cameraOrbitLauncher = GetComponent<CameraOrbitController>();
        
        // 다소 하드코딩적인 방법. 다만 게임 자체가 턴제라는 점과 많아봐야 4개체 정도만 .Spawn()되므로 지나친 연산부하가 예상되지는 않아서 다음과 같이 작성함
        _cam = FindFirstObjectByType<Camera>();
        _cineCam = FindFirstObjectByType<CinemachineCamera>();
        _aimController.SetCamTarget(_cam, _cineCam, _aimPivot);
        
        _deathHandler.Initialize(this, _gameMgr, _launcher, _body);
        _effectController.Initialize(transform, _launcher);
    }

    public override void Spawned()
    {
        CacheGameManagerIfNeeded();

        if (Object.HasInputAuthority)
        {
            ShowArrow(false);
            SetLocalCameraTargetToPlayer();

            _aimController.AimReleased += SetPowerAndDirection;
            _aimController.enabled = true;
            _cameraOrbitLauncher.enabled = true;
        }
        else
        {
            _aimController.enabled = false;
            _cameraOrbitLauncher.enabled = false;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Object.HasInputAuthority)
        {
            _aimController.AimReleased -= SetPowerAndDirection;
        }
    }

    private void OnCollisionEnter(Collision coll)
    {
        if (!Object.HasStateAuthority) return;
        if (IsDead) return;

        ContactPoint contact = coll.GetContact(0);

        if (coll.gameObject.CompareTag("Player") && coll.gameObject.GetComponent<PhotonPlayer>() != null)
        {
            TryPlayCollisionEffect(contact);
        }

        if (coll.gameObject.CompareTag("DEAD"))
        {
            Debug.Log("DEAD");
            RpcPlayDeadEffect(1, contact.point, contact.normal);
            RpcShowingDustParticle(false);
            DiedOnStateAuthority();
        }
    }

    #endregion

    #region 초기화

    public void Init(GameManager gameMgr)
    {
        _gameMgr = gameMgr;

        if (_deathHandler != null)
        {
            _deathHandler.SetGameManager(gameMgr);
        }
    }

    private void CacheGameManagerIfNeeded()
    {
        if (_gameMgr == null)
        {
            _gameMgr = FindFirstObjectByType<GameManager>();
        }

        if (_deathHandler != null)
        {
            _deathHandler.SetGameManager(_gameMgr);
        }
    }

    #endregion

    #region 조준 / 발사

    public void SetPowerAndDirection(Vector3 dir, float power)
    {
        if (!HasInputAuthority) return;

        RpcSetDirection(dir);
        RpcSetPower(power);
        Debug.Log($"{dir} / {power}");

        RpcSetLaunch(dir, power);
    }

    public void RequestFaceQueuedDirectionFromStateAuthority()
    {
        if (!Object.HasStateAuthority) return;
        if (_launcher == null) return;

        RpcFaceDirection(_launcher.QueuedDirection);
    }

    private void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        if (_rb != null)
        {
            _rb.angularVelocity = Vector3.zero;
            _rb.rotation = targetRotation;
            return;
        }

        transform.rotation = targetRotation;
    }

    #endregion

    #region 죽음 흐름
    public void SetDead()
    {
        if (!Object.HasStateAuthority) return;

        IsDead = true;
    }

    private void DiedOnStateAuthority()
    {
        if (IsDead) return;

        IsDead = true;

        string eliminatedPlayerName = _deathHandler.GetDisplayPlayerName(PlayerName, Object.InputAuthority);

        Debug.Log($"{eliminatedPlayerName}");
        _deathHandler.EliminatePlayer(Object.InputAuthority, eliminatedPlayerName);
        RpcSetDead();
        RpcNotifyPlayerEliminated(eliminatedPlayerName);
    }

    #endregion

    #region 화살표 변경 요청

    public void ShowArrow(bool flag)
    {
        _arrow.gameObject.SetActive(flag);
    }

    public void RequestHideArrowFromStateAuthority(bool flag)
    {
        if (!Object.HasStateAuthority) return;

        RpcShowArrow(flag);
    }

    public void RequestShowReturnPanelWinFromStateAuthority()
    {
        if (!Object.HasStateAuthority) return;

        RpcShowReturnPanelWin();
    }

    #endregion

    #region 카메라

    private void SetLocalCameraTargetToPlayer()
    {
        CinemachineCamera localCineCam = FindFirstObjectByType<CinemachineCamera>();

        if (localCineCam != null)
        {
            localCineCam.Target.TrackingTarget = transform;
        }
    }

    #endregion

    #region 이펙트 / 오디오

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RpcShowingDustParticle(bool flag)
    {
        _effectController.SpawnDustParticle();
    }

    private void TryPlayCollisionEffect(ContactPoint contact)
    {
        if (!_effectController.CanPlayCollisionEffect()) return;

        RpcPlayHitEffect(contact.point, contact.normal);
    }

    #endregion

    #region RPC 함수들

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RpcSetPower(float power) // 힘 저장
    {
        _boostPower = power;
        Debug.Log($"{_boostPower} 힘 저장");
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RpcSetDirection(Vector3 direction) // 방향 저장
    {
        _dir = direction;
        Debug.Log($"{_dir} 방향 저장");
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RpcSetLaunch(Vector3 dir, float power) // PlayerLauncher.cs의 QueseLaunch 실행 여부 RPC 전송
    {
        _launcher.QueueLaunch(dir, power);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcSetDead() // 죽음 상태로 전환
    {
        _deathHandler.ApplyDeadState(Object.HasInputAuthority);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcNotifyPlayerEliminated(string eliminatedPlayerName) // 죽음 상태를 호스트에게 전달
    {
        _deathHandler.NotifyPlayerEliminated(eliminatedPlayerName);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcShowArrow(bool flag) // 각자의 화살표 보여주기
    {
        if (!Object.HasInputAuthority) return;

        ShowArrow(flag);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcFaceDirection(Vector3 direction) // 세팅된 방향으로 전환
    {
        FaceDirection(direction);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcShowReturnPanelWin()
    {
        if (!Object.HasInputAuthority) return;

        _deathHandler.ShowLocalReturnPanelWin(true);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcPlayHitEffect(Vector3 position, Vector3 normal) // RPC로 사운드 생성
    {
        _effectController.PlayHitEffect(position, normal);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcPlayDeadEffect(int effectIndex, Vector3 position, Vector3 normal) // RPC로 이펙트 발생
    {
        _effectController.PlayDeadEffect(effectIndex, position, normal);
    }

    #endregion
}
