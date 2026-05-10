using System;
using System.Collections;
using Fusion;
using UnityEngine;

public class RoundFlowController : MonoBehaviour
{
    // 라운드 흐름 관리 스크립트
    [Header("라운드 의존 컴포넌트")]
    [SerializeField] private PlayerRegistry _playerRegistry;
    [SerializeField] private RoundPhysicsMonitor _roundPhysicsMonitor;
    [SerializeField] private MapPhaseController _mapPhaseController;
    [SerializeField] private NetworkCountdown _countDown;

    [Header("현재 라운드 관련 정보")]
    [SerializeField] private int _currRound = 0;
    [SerializeField] private int _maxRound = 0;
    [SerializeField] private int _roundPerSec = 20;

    private bool _isGameStartScheduled;
    private Coroutine _checkStopCoroutine;
    private Func<NetworkRunner> _runnerProvider;
    private WaitForSeconds _checkStopWait = new WaitForSeconds(1f);

    public event Action<GameState> StateChanged;
    public event Action<string> AfterGameStart;
    public event Action<string> RoundReady;
    public event Action<string> RoundStarted;
    public event Action<string> RoundEnded;
    public event Action<string> AfterMapReduce;

    public GameState State { get; private set; } = GameState.Waiting;

    // 초기화 - Codex 도움 받음
    public void Initialize(
        PlayerRegistry playerRegistry,
        RoundPhysicsMonitor roundPhysicsMonitor,
        MapPhaseController mapPhaseController,
        NetworkCountdown countDown,
        Func<NetworkRunner> runnerProvider) // 함수 자체를 전달받음
    {
        _playerRegistry = playerRegistry;
        _roundPhysicsMonitor = roundPhysicsMonitor;
        _mapPhaseController = mapPhaseController;
        _countDown = countDown;
        _runnerProvider = runnerProvider;
    }

    public void TryScheduleGameStart(int currentPlayerCount, int gameStartPlayerCount)
    {
        // 아직 플레이어가 충분하지 않거나 or 게임이 시작한 상태가 아니라면 (뒤의 두개는 사실상 동의미)
        if (currentPlayerCount < gameStartPlayerCount || _isGameStartScheduled || State != GameState.Waiting)
        {
            return;
        }

        _isGameStartScheduled = true;
        _currRound = 0;

        Debug.Log("인원 충족 시");
        StartCoroutine(StartingRoundAfterSec());
    }

    // 라운드 준비
    public void RoundsReady()
    {
        SetState(GameState.RoundReady);

        _currRound++;

        _playerRegistry.RefreshRoundCounts();
        _roundPhysicsMonitor.RestoreSurvivorPlayerFreeze(_playerRegistry.SurvivalCharacters);

        OutskirtWarnRed(); // 이번 라운드에서 떨어져나갈 외곽 빨갛게 칠해서 경고
        SetTimer(_roundPerSec); // 타이머 세팅
        SetRoundState(); // 지금이 몇 라운드인지 UI로 띄우기
        ShowEachOthersArrow(true); // 각자 플레이어들의 화살표 보여주기
    }

    private void OutskirtWarnRed()
    {
        _countDown.RpcWarnOutskirt();
    }

    public void ApplyOutskirtWarning()
    {
        NetworkRunner runner = _runnerProvider?.Invoke();
        if (runner == null || !runner.IsServer) return;

        _mapPhaseController.ApplyOutskirtWarning(_currRound);
    }

    private void SetRoundState()
    {
        SetRoundStatus($"{_currRound} 라운드");
        Debug.Log($"{_currRound} 라운드");
    }

    private IEnumerator StartingRoundAfterSec()
    {
        string startMessage = "게임이 곧 시작합니다";
        AfterGameStart?.Invoke(startMessage);
        _countDown.RpcSetMainStatus(startMessage);

        yield return _checkStopWait;

        _maxRound = _mapPhaseController.GetMaxRound();
        RoundsReady();
        _mapPhaseController.ApplyOutskirtDetectorRound(_currRound);
    }

    private IEnumerator StartingNewRoundAfterSec()
    {
        yield return _checkStopWait;

        Debug.Log(_playerRegistry.AliveCharacterCount);
        if (_playerRegistry.AliveCharacterCount < 2)
        {
            GameEnd();
        }
        else
        {
            if (_currRound <= _maxRound)
            {
                RoundsReady();
            }
        }
    }

    public void BoostAllPlayers()
    {
        SetState(GameState.RoundPlaying);
        Debug.Log("전체 부스트 시작");

        foreach (var player in _playerRegistry.SurvivalCharacters)
        {
            NetworkObject netObj = player.Value;

            PlayerLauncher launcher = netObj.transform.GetComponent<PlayerLauncher>();
            launcher.TriggerLaunch();
            Debug.Log($"{player.Key} 부스트");

            netObj.transform.GetComponent<PhotonPlayer>().RpcShowingDustParticle(true);
        }

        _checkStopCoroutine = StartCoroutine(CheckAllPlayersStopped());
    }

