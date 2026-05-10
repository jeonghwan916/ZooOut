using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

public class PhotonSessionManager : MonoBehaviour
{
    [Header("방 이름")]
    [SerializeField] private string _sessionName = "Room";
    [SerializeField] private int _roomNum = 1;
    [SerializeField] private int _sessionLimit = 4;
    [SerializeField] private int _photonCcuLimit = 20;

    private const string SessionLimitPropertyKey = "sessionLimit";
    private const string SessionInGamePropertyKey = "inGame";
    private const int SessionOpenValue = 0;
    private const int SessionInGameValue = 1;
    private const int MinRandomRoomNumber = 100000;
    private const int MaxRandomRoomNumber = 1000000;

    private readonly List<SessionInfo> _cachedSessionList = new List<SessionInfo>();
    private TaskCompletionSource<List<SessionInfo>> _sessionListCompletionSource;
    
    private bool _isMatchmakingRequestInProgress; // 매치메이킹 요청 중인가?
    private bool _isSessionLobbyJoined;           // 세션에 접속중인가?
    private bool _isReturningToMainMenu;          // 메인메뉴로 돌아왔는가?
    
    private NetworkRunner _runner;
    public NetworkRunner Runner => _runner;
    
    private INetworkRunnerCallbacks _callbackTarget;
    private Action<int> _gameStartPlayerCountChanged;
    private Action _sessionEntered;
    private Action _sessionSelectionRequired;

    // 초기화 함수 - Codex 도움 받음
    public void Initialize(
        INetworkRunnerCallbacks callbackTarget,
        Action<int> gameStartPlayerCountChanged,
        Action sessionEntered,
        Action sessionSelectionRequired)
    {
        _callbackTarget = callbackTarget;
        _gameStartPlayerCountChanged = gameStartPlayerCountChanged;
        _sessionEntered = sessionEntered;
        _sessionSelectionRequired = sessionSelectionRequired;
    }

    // 세션 인원 제환
    public void SetSessionLimit(int sessionLimit)
    {
        _sessionLimit = sessionLimit;
    }

    // 게임 스타트하면 세션 닫음
    public void CloseSessionForGameStart()
    {
        if (_runner == null || !_runner.IsServer || !_runner.SessionInfo.IsValid)
        {
            return;
        }

        _runner.SessionInfo.IsOpen = false;
        _runner.SessionInfo.IsVisible = false;

        bool propertyUpdated = _runner.SessionInfo.UpdateCustomProperties(new Dictionary<string, SessionProperty>()
        {
            { SessionInGamePropertyKey, SessionInGameValue }
        });

        Debug.Log($"게임 시작으로 세션 입장을 닫았습니다 : {_runner.SessionInfo.Name}");
    }

    // 비동기 대기, 세션을 만들거나 이미 진행중인 세션에 참갛하기
    public async void MakeOrJoinSession()
    {
        // 매치메이킹이 진행 중이거나 or 이미 세션에 접속된 상태라면...
        if (_isMatchmakingRequestInProgress || (_runner != null && _runner.IsInSession))
        {
            return;
        }

        _isMatchmakingRequestInProgress = true;
        List<SessionInfo> sessionList = await GetSessionListFromLobby(); // 로비로부터 세션 리스트들 받아오기
        if (sessionList == null) // 세션이 하나도 없다면
        {
            _isMatchmakingRequestInProgress = false; // 매치메이킹 종료
            return;
        }

        int currentCcu = GetCurrentCcu(sessionList);
        Debug.Log($"현재 접속 중인 CCU: {currentCcu} / {_photonCcuLimit}");

        SessionInfo joinableSession = GetJoinableSession(sessionList); // 세션이 있다면 접속 가능한 세션 찾기
        if (joinableSession == null) // 접속 가능한 세션이 하나도 없다면 (모두 풀방이거나 할때)
        {
            _isMatchmakingRequestInProgress = false; // 매치메이킹 종료
            _sessionSelectionRequired?.Invoke();
            return;
        }

        // 접속한 방의 세션 리미트 받아오기
        int joinedSessionLimit = GetSessionLimit(joinableSession);
        Debug.Log($"입장할 방의 sessionLimit: {joinedSessionLimit}");
        Debug.Log($"Client 입장 요청 후 예상 CCU: {currentCcu + 1} / {_photonCcuLimit}");
        _gameStartPlayerCountChanged?.Invoke(joinedSessionLimit);

        // 게임 모드는 클라이언트 모드로 시작하기
        StartGameResult startGameResult = await StartGame(GameMode.Client, joinableSession.Name);
        if (!startGameResult.Ok)
        {
            Debug.LogError($"Client 방 입장 실패: {startGameResult.ShutdownReason} / {startGameResult.ErrorMessage}");
            _isMatchmakingRequestInProgress = false;

            if (startGameResult.ShutdownReason == ShutdownReason.GameIsFull)
            {
                _sessionSelectionRequired?.Invoke();
            }

            return;
        }

        _isMatchmakingRequestInProgress = false; // 매치메이킹 종료
        _sessionEntered?.Invoke();
    }

