using UnityEngine;
using Fusion;

public class MapPhaseController : MonoBehaviour
{
    [Header("맵 & 디텍터 컴포넌트")]
    [SerializeField] private FallOffMapShrinker _fallOffMapShrinker;
    [SerializeField] private OutskirtDetectorCreator _outskirtDetectorCreator;

    // 맵 스폰 : 맵 오브젝으들도 runner.Spawn()으로 스폰시켜야 머테리얼 변경과 같은 값을 추적 가능
    public void SpawnMap(NetworkRunner runner)
    {
        if (_fallOffMapShrinker == null) return;

        _fallOffMapShrinker.SpawnMap(runner);
    }

    // 최대 라운드 받아오기
    public int GetMaxRound()
    {
        if (_fallOffMapShrinker == null)
        {
            Debug.LogWarning("MapPhaseController에 FallOffMapShrinker가 할당되지 않음");
            return 0;
        }

        return _fallOffMapShrinker.GetInitialSize() / 2;
    }

    // 외곽 빨간색으로 만들어서 경고
    public void ApplyOutskirtWarning(int currentRound)
    {
        if (_fallOffMapShrinker == null) return;

        _fallOffMapShrinker.RequestShrink();
        _fallOffMapShrinker.RequestWarning();
    }

    // 빨간색으로 만든 외곽 추락시키기
    public void ApplyFallMap()
    {
        if (_fallOffMapShrinker == null) return;

        _fallOffMapShrinker.RequestFall();
    }

    // 추락 감지 지점 좁히기
    public void ApplyOutskirtDetectorRound(int round)
    {
        if (_outskirtDetectorCreator == null) return;

        _outskirtDetectorCreator.ApplyRound(round);
        _outskirtDetectorCreator.ApplyCurrentRound();
    }
}
