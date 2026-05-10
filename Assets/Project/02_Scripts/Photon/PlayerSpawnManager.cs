using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class PlayerSpawnManager : MonoBehaviour
{
    [Header("플레이어 프리팹 할당")]
    [SerializeField] private NetworkPrefabRef[] _playerPrefab;

    [Header("스폰 지점")]
    [SerializeField] private List<Transform> _playerSpawnPoints;

    // 초기화 함수 - Codex 도움 받음
    public NetworkObject SpawnPlayer(
        NetworkRunner runner,
        PlayerRef player,
        int currentPlayerCount,
        GameManager gameMgr,
        Action<NetworkObject> onBeforeSpawned = null)
    {
        NetworkPrefabRef playerPrefab = GetPlayerPrefab(currentPlayerCount);
        Vector3 spawnPosition = GetSpawnPosition(currentPlayerCount);

        return runner.Spawn(playerPrefab, spawnPosition, Quaternion.identity, player,
            (runner, obj) =>
            {
                obj.GetComponent<PhotonPlayer>().Init(gameMgr);
                onBeforeSpawned?.Invoke(obj);
            }
        );
    }

    private NetworkPrefabRef GetPlayerPrefab(int currentPlayerCount)
    {
        // 방어 코드 : 플레이어 프리팹이 할당되지 않았다면
        if (!HasItems(_playerPrefab))
        {
            Debug.LogError("PlayerSpawnManager에 플레이어 프리팹이 할당되지 않았습니다.");
            return default;
        }

        int prefabIndex = Mathf.Clamp(currentPlayerCount - 1, 0, _playerPrefab.Length - 1);
        return _playerPrefab[prefabIndex];
    }

    private Vector3 GetSpawnPosition(int currentPlayerCount)
    {
        // 방어 코드 : 플레이어 스폰 지점이 설정되지 않았다면
        if (!HasItems(_playerSpawnPoints))
        {
            Debug.LogError("PlayerSpawnManager에 스폰 지점이 할당되지 않았습니다.");
            return Vector3.zero; // 그냥 0 0 0 지점 보내주기
        }

        int spawnIndex = Mathf.Clamp(currentPlayerCount - 1, 0, _playerSpawnPoints.Count - 1);
        return _playerSpawnPoints[spawnIndex].position;
    }

    // 콜렉션에 아이템이 있냐 없냐 판단
    private bool HasItems<T>(ICollection<T> items) 
    {
        if (items != null && items.Count > 0)
        {
            return true;  // 아이템이 있음
        }
        else
        {
            return false; // 아이템이 없음
        }
    }
}