    // 게임매니저에서 설정된 세션 리밋대로 세션 만들기
    public async void CreateSessionWithCurrentLimit()
    {
        if (_isMatchmakingRequestInProgress || (_runner != null && _runner.IsInSession))
        {
            return;
        }

        _isMatchmakingRequestInProgress = true;
        List<SessionInfo> sessionList = await GetSessionListFromLobby();
        if (sessionList == null)
        {
            _isMatchmakingRequestInProgress = false;
            return;
        }

        int currentCcu = GetCurrentCcu(sessionList);
        Debug.Log($"현재 접속 중인 CCU: {currentCcu} / {_photonCcuLimit}");

        if (!CanCreateSessionWithinCcuLimit(currentCcu))
        {
            Debug.Log($"새 세션 생성 거부: 현재 CCU {currentCcu} + 요청 sessionLimit {_sessionLimit} > Photon CCU 한도 {_photonCcuLimit}");
            _isMatchmakingRequestInProgress = false;
            return;
        }

        string randomSessionName = GenerateRandomSessionName(sessionList);
        Debug.Log($"홋트 모드로 새 방 생성: {randomSessionName}, 세션 리미트: {_sessionLimit}");
        Debug.Log($"호스트 생성 요청 후 예상 CCU 예약치: {currentCcu + _sessionLimit} / {_photonCcuLimit}");
        _gameStartPlayerCountChanged?.Invoke(_sessionLimit);

        StartGameResult startGameResult = await StartGame(GameMode.Host, randomSessionName);
        if (!startGameResult.Ok)
        {
            Debug.LogError($"호스트 방 생성 실패: {startGameResult.ShutdownReason} / {startGameResult.ErrorMessage}");
            _isMatchmakingRequestInProgress = false;
            return;
        }

        _isMatchmakingRequestInProgress = false;
        _sessionEntered?.Invoke();
    }
    

    // 세션 리스트 업데이트
    public void OnSessionListUpdated(List<SessionInfo> sessionList)
    {
        _cachedSessionList.Clear();
        _cachedSessionList.AddRange(sessionList);
        Debug.Log($"세션 목록 갱신 기준 현재 접속 중인 CCU: {GetCurrentCcu(_cachedSessionList)} / {_photonCcuLimit}");
        _sessionListCompletionSource?.TrySetResult(new List<SessionInfo>(sessionList));
    }

    // 외부 요인으로 인해 방이 강제로 터질때
    public void HandleShutdown(ShutdownReason shutdownReason)
    {
        Debug.Log($"Photon 세션 종료 감지: {shutdownReason}");
        if (shutdownReason == ShutdownReason.GameIsFull)
        {
            _runner = null;
            _isMatchmakingRequestInProgress = false;
            _isSessionLobbyJoined = false;
            _sessionSelectionRequired?.Invoke();
            return;
        }

        ForceReturnToStartScreen();
    }

    // 서버 접속이 끊길때 대비
    public void HandleDisconnectedFromServer(NetDisconnectReason reason)
    {
        Debug.Log($"Photon 서버 연결 끊김: {reason}");
        ForceReturnToStartScreen();
    }

    public async void ReturnToMainMenu()
    {
        if (_isReturningToMainMenu)
        {
            return;
        }

        _isReturningToMainMenu = true;

        if (_runner != null)
        {
            Debug.Log("현재 Photon 세션 연결을 종료합니다.");
            await _runner.Shutdown();

            _runner = null;
            _isSessionLobbyJoined = false;
            _isMatchmakingRequestInProgress = false;
        }

        SceneManager.LoadSceneAsync("StartScreen");
    }

    private NetworkRunner CreateRunner()
    {
        if (_runner != null)
        {
            return _runner;
        }

        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;

        if (_callbackTarget != null)
        {
            _runner.AddCallbacks(_callbackTarget);
        }

        return _runner;
    }

