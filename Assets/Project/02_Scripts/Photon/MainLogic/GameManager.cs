    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using Fusion;
    using Fusion.Sockets;
    using UnityEngine.Serialization;

    public class GameManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        #region 변수들
        [SerializeField] private GameState _state = GameState.Waiting;
        
        [Header("연결 컴포넌트들")]
        [SerializeField] private PlayerRegistry _playerRegistry;
        [SerializeField] private PlayerSpawnManager _playerSpawnManager;
        [SerializeField] private PhotonSessionManager _sessionManager;
        [SerializeField] private GameHudPanelController _hudPanelController;
        [SerializeField] private RoundPhysicsMonitor _roundPhysicsMonitor;
        [SerializeField] private MapPhaseController _mapPhaseController;
        [SerializeField] private RoundFlowController _roundFlowController;

        [Header("네트워크 카운트다운")]
        [SerializeField] private NetworkCountdown _countDown; // 카운트다운 컴포넌트

        [Header("게임 시작에 필요한 인원")]
        [SerializeField] private int _gameStartPlayerCnt = 4;
        
        #region 이벤트
        // 플레이어 관련 (참가 / 탈락) 이벤트
        public static event Action<string, int, int> PlayerJoined;          // 플레이어 참가 시 : {플레이어명}입장, {현재 플레이어 수} / {필요 인원 수}
        public static event Action<string> PlayerDead;                // 플레이어 탈락 시
        
        // 라운드 관련 이벤트
        public event Action<string> BeforeStartGame;                 // 게임 시작 전
        public event Action<string> AfterGameStart;                 // 게임 시작 시 (인원 충족 시)
        public event Action<string> RoundReady;                  // 라운드 준비 시
        public event Action<string> RoundStarted;                     // 라운드 시작 시
        public event Action<string> RoundEnded;                     // 라운드 종료 시
        public event Action<string> AfterMapReduce;       // 플레이 구역 축소 동작 후 (완료되면 라운드 준비 시로 돌아감)
        #endregion

        #region 프로퍼티
        public GameState State => _state;
        #endregion
        #endregion

        private NetworkRunner Runner // 세션매니저 컴포넌트가 존재하면 그 안의 runner를 가져오면 됨
        {
            get
            {
                if (_sessionManager != null)
                {
                    return _sessionManager.Runner;
                }
                else
                {
                    return null;
                }
            }
        }
        
        #region 유니티 콜백 메서드
        private void Awake()
        {
            // 전처리기로 에디터 상황에서만 디버그로그 출력
            #if !UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.unityLogger.logEnabled = false;
            #endif
            
            if (_playerRegistry == null)
            {
                _playerRegistry = GetComponent<PlayerRegistry>();
            }

            if (_playerSpawnManager == null)
            {
                _playerSpawnManager = GetComponent<PlayerSpawnManager>();
            }

            if (_sessionManager == null)
            {
                _sessionManager = GetComponent<PhotonSessionManager>();
            }

            if (_hudPanelController == null)
            {
                _hudPanelController = GetComponent<GameHudPanelController>();
            }

            if (_roundPhysicsMonitor == null)
            {
                _roundPhysicsMonitor = GetComponent<RoundPhysicsMonitor>();
            }

            if (_mapPhaseController == null)
            {
                _mapPhaseController = GetComponent<MapPhaseController>();
            }

            if (_roundFlowController == null)
            {
                _roundFlowController = GetComponent<RoundFlowController>();
            }

            _roundFlowController.Initialize(
                _playerRegistry,
                _roundPhysicsMonitor,
                _mapPhaseController,
                _countDown,
                () => Runner);

            // 라운드 흐름 컴포넌트들 이벤트에 구독
            _roundFlowController.StateChanged += nextState => _state = nextState;
            _roundFlowController.AfterGameStart += message => AfterGameStart?.Invoke(message);
            _roundFlowController.RoundReady += message => RoundReady?.Invoke(message);
            _roundFlowController.RoundStarted += message => RoundStarted?.Invoke(message);
            _roundFlowController.RoundEnded += message => RoundEnded?.Invoke(message);
            _roundFlowController.AfterMapReduce += message => AfterMapReduce?.Invoke(message);

            // 세션 매니저 초기화 작업
            _sessionManager.Initialize(
                this,
                sessionLimit => _gameStartPlayerCnt = sessionLimit,
                () => _hudPanelController.HideRoomEntryPanels(),
                () => _hudPanelController.ShowSessionSizeSelection());
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _hudPanelController.ShowAskReturnPanel(!_hudPanelController.IsAskReturnPanelActive);
            }
        }
        #endregion

        #region 플레이어 조인 시
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            _hudPanelController.ShowTopPanel(true);

            if (!runner.IsServer) return;

            _mapPhaseController.SpawnMap(runner);
            
            int currentPlayerCount = _playerRegistry.RegisterPlayerJoined();
            
            string waitingMessage = $"플레이어 대기 중... {currentPlayerCount} / {_gameStartPlayerCnt}";
            BeforeStartGame?.Invoke(waitingMessage);
            _countDown.RpcSetMainStatus(waitingMessage);

            if (currentPlayerCount >= _gameStartPlayerCnt)
            {
                _sessionManager.CloseSessionForGameStart();
            }

            _roundFlowController.TryScheduleGameStart(currentPlayerCount, _gameStartPlayerCnt);

            Debug.Log($"현재 세션 내 인원 : {currentPlayerCount}");

            NetworkObject networkPlayerObject = _playerSpawnManager.SpawnPlayer(
                runner,
                player,
                currentPlayerCount,
                this,
                obj => PlayerJoined?.Invoke("플레이어 입장", currentPlayerCount, _gameStartPlayerCnt));

            _playerRegistry.RegisterSpawned(player, networkPlayerObject);            // 이번에 스폰한 캐릭터를 지금까지 스폰/생존 명단에 추가
        }
        #endregion

        #region 플레이어 떠날 시
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (_playerRegistry.TryGetSpawned(player, out NetworkObject networkObject))
            {
                bool wasAlive = networkObject.TryGetComponent(out PhotonPlayer playerScript) && !playerScript.IsDeadLocal;

                _playerRegistry.MarkPlayerLeft(player, networkObject, wasAlive);
                
                // 세션에서 접속 끊은 오브젝트는 디스폰시켜서 오브젝트 없애기
                runner.Despawn(networkObject);                  
            }
        }
        #endregion
        
        #region 버튼 이벤트
        public void SetSessionLimit(int sessionLimit)
        {
            _sessionManager.SetSessionLimit(sessionLimit);
        }

        public void MakeOrJoinSession()
        {
            _sessionManager.MakeOrJoinSession();
        }

        public void MakeSession()
        {
            _sessionManager.CreateSessionWithCurrentLimit();
        }
        public void ReturnToMainMenu()
        {
            _sessionManager.ReturnToMainMenu();
        }
        #endregion
        
        #region 라운드 흐름 사이클 (준비 -> 경고 -> 시작 -> 정지 검사 -> 외곽 -> 정지 검사 -> 종료 -> 준비)
        public void ApplyOutskirtWarning()
        {
            _roundFlowController.ApplyOutskirtWarning();
        }
        public void BoostAllPlayers()
        {
            _roundFlowController.BoostAllPlayers();
        }
        public void ApplyFallMap()
        {
            _roundFlowController.ApplyFallMap();
        }
        #endregion

        #region 플레이어 사망 처리
        public void EliminatePlayer(PlayerRef playerRef)
        {
            if (_playerRegistry.TryGetSurvivor(playerRef, out NetworkObject playerObject))
            {
                PhotonPlayer photonPlayerScript = playerObject.GetComponent<PhotonPlayer>();
                photonPlayerScript.SetDead(); // 플레이어 스크립트에 사망 처리 신호 보내서 죽음 상태로 전환
                
                _playerRegistry.MarkDead(playerRef, playerObject);
            }
        }
        public static void NotifyPlayerEliminated(string playerName)
        {
            Debug.Log($"탈락 이벤트 호출: {playerName} / 구독자 있음: {PlayerDead != null}");
            PlayerDead?.Invoke(playerName); // 플레이어 사망 시 사망한 플레이어의 닉네임을 모든 구독자에게 보냄
        }
        #endregion

        #region 화살표 Disable All
        public void ShowEachOthersArrow(bool flag)
        {
            _roundFlowController.ShowEachOthersArrow(flag);
        }

        // 모든 플레이어들이 각자 움직일 방향으로 회전하게 만들기
        public void FaceAllPlayersToQueuedDirection()
        {
            _roundFlowController.FaceAllPlayersToQueuedDirection();
        }
        #endregion
        
        #region 메인 화면 귀환 패널 OnOff
        public void ShowReturnPanelDefeat(bool flag)
        {
            _hudPanelController.ShowReturnPanelDefeat(flag);
        }

        public void ShowReturnPanelWin(bool flag)
        {
            _hudPanelController.ShowReturnPanelWin(flag);
        }
        #endregion
        
        #region 기능이 정의되지 않은 콜백 / 스텁들
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            _sessionManager.HandleShutdown(shutdownReason);
        }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            if (runner.IsServer && (_state != GameState.Waiting || _playerRegistry.CurrentPlayerCount >= _gameStartPlayerCnt))
            {
                request.Refuse();
                return;
            }

            request.Accept();
        }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            _sessionManager.OnSessionListUpdated(sessionList);
        }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player){ }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player){ }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            _sessionManager.HandleDisconnectedFromServer(reason);
        }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        #endregion
    }
    // 현재 게임 상태
    public enum GameState
    {
        Waiting,
        RoundReady,
        RoundPlaying,
        GameOver
    }
