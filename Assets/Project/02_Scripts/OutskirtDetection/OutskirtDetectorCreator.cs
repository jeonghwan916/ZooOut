using UnityEngine;

// 라운드 크기에 맞춰 4방향 외곽 감지기의 위치와 Collider 크기를 갱신
public class OutskirtDetectorCreator : MonoBehaviour
{
    // 스테이지 초기 사이즈가 확정되기 전까지 사용할 임시 기본 크기
    private const float DefaultWallSize = 500.0f;

    [Header("Round")]
    [SerializeField] private int _currentRound = 1;

    // 라운드별 외곽 감지 범위. 배열 인덱스 0이 1라운드이며, Vector2Int.y는 월드 Z 크기로 사용
    [SerializeField] private Vector2Int[] _wallSizes;

    [Header("Wall Settings")]
    [SerializeField] private float _wallHeight = 10.0f;

    [SerializeField] private float _wallThickness = 0.2f;

    private OutskirtWall _northWall;
    private OutskirtWall _southWall;
    private OutskirtWall _eastWall;
    private OutskirtWall _westWall;

    private void Start()
    {
        // GameManager 연결 전 임시 생성
        CreateWalls(DefaultWallSize, DefaultWallSize);
    }

    public void ApplyCurrentRound()
    {
        ApplyRound(_currentRound);
    }

    public void ApplyRound(int round)
    {
        _currentRound = round;
        Vector2Int wallSize = _wallSizes[_currentRound - 1];

        UpdateWalls(wallSize.x, wallSize.y);
    }

    private void CreateWalls(float sizeX, float sizeZ)
    {
        _northWall = CreateWall("North Wall");
        _southWall = CreateWall("South Wall");
        _eastWall = CreateWall("East Wall");
        _westWall = CreateWall("West Wall");

        UpdateWalls(sizeX, sizeZ);
    }

    private OutskirtWall CreateWall(string wallName)
    {
        GameObject wall = new GameObject(wallName);
        wall.transform.SetParent(transform);
        wall.transform.localRotation = Quaternion.identity;

        BoxCollider wallCollider = wall.AddComponent<BoxCollider>();
        wallCollider.isTrigger = true;
        wallCollider.center = Vector3.zero;

        wall.AddComponent<OutskirtDetector>();

        return new OutskirtWall(wall.transform, wallCollider);
    }

    private void UpdateWalls(float sizeX, float sizeZ)
    {
        OutskirtDetector.ConfigureSafeArea(transform, sizeX, sizeZ);

        float halfX = sizeX * 0.5f;
        float halfZ = sizeZ * 0.5f;

        UpdateWall(_northWall, new Vector3(0.0f, 0.0f, halfZ), new Vector3(sizeX, _wallHeight, _wallThickness));
        UpdateWall(_southWall, new Vector3(0.0f, 0.0f, -halfZ), new Vector3(sizeX, _wallHeight, _wallThickness));
        UpdateWall(_eastWall, new Vector3(halfX, 0.0f, 0.0f), new Vector3(_wallThickness, _wallHeight, sizeZ));
        UpdateWall(_westWall, new Vector3(-halfX, 0.0f, 0.0f), new Vector3(_wallThickness, _wallHeight, sizeZ));
    }

    private void UpdateWall(OutskirtWall wall, Vector3 localPosition, Vector3 colliderSize)
    {
        wall.Transform.localPosition = localPosition;

        wall.Collider.size = colliderSize;
    }

    private struct OutskirtWall
    {
        public OutskirtWall(Transform transform, BoxCollider collider)
        {
            Transform = transform;
            Collider = collider;
        }

        public Transform Transform { get; }
        public BoxCollider Collider { get; }
    }
}