    private async Task<StartGameResult> StartGame(GameMode mode, string sessionName = null)
    {
        CreateRunner();

        var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
        var sceneInfo = new NetworkSceneInfo();
        if (scene.IsValid)
        {
            sceneInfo.AddSceneRef(scene, LoadSceneMode.Additive);
        }

        var startGameArgs = new StartGameArgs()
        {
            GameMode = mode,
            SessionName = sessionName ?? "TestRoom", // sessionName이 값이 있으면 그걸 쓰고, 없으면 TestRoom을 기본값으로 사용
            Scene = scene,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        };

        if (mode == GameMode.Host)
        {
            startGameArgs.PlayerCount = _sessionLimit;
            startGameArgs.SessionProperties = new Dictionary<string, SessionProperty>()
            {
                { SessionLimitPropertyKey, _sessionLimit },
                { SessionInGamePropertyKey, SessionOpenValue }
            };
        }

        return await _runner.StartGame(startGameArgs);
    }

    private async Task<List<SessionInfo>> GetSessionListFromLobby()
    {
        CreateRunner();

        _sessionListCompletionSource = new TaskCompletionSource<List<SessionInfo>>();
        if (!_isSessionLobbyJoined)
        {
            StartGameResult lobbyResult = await _runner.JoinSessionLobby(SessionLobby.ClientServer);
            if (!lobbyResult.Ok)
            {
                Debug.LogError($"세션 로비 입장 실패: {lobbyResult.ShutdownReason} / {lobbyResult.ErrorMessage}");
                return null;
            }

            _isSessionLobbyJoined = true;
        }
        else
        {
            _sessionListCompletionSource.TrySetResult(new List<SessionInfo>(_cachedSessionList));
        }

        return await WaitForSessionList();
    }

    private async Task<List<SessionInfo>> WaitForSessionList()
    {
        Task<List<SessionInfo>> sessionListTask = _sessionListCompletionSource.Task;
        Task completedTask = await Task.WhenAny(sessionListTask, Task.Delay(3000));

        if (completedTask == sessionListTask)
        {
            return sessionListTask.Result;
        }

        return new List<SessionInfo>(_cachedSessionList);
    }

    private SessionInfo GetJoinableSession(List<SessionInfo> sessionList)
    {
        foreach (SessionInfo session in sessionList)
        {
            if (session != null &&
                session.IsValid &&
                session.IsOpen &&
                !IsSessionInGame(session) &&
                session.PlayerCount < session.MaxPlayers)
            {
                return session;
            }
        }

        return null;
    }

    private bool IsSessionInGame(SessionInfo session)
    {
        if (session.Properties == null ||
            !session.Properties.TryGetValue(SessionInGamePropertyKey, out SessionProperty inGameProperty) ||
            !inGameProperty.IsInt)
        {
            return false;
        }

        return inGameProperty == SessionInGameValue;
    }

    private int GetCurrentCcu(List<SessionInfo> sessionList)
    {
        int currentCcu = 0;
        foreach (SessionInfo session in sessionList)
        {
            if (session != null && session.IsValid)
            {
                currentCcu += session.PlayerCount;
            }
        }

        return currentCcu;
    }

    private bool CanCreateSessionWithinCcuLimit(int currentCcu)
    {
        return currentCcu + _sessionLimit <= _photonCcuLimit;
    }

    private int GetSessionLimit(SessionInfo session)
    {
        if (session.Properties != null &&
            session.Properties.TryGetValue(SessionLimitPropertyKey, out SessionProperty sessionLimitProperty) &&
            sessionLimitProperty.IsInt)
        {
            return sessionLimitProperty;
        }

        return session.MaxPlayers;
    }

    private string GenerateRandomSessionName(List<SessionInfo> sessionList)
    {
        HashSet<string> existingSessionNames = new HashSet<string>();
        foreach (SessionInfo session in sessionList)
        {
            if (session != null && !string.IsNullOrEmpty(session.Name))
            {
                existingSessionNames.Add(session.Name);
            }
        }

        for (int i = 0; i < 20; i++)
        {
            _roomNum = Random.Range(MinRandomRoomNumber, MaxRandomRoomNumber);
            _sessionName = _roomNum.ToString();

            if (!existingSessionNames.Contains(_sessionName))
            {
                return _sessionName;
            }
        }

        _roomNum = Random.Range(MinRandomRoomNumber, MaxRandomRoomNumber);
        _sessionName = $"{_roomNum}";
        return _sessionName;
    }

    private void ForceReturnToStartScreen()
    {
        if (_isReturningToMainMenu)
        {
            return;
        }

        _isReturningToMainMenu = true;
        _runner = null;
        _isSessionLobbyJoined = false;
        _isMatchmakingRequestInProgress = false;

        Debug.Log("호스트 나감 또는 세션 종료로 StartScreen으로 이동합니다.");
        SceneManager.LoadSceneAsync("StartScreen");
    }
}
