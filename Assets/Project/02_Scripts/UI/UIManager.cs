using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _mainStatus;
    [SerializeField] private TextMeshProUGUI _rountStatus;
    [SerializeField] private TextMeshProUGUI _eliminatedPlayersStatus;
    [SerializeField] private NetworkCountdown _networkCountdown;

    private readonly List<string> _eliminatedPlayerLogs = new List<string>();

    private void OnEnable()
    {
        GameManager.PlayerDead += OnEliminatedPlayer;

        if (_networkCountdown == null)
        {
            Debug.LogWarning($"{nameof(UIManager)}에 {nameof(NetworkCountdown)} 참조가 없습니다.");
            return;
        }

        _networkCountdown.MainStatusChanged += OnMainStatusChanged;
        _networkCountdown.RoundStatusChanged += OnNumberOfRounds;
        _networkCountdown.WhileCountDown += OnBeforeStartRound;
    }

    private void OnDisable()
    {
        GameManager.PlayerDead -= OnEliminatedPlayer;

        if (_networkCountdown == null) return;

        _networkCountdown.MainStatusChanged -= OnMainStatusChanged;
        _networkCountdown.RoundStatusChanged -= OnNumberOfRounds;
        _networkCountdown.WhileCountDown -= OnBeforeStartRound;
    }

    [ContextMenu("테스트 메시지 보내기")]
    public void TestAddMessage()
    {
        OnEliminatedPlayer("테스트 플레이어");
    }

    private void OnMainStatusChanged(string message)
    {
        _mainStatus.text = message;
    }

    private void OnBeforeStartRound(string message)
    {
        _mainStatus.text = message;
    }

    private void OnNumberOfRounds(string message)
    {
        _rountStatus.text = message;
    }

    private void OnEliminatedPlayer(string playerName)
    {
        if (_eliminatedPlayersStatus == null)
        {
            Debug.LogWarning("탈락자 상태 텍스트 참조가 없습니다.");
            return;
        }

        _eliminatedPlayerLogs.Add($"{playerName} 탈락");
        _eliminatedPlayersStatus.text = string.Join(Environment.NewLine, _eliminatedPlayerLogs);
    }
}
