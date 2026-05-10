using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class PlayerRegistry : MonoBehaviour
{
    private readonly Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();
    private readonly Dictionary<PlayerRef, NetworkObject> _survivalCharacters = new Dictionary<PlayerRef, NetworkObject>();
    private readonly Dictionary<PlayerRef, NetworkObject> _deadCharacters = new Dictionary<PlayerRef, NetworkObject>();

    [Header("플레이어 수 / 탈락자 / 생존자 정보")]
    [SerializeField] private int _currPlayerCnt;
    [SerializeField] private int _survivorCount;
    [SerializeField] private int _deadCount;

    public int CurrentPlayerCount => _currPlayerCnt;
    public int DeadCount => _deadCount;
    public int AliveCharacterCount => _survivalCharacters.Count;

    public IEnumerable<KeyValuePair<PlayerRef, NetworkObject>> SurvivalCharacters => _survivalCharacters;

    public int RegisterPlayerJoined()
    {
        _currPlayerCnt++;
        return _currPlayerCnt;
    }

    public void RegisterSpawned(PlayerRef player, NetworkObject networkObject)
    {
        _spawnedCharacters.Add(player, networkObject);
        _survivalCharacters.Add(player, networkObject);
    }

    public bool TryGetSpawned(PlayerRef player, out NetworkObject networkObject)
    {
        return _spawnedCharacters.TryGetValue(player, out networkObject);
    }

    public bool TryGetSurvivor(PlayerRef player, out NetworkObject networkObject)
    {
        return _survivalCharacters.TryGetValue(player, out networkObject);
    }

    public void MarkPlayerLeft(PlayerRef player, NetworkObject networkObject, bool wasAlive)
    {
        _spawnedCharacters.Remove(player);
        _currPlayerCnt = Mathf.Max(0, _currPlayerCnt - 1);

        if (wasAlive && _survivalCharacters.Remove(player))
        {
            _deadCharacters[player] = networkObject;
        }

        RefreshCounts();
    }

    public void RefreshRoundCounts()
    {
        _survivorCount = _survivalCharacters.Count;
        _deadCount = 0;
    }

    public void MarkDead(PlayerRef player, NetworkObject networkObject)
    {
        _survivalCharacters.Remove(player);
        _deadCharacters[player] = networkObject;

        RefreshCounts();
    }

    private void RefreshCounts()
    {
        _survivorCount = _survivalCharacters.Count;
        _deadCount = _deadCharacters.Count;
    }
}
