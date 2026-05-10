using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class FallOffMapShrinker : MonoBehaviour
{
    // 큐브를 떨어뜨릴 때 필요한 참조 구조체
    // 생성 시점에 미리 캐싱한 겂을 사용하므로 GetComponent 반복 호출울 줄이기 위함
    private readonly struct TileData
    {
        public GameObject Tile { get; }
        public Rigidbody Rigidbody { get; }
        public CubeColorChange ColorChange { get; }
        public NetworkObject NetworkObject { get; }

        public TileData(GameObject tile, Rigidbody rigidbody, CubeColorChange colorChange, NetworkObject networkObject)
        {
            Tile = tile;
            Rigidbody = rigidbody;
            ColorChange = colorChange;
            NetworkObject = networkObject;
        }
    }

    public static FallOffMapShrinker Instance { get; private set; }

    [Serializable]
    public class ShrinkEvent : UnityEvent<int, int> { }
    [Serializable]
    public class TileEvent : UnityEvent<GameObject> { }

    [Header("맵 세팅")]
    [SerializeField] private GameObject _tilePrefab; // 생성할 큐브 프리팹
    [SerializeField] private int _initialSize = 5; // 시작할 맵의 크기 5x5 홀수로만 할것!!
    [SerializeField] private int _requestedShrinkLayers = 1; // 떨어 트릴 큐브의 겹수 

    [Header("큐브 떨어지는 시간 / 힘")]
    [SerializeField] private float _fallDuration = 1.0f; // 떨어지는 시간
    [SerializeField] private float _fallImpulse = 2.0f; // 떨어지는 힘의 크기 변수
    
    [Header("이벤트")] // 결과를 받아볼 콜백 함수 등록
    [SerializeField] private ShrinkEvent _onShrinkStarted;
    [SerializeField] private TileEvent _onTileWarning;
    [SerializeField] private TileEvent _onTileFallStarted;
    [SerializeField] private TileEvent _onTileDestroyed;

    private GameObject[,] _mapGrid;
    private Rigidbody[,] _rigidbodyGrid;
    private CubeColorChange[,] _colorChangeGrid;
    private NetworkObject[,] _networkObjectGrid;

    private int _centerIndex;
    private int _currentSize;
    private bool _isShrinking;
    private bool _hasPendingTiles;
    private bool _isMapSpawned;

    private WaitForSeconds _fallWait;
    private List<TileData> _pendingTilesToFall = new List<TileData>();

    private void Awake()
    {
        Instance = this;

        if ((_initialSize & 1) == 0)
        {
            Debug.LogError("초기 사이즈는 반드시 홀수여야 함");
            _initialSize += 1;
        }

        _centerIndex = _initialSize >> 1;
        _fallWait = new WaitForSeconds(_fallDuration);

        InitializeMapStorage();
        _currentSize = _initialSize;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // 1. 떨어 트릴  둘레  결정 요청 함수
    public void RequestShrink()
    {
        RequestShrink(_requestedShrinkLayers);
    }

    public void SpawnMap(NetworkRunner runner)
    {
        if (_isMapSpawned)
        {
            return;
        }

        if (runner == null || !runner.IsServer)
        {
            return;
        }

        if (_tilePrefab == null)
        {
            Debug.LogError("타일 프리팹 배정 안됨!!!");
            return;
        }

        InitializeMapStorage();
        _currentSize = _initialSize;

        for (int x = 0; x < _initialSize; x++)
        {
            int offsetX = x - _centerIndex;

            for (int y = 0; y < _initialSize; y++)
            {
                int offsetY = y - _centerIndex;
                int gridX = x;
                int gridY = y;

                runner.Spawn(_tilePrefab, new Vector3(offsetX, 0f, offsetY), Quaternion.identity, null,
                    (spawnRunner, spawnedObject) =>
                    {
                        CubeColorChange colorChange = spawnedObject.GetComponent<CubeColorChange>();
                        if (colorChange != null)
                        {
                            colorChange.InitGridIndex(gridX, gridY);
                        }
                    });
            }
        }

        _isMapSpawned = true;
    }

    public void RegisterNetworkTile(CubeColorChange colorChange)
    {
        if (colorChange == null)
        {
            return;
        }

        InitializeMapStorage();

        int x = Mathf.RoundToInt(colorChange.transform.position.x) + _centerIndex;
        int y = Mathf.RoundToInt(colorChange.transform.position.z) + _centerIndex;
        if (x < 0 || x >= _initialSize || y < 0 || y >= _initialSize)
        {
            return;
        }

        GameObject tile = colorChange.gameObject;
        _mapGrid[x, y] = tile;
        _rigidbodyGrid[x, y] = tile.GetComponent<Rigidbody>();
        _colorChangeGrid[x, y] = colorChange;
        _networkObjectGrid[x, y] = colorChange.Object;
    }

    public void RequestShrink(int requestedShrinkLayers)
    {
        if (_isShrinking)
        {
            return;
        }

        if (_currentSize <= 1)
        {
            return;
        }

        int shrinkLayers = NormalizeShrinkLayers(requestedShrinkLayers);
        int nextSize = CalculateNextSize(shrinkLayers);
        if (nextSize >= _currentSize)
        {
            return;
        }

        PrepareShrink(nextSize);
    }

    // 2. 떨어트릴 둘레를 경고 색깔로 표시
    public void RequestWarning()
    {
        if (!_hasPendingTiles)
        {
            return;
        }

        for (int i = 0; i < _pendingTilesToFall.Count; i++)
        {
            TileData tileData = _pendingTilesToFall[i];
            if (tileData.Tile == null)
            {
                continue;
            }

            _onTileWarning?.Invoke(tileData.Tile);

            // 캐싱된 CubeColorChange 직접 사용 (GetComponent 재호출 제거)
            if (tileData.ColorChange != null)
            {
                tileData.ColorChange.SetWarningColor();
            }
            else
            {
                ApplyWarningColor(tileData.Tile);
            }
        }
    }

    // 3. 경고한 둘레 밑으로 떨어 뜨리기
    public void RequestFall()
    {
        if (!_hasPendingTiles)
        {
            return;
        }

        for (int i = 0; i < _pendingTilesToFall.Count; i++)
        {
            TileData tileData = _pendingTilesToFall[i];
            if (tileData.Tile == null)
            {
                continue;
            }

            _onTileFallStarted?.Invoke(tileData.Tile);

            // 캐싱된 CubeColorChange 직접 사용 (GetComponent 재호출 제거)
            if (tileData.ColorChange != null)
            {
                tileData.ColorChange.StartFall(_fallImpulse);
            }
            else
            {
                ApplyFallForce(tileData.Rigidbody);
            }
        }

        StartCoroutine(DestroyPendingTilesAfterDelay());
    }

    // 색깔 적용 함수
    public void ApplyWarningColor(GameObject tileObject)
    {
        if (tileObject == null)
        {
            return;
        }

        CubeColorChange cubeColorChange = tileObject.GetComponent<CubeColorChange>();
        if (cubeColorChange != null)
        {
            cubeColorChange.SetWarningColor();
        }
    }

    public void StartTileFall(GameObject tileObject)
    {
        if (tileObject == null)
        {
            return;
        }

        CubeColorChange cubeColorChange = tileObject.GetComponent<CubeColorChange>();
        if (cubeColorChange != null)
        {
            cubeColorChange.StartFall(_fallImpulse);
            return;
        }

        Rigidbody tileRigidbody = tileObject.GetComponent<Rigidbody>();
        if (tileRigidbody != null)
        {
            tileRigidbody.isKinematic = false;
            tileRigidbody.useGravity = true;
            tileRigidbody.AddForce(Vector3.down * _fallImpulse, ForceMode.Impulse);
        }
    }

    /// <summary>Rigidbody를 활성화하고 아래 방향 충격 적용</summary>
    private void ApplyFallForce(Rigidbody rb)
    {
        if (rb == null)
        {
            return;
        }

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.AddForce(Vector3.down * _fallImpulse, ForceMode.Impulse);
    }
    
    private void InitializeMapStorage()
    {
        if (_mapGrid != null)
        {
            return;
        }

        _mapGrid = new GameObject[_initialSize, _initialSize];
        _rigidbodyGrid = new Rigidbody[_initialSize, _initialSize];
        _colorChangeGrid = new CubeColorChange[_initialSize, _initialSize];
        _networkObjectGrid = new NetworkObject[_initialSize, _initialSize];
    }

    private void PrepareShrink(int nextSize)
    {
        _isShrinking = true;

        int previousSize = _currentSize;
        _currentSize = nextSize;

        int keepRadius = _currentSize >> 1;
        int keepMin = _centerIndex - keepRadius;
        int keepMax = _centerIndex + keepRadius;

        _pendingTilesToFall.Clear();

        int removedTileCount = Mathf.Max(0, (previousSize * previousSize) - (_currentSize * _currentSize));
        _pendingTilesToFall.Capacity = Mathf.Max(_pendingTilesToFall.Capacity, removedTileCount);

        int previousRadius = previousSize >> 1;
        int previousMin = _centerIndex - previousRadius;
        int previousMax = _centerIndex + previousRadius;

        for (int x = previousMin; x <= previousMax; x++)
        {
            for (int y = previousMin; y <= previousMax; y++)
            {
                bool isOutsideTargetArea = x < keepMin || x > keepMax || y < keepMin || y > keepMax;
                if (!isOutsideTargetArea)
                {
                    continue;
                }

                TryQueueTile(x, y, _pendingTilesToFall);
            }
        }

        _hasPendingTiles = _pendingTilesToFall.Count > 0;
        _onShrinkStarted?.Invoke(previousSize, _currentSize);
    }

    private int NormalizeShrinkLayers(int requestedShrinkLayers)
    {
        if (requestedShrinkLayers < 1)
        {
            return 1;
        }

        return requestedShrinkLayers;
    }

    private int CalculateNextSize(int shrinkLayers)
    {
        int nextSize = _currentSize - (shrinkLayers * 2);
        if (nextSize < 1)
        {
            nextSize = 1;
        }

        return nextSize;
    }

    private IEnumerator DestroyPendingTilesAfterDelay()
    {
        yield return _fallWait;

        for (int i = 0; i < _pendingTilesToFall.Count; i++)
        {
            TileData tileData = _pendingTilesToFall[i];
            if (tileData.Tile == null)
            {
                continue;
            }

            _onTileDestroyed?.Invoke(tileData.Tile);
            // 캐싱된 NetworkObject 직접 사용 (GetComponent 재호출 제거)
            NetworkObject networkObject = tileData.NetworkObject;
            if (networkObject == null && tileData.ColorChange != null)
            {
                networkObject = tileData.ColorChange.Object;
            }

            if (networkObject != null && networkObject.Runner != null && networkObject.HasStateAuthority)
            {
                networkObject.Runner.Despawn(networkObject);
            }
            else if (networkObject == null)
            {
                Destroy(tileData.Tile);
            }
        }
        
        _pendingTilesToFall.Clear();
        _hasPendingTiles = false;
        _isShrinking = false;
    }

    private void TryQueueTile(int x, int y, List<TileData> tilesToFall)
    {
        GameObject tile = _mapGrid[x, y];
        if (tile == null)
        {
            return;
        }

        tilesToFall.Add(new TileData(tile, _rigidbodyGrid[x, y], _colorChangeGrid[x, y], _networkObjectGrid[x, y]));
        _mapGrid[x, y] = null;
        _rigidbodyGrid[x, y] = null;
        _colorChangeGrid[x, y] = null;
        _networkObjectGrid[x, y] = null;
    }

    public int GetInitialSize()
    {
        return _initialSize;
    }
}
