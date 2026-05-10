using Fusion;
using UnityEngine;

public class PlayerDeathHandler : MonoBehaviour
{
    private PhotonPlayer _owner;
    private GameManager _gameMgr;
    private PlayerLauncher _launcher;
    private GameObject _body;

    public void Initialize(
        PhotonPlayer owner,
        GameManager gameMgr,
        PlayerLauncher launcher,
        GameObject body)
    {
        _owner = owner;
        _gameMgr = gameMgr;
        _launcher = launcher;
        _body = body;
    }

    public void SetGameManager(GameManager gameMgr)
    {
        _gameMgr = gameMgr;
    }

    public string GetDisplayPlayerName(string requestedName, PlayerRef playerRef)
    {
        return string.IsNullOrEmpty(requestedName)
            ? $"Player {playerRef.PlayerId}"
            : requestedName;
    }

    public void HandleLocalDeathRequested(string playerName)
    {
        Debug.Log($"{playerName} 탈락!");
        ShowLocalReturnPanel(true);
    }

    public void ApplyDeadState(bool hasInputAuthority)
    {
        if (_launcher != null)
        {
            _launcher.enabled = false;
        }

        if (_body != null)
        {
            _body.SetActive(false);
        }

        if (_owner != null)
        {
            _owner.ShowArrow(false);
        }

        if (hasInputAuthority)
        {   
            ShowLocalReturnPanel(true);
        }
    }

    public void EliminatePlayer(PlayerRef playerRef, string eliminatedPlayerName)
    {
        CacheGameManagerIfNeeded();

        if (_gameMgr == null)
        {
            Debug.LogWarning("GameMgr_PhotonHostMode를 찾지 못해서 탈락 처리를 요청할 수 없습니다.");
            return;
        }

        _gameMgr.EliminatePlayer(playerRef);
    }

    public void NotifyPlayerEliminated(string eliminatedPlayerName)
    {
        Debug.Log($"==========={eliminatedPlayerName} 탈락");
        GameManager.NotifyPlayerEliminated(eliminatedPlayerName);
    }

    public void ShowLocalReturnPanel(bool flag)
    {
        CacheGameManagerIfNeeded();

        if (_gameMgr == null)
        {
            Debug.LogWarning("GameMgr_PhotonHostMode를 찾지 못해서 ReturnPanel을 표시할 수 없습니다.");
            return;
        }

        _gameMgr.ShowReturnPanelDefeat(flag);
    }

    public void ShowLocalReturnPanelWin(bool flag)
    {
        CacheGameManagerIfNeeded();

        if (_gameMgr == null)
        {
            Debug.LogWarning("GameMgr_PhotonHostMode를 찾지 못해서 Win ReturnPanel을 표시할 수 없습니다.");
            return;
        }

        _gameMgr.ShowReturnPanelWin(flag);
    }

    private void CacheGameManagerIfNeeded()
    {
        if (_gameMgr == null)
        {
            _gameMgr = FindFirstObjectByType<GameManager>();
        }
    }
}
