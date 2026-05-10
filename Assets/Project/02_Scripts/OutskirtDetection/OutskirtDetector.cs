using System.Collections.Generic;
using UnityEngine;

// 플레이어가 외곽 감지 영역에 닿으면 Y 위치 고정을 임시로 해제
public class OutskirtDetector : MonoBehaviour
{
    private const string PlayerLayerName = "Player";
    private const string PlayerTag = "Player";
    private const float DefaultPlayerY = 1.075f;
    private const float SafeAreaMargin = 0.05f;

    // 외곽 진입 전 Rigidbody 제약 상태를 저장해 복구 시 원래 값으로 되돌림
    private static readonly Dictionary<Rigidbody, PlayerFreezeState> _playerFreezeStates = new Dictionary<Rigidbody, PlayerFreezeState>();
    private static readonly Dictionary<Rigidbody, int> _playerOutskirtContactCounts = new Dictionary<Rigidbody, int>();
    private static readonly HashSet<Rigidbody> _playersRestoredUntilExit = new HashSet<Rigidbody>();

    private static Transform _safeAreaRoot;
    private static float _safeAreaHalfX;
    private static float _safeAreaHalfZ;
    private static bool _hasSafeAreaBounds;

    private void OnTriggerEnter(Collider other)
    {
        if (!TryGetPlayerRigidbody(other, out Rigidbody playerRigidbody))
        {
            return;
        }

        AddOutskirtContact(playerRigidbody);
        ReleasePlayerFreeze(playerRigidbody, true);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!TryGetPlayerRigidbody(other, out Rigidbody playerRigidbody))
        {
            return;
        }

