using System;
using System.Collections;
using Fusion;
using UnityEngine;

public class NetworkCountdown : NetworkBehaviour
{
    #region 변수들

    [Header("게임매니저 컴포넌트")]
    [SerializeField] private GameManager _gameMgr;

    // 타이머 : NetWorked 변수
    [Networked] private TickTimer CountDownTimer { get; set; }
    [Networked] private int RemainingSecondsInt { get; set; }
    [Networked] private int TotalSecondsInt { get; set; }
    
    private int _lastRenderedSecond = -1;
    private int _lastPrintedSecond = -1;
    private bool _startedEventSent;
    private bool _isRunningRendered;
    

    #region 이벤트
    public event Action<string> WhileCountDown;    // 카운트다운 진행 중 : 남은 시간이 바뀔 때마다 남은 초 전달
    public event Action<string> MainStatusChanged;    // 메인 상태 UI 변경
    public event Action<string> RoundStatusChanged;   // 라운드 상태 UI 변경
    #endregion
    

    #endregion

    #region 카운트다운
    public void RunningCountDown(int seconds)
    {
        if (!Object.HasStateAuthority) return;

        CountDownTimer = TickTimer.CreateFromSeconds(Runner, seconds);
        TotalSecondsInt = seconds;
        RemainingSecondsInt = seconds;
        _lastPrintedSecond = seconds;
        _startedEventSent = false;
        _isRunningRendered = false;
    }
    
    // Render는 호스트/클라이언트 모두에서 매 프레임 호출됨
    public override void Render()
    {
        // 이전에 렌더링한 값과 다를 때만 UI 갱신
        if (RemainingSecondsInt == _lastRenderedSecond) return;
        if (RemainingSecondsInt <= 0 && !_startedEventSent && !_isRunningRendered) return;

        _lastRenderedSecond = RemainingSecondsInt;
        if (RemainingSecondsInt > 0)
        {
            _startedEventSent = true;
            _isRunningRendered = true;

            WhileCountDown?.Invoke($"라운드 남은 시간... {RemainingSecondsInt}");
            Debug.Log($"라운드 남은 시간... {RemainingSecondsInt}"); // Guest에서도 나오는 장면
            
            return;
        }
        else
        {
            WhileCountDown?.Invoke($"라운드 시작!");
        }
        
        if (_startedEventSent && _isRunningRendered)
        {
            _isRunningRendered = false;
            
            Debug.Log("라운드 종료");
        }
    }
    #endregion

    #region 유니티 콜백 메서드
    // 호스트만 계산 및 [Networked] 변수 갱신
    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        if (!CountDownTimer.IsRunning) return;

        float remaining = CountDownTimer.RemainingTime(Runner) ?? 0f;
        int remainSecond = Mathf.CeilToInt(remaining);

        if (remainSecond != _lastPrintedSecond && remainSecond > 0)
        {
            _lastPrintedSecond = remainSecond;
            RemainingSecondsInt = remainSecond;
        }

        if (CountDownTimer.Expired(Runner))
        {
            Debug.Log("선택 시간 종료");
            RemainingSecondsInt = 0;
            CountDownTimer = TickTimer.None;

            // 여기에 각자가 가진 arrow를 disable하도록 스크립트 작성
            _gameMgr.ShowEachOthersArrow(false);
            
            StartCoroutine(BoostAfterDelay(2f));
        }
    }
    #endregion

    #region 코루틴 : 몇 초 뒤에 게임매니저 스크립트를 참조해서 모든 플레이어들의 방향을 바꾸고, 다시 몇 초 뒤에 해당 방향으로 발사
    // NetworkCountdown.cs에 코루틴 추가
    private IEnumerator BoostAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        _gameMgr.FaceAllPlayersToQueuedDirection();
        
        yield return new WaitForSeconds(delay);
        _gameMgr.BoostAllPlayers();
    }
    #endregion

    #region RPC 메서드들
    #region UI 갱신
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RpcSetMainStatus(string message)
    {
        MainStatusChanged?.Invoke(message);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RpcSetRoundStatus(string message)
    {
        RoundStatusChanged?.Invoke(message);
    }
    #endregion

    #region 게임매니저에서 RPC 사용 위해서 경유하는 RPC 매서드들
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RpcWarnOutskirt()
    {
        _gameMgr.ApplyOutskirtWarning();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RpcFallMap()
    {
        _gameMgr.ApplyFallMap();
    }
    #endregion
    #endregion
}