    private void OnAllPlayersStopped()
    {
        NetworkRunner runner = _runnerProvider?.Invoke();
        if (runner == null || !runner.IsServer) return;

        _checkStopCoroutine = null;
        Debug.Log("모든 플레이어가 멈췄습니다 — 라운드 종료 처리 진입");

        Debug.Log($"{_playerRegistry.SurvivalCharacters}");
    }

    private IEnumerator CheckAllPlayersStopped()
    {
        bool fallFlag = false;

        yield return _checkStopWait;

        while (State == GameState.RoundPlaying)
        {
            yield return _checkStopWait;

            if (_playerRegistry.AliveCharacterCount == 0) break;

            bool allStopped = _roundPhysicsMonitor.AreAllSurvivorsStopped(
                _playerRegistry.AliveCharacterCount,
                _playerRegistry.SurvivalCharacters);

            if (allStopped)
            {
                OnAllPlayersStopped();

                if (!fallFlag)
                {
                    fallFlag = true;
                    _countDown.RpcFallMap();

                    if (_currRound <= _maxRound)
                    {
                        _mapPhaseController.ApplyOutskirtDetectorRound(_currRound + 1);
                    }

                    yield return WaitUntilAllSurvivorsStoppedAfterFall();

                    string roundResult;

                    if (_playerRegistry.AliveCharacterCount <= 1) // 생존자가 1명 이하라면...
                    {
                        roundResult = "게임 종료!";
                    }
                    else // 그렇지 않고 생존자가 2명 이상이라면
                    {
                        roundResult = $"{_currRound}라운드 종료. {_playerRegistry.AliveCharacterCount}명 생존 / {_playerRegistry.DeadCount}명 탈락";
                    }

                    AfterMapReduce?.Invoke(roundResult);
                    _countDown.RpcSetMainStatus(roundResult);
                }

                yield return _checkStopWait;

                StartCoroutine(StartingNewRoundAfterSec());
                yield break;
            }
        }
    }

    private IEnumerator WaitUntilAllSurvivorsStoppedAfterFall()
    {
        yield return _checkStopWait;

        while (State == GameState.RoundPlaying)
        {
            if (_roundPhysicsMonitor.AreAllSurvivorsStopped(
                    _playerRegistry.AliveCharacterCount,
                    _playerRegistry.SurvivalCharacters))
            {
                yield break;
            }

            yield return _checkStopWait;
        }
    }

    public void ApplyFallMap()
    {
        NetworkRunner runner = _runnerProvider?.Invoke();
        if (runner == null || !runner.IsServer) return;

        _mapPhaseController.ApplyFallMap();
    }

    public void GameEnd()
    {
        SetState(GameState.GameOver);
        Debug.Log("게임이 종료되었습니다!");
        ShowSurvivorsReturnPanelWin();
    }

    private void SetTimer(int time)
    {
        _countDown.RunningCountDown(time);
    }

    public void ShowEachOthersArrow(bool flag)
    {
        foreach (var kvp in _playerRegistry.SurvivalCharacters)
        {
            NetworkObject netObj = kvp.Value;

            PhotonPlayer photonPlayerScript = netObj.GetComponent<PhotonPlayer>();

            if (photonPlayerScript != null)
            {
                photonPlayerScript.RequestHideArrowFromStateAuthority(flag);
            }
        }
    }

    // 모든 플레이어들을 진출 방향대로 바라보게 회전시키기
    public void FaceAllPlayersToQueuedDirection()
    {
        foreach (var kvp in _playerRegistry.SurvivalCharacters)
        {
            NetworkObject netObj = kvp.Value;
            if (netObj == null)
            {
                continue;
            }

            PhotonPlayer photonPlayerScript = netObj.GetComponent<PhotonPlayer>();
            if (photonPlayerScript != null)
            {
                photonPlayerScript.RequestFaceQueuedDirectionFromStateAuthority();
            }
        }
    }

    private void SetRoundStatus(string message)
    {
        RoundStarted?.Invoke(message);
        _countDown.RpcSetRoundStatus(message);
    }

    // 생존자에게는 승리 패널 보여주기 (여기서는 배열로 여러명 받아오는걸로 하지만 실제 게임 로직을 생각해보면 딱 1명만 받을 구조임)
    private void ShowSurvivorsReturnPanelWin()
    {
        foreach (var kvp in _playerRegistry.SurvivalCharacters)
        {
            NetworkObject netObj = kvp.Value;
            if (netObj == null)
            {
                continue;
            }

            PhotonPlayer photonPlayerScript = netObj.GetComponent<PhotonPlayer>();
            if (photonPlayerScript != null)
            {
                photonPlayerScript.RequestShowReturnPanelWinFromStateAuthority();
            }
        }
    }

    private void SetState(GameState nextState)
    {
        State = nextState;
        StateChanged?.Invoke(nextState);
    }
}