        ReleasePlayerFreeze(playerRigidbody, true);
    }

    private void OnTriggerExit(Collider other)
    {
        RestoreOrKeepReleasedBySafeArea(other);
    }

    public static void ConfigureSafeArea(Transform safeAreaRoot, float sizeX, float sizeZ)
    {
        _safeAreaRoot = safeAreaRoot;
        _safeAreaHalfX = sizeX * 0.5f;
        _safeAreaHalfZ = sizeZ * 0.5f;
        _hasSafeAreaBounds = sizeX > 0.0f && sizeZ > 0.0f;
    }

    public static void RestorePlayerFreeze(Rigidbody playerRigidbody)
    {
        RestorePlayerFreeze(playerRigidbody, true, true);
    }

    private static void ReleasePlayerFreeze(Rigidbody playerRigidbody, bool ignoreRestoredUntilExit)
    {
        if (playerRigidbody == null || !CanChangePlayerRigidbody(playerRigidbody))
        {
            return;
        }

        if (ignoreRestoredUntilExit && _playersRestoredUntilExit.Contains(playerRigidbody))
        {
            return;
        }

        if (!_playerFreezeStates.ContainsKey(playerRigidbody))
        {
            _playerFreezeStates.Add(
                playerRigidbody,
                new PlayerFreezeState(playerRigidbody.constraints));
        }

        playerRigidbody.constraints &= ~RigidbodyConstraints.FreezePositionY;
        playerRigidbody.WakeUp();
    }

    private void RestoreOrKeepReleasedBySafeArea(Collider other)
    {
        if (!TryGetPlayerRigidbody(other, out Rigidbody playerRigidbody))
        {
            return;
        }

        _playersRestoredUntilExit.Remove(playerRigidbody);

        int remainingContactCount = RemoveOutskirtContact(playerRigidbody);
        if (remainingContactCount > 0)
        {
            return;
        }

        // 감지기에서 나왔지만 안전 영역 안이면 다시 Y 위치를 고정
        if (IsInsideSafeArea(playerRigidbody.position))
        {
            RestorePlayerFreeze(playerRigidbody, false, false);
            return;
        }

        ReleasePlayerFreeze(playerRigidbody, false);
    }

    private static void RestorePlayerFreeze(Rigidbody playerRigidbody, bool ignoreUntilExit, bool resetY)
    {
        if (playerRigidbody == null || !CanChangePlayerRigidbody(playerRigidbody))
        {
            return;
        }

        bool hasFreezeState = _playerFreezeStates.TryGetValue(playerRigidbody, out PlayerFreezeState freezeState);
        RigidbodyConstraints restoredConstraints = hasFreezeState
            ? freezeState.OriginalConstraints
            : playerRigidbody.constraints | RigidbodyConstraints.FreezePositionY;

        if (resetY)
        {
            // 떨어지지 않은 플레이어는 기본 높이로 되돌린 뒤 Y 위치를 다시 고정
            playerRigidbody.constraints &= ~RigidbodyConstraints.FreezePositionY;

            Vector3 position = playerRigidbody.position;
            playerRigidbody.position = new Vector3(position.x, DefaultPlayerY, position.z);
            playerRigidbody.transform.position = playerRigidbody.position;

            Vector3 velocity = playerRigidbody.linearVelocity;
            playerRigidbody.linearVelocity = new Vector3(velocity.x, 0.0f, velocity.z);

            restoredConstraints |= RigidbodyConstraints.FreezePositionY;
        }

        playerRigidbody.constraints = restoredConstraints;

        if (hasFreezeState)
        {
            _playerFreezeStates.Remove(playerRigidbody);
        }

        if (ignoreUntilExit && hasFreezeState)
        {
            _playersRestoredUntilExit.Add(playerRigidbody);
        }
    }

    private static bool TryGetPlayerRigidbody(Collider other, out Rigidbody playerRigidbody)
    {
        playerRigidbody = other.attachedRigidbody;
        if (playerRigidbody == null)
        {
            return false;
        }

        int playerLayer = LayerMask.NameToLayer(PlayerLayerName);
        bool isPlayerLayer = other.gameObject.layer == playerLayer || playerRigidbody.gameObject.layer == playerLayer;
        bool isPlayerTag = other.CompareTag(PlayerTag) || playerRigidbody.CompareTag(PlayerTag);

        return isPlayerLayer || isPlayerTag;
    }

    private static bool CanChangePlayerRigidbody(Rigidbody playerRigidbody)
    {
        // 네트워크 플레이어는 StateAuthority가 있는 인스턴스에서만 Rigidbody를 변경
        if (playerRigidbody.TryGetComponent(out PhotonPlayer player) && player.Object != null)
        {
            return player.Object.HasStateAuthority;
        }

        return true;
    }

    private static void AddOutskirtContact(Rigidbody playerRigidbody)
    {
        if (_playerOutskirtContactCounts.TryGetValue(playerRigidbody, out int contactCount))
        {
            _playerOutskirtContactCounts[playerRigidbody] = contactCount + 1;
            return;
        }

        _playerOutskirtContactCounts.Add(playerRigidbody, 1);
    }

    private static int RemoveOutskirtContact(Rigidbody playerRigidbody)
    {
        if (!_playerOutskirtContactCounts.TryGetValue(playerRigidbody, out int contactCount))
        {
            return 0;
        }

        contactCount--;
        if (contactCount <= 0)
        {
            _playerOutskirtContactCounts.Remove(playerRigidbody);
            return 0;
        }

        _playerOutskirtContactCounts[playerRigidbody] = contactCount;
        return contactCount;
    }

    private static bool IsInsideSafeArea(Vector3 worldPosition)
    {
        if (!_hasSafeAreaBounds)
        {
            return true;
        }

        Vector3 localPosition = _safeAreaRoot != null
            ? _safeAreaRoot.InverseTransformPoint(worldPosition)
            : worldPosition;

        return localPosition.x > -_safeAreaHalfX + SafeAreaMargin
               && localPosition.x < _safeAreaHalfX - SafeAreaMargin
               && localPosition.z > -_safeAreaHalfZ + SafeAreaMargin
               && localPosition.z < _safeAreaHalfZ - SafeAreaMargin;
    }

    private readonly struct PlayerFreezeState
    {
        public PlayerFreezeState(RigidbodyConstraints originalConstraints)
        {
            OriginalConstraints = originalConstraints;
        }

        public RigidbodyConstraints OriginalConstraints { get; }
    }
}
