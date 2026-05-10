using UnityEngine;
using System.Collections.Generic;
using Fusion;

public class RoundPhysicsMonitor : MonoBehaviour
{
    // 매 라운드마다 오브젝트들의 물리 움직임을 검사하는 스크립트
    
    [Header("정지 감지 설정 수치")]
    [SerializeField] private float _stopVelocityThreshold = 0.05f;
    [SerializeField] private float _stopAngularThreshold = 0.05f;

    public void RestoreSurvivorPlayerFreeze(IEnumerable<KeyValuePair<PlayerRef, NetworkObject>> survivalCharacters)
    {
        foreach (var kvp in survivalCharacters)
        {
            NetworkObject netObj = kvp.Value;
            if (netObj == null)
            {
                continue;
            }

            Rigidbody rb = netObj.GetComponent<Rigidbody>();
            OutskirtDetector.RestorePlayerFreeze(rb);
        }
    }

    public bool AreAllSurvivorsStopped(int aliveCount, IEnumerable<KeyValuePair<PlayerRef, NetworkObject>> survivalCharacters)
    {
        // 생존자가 한명도 없다면 이 스크립트는 실행되징 않음
        if (aliveCount <= 0)
        {
            return true;
        }

        foreach (var kvp in survivalCharacters)
        {
            NetworkObject netObj = kvp.Value;
            if (netObj == null)
            {
                continue;
            }

            Rigidbody rb = netObj.GetComponent<Rigidbody>();
            if (rb == null)
            {
                continue;
            }

            float linearSpeed = rb.linearVelocity.magnitude;   // 현재 이동 속도
            float angularSpeed = rb.angularVelocity.magnitude; // 현재 회전 속도

            bool isLinearStopped  = linearSpeed  <= _stopVelocityThreshold; // 이동이 거의 멈췄는지
            bool isAngularStopped = angularSpeed <= _stopAngularThreshold;  // 회전이 거의 멈췄는지

            bool isStopped = isLinearStopped && isAngularStopped; // 둘 다 멈춰야 진짜 멈춘 것

            if (!isStopped)
            {
                return false; // 하나라도 안 멈춘 상황이면 false 보내보리기
            }

            StopRigidbody(rb);
        }

        return true;
    }

    private void StopRigidbody(Rigidbody rb)
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.Sleep();
    }
}
