using UnityEngine;

// 발사된 하트 하나의 움직임을 담당
public class HeartProjectile : MonoBehaviour
{
    private Vector3 targetPosition;   // 날아갈 목표 지점
    private float speed;              // 이동 속도
    private bool isLaunched = false;  // 발사됐는지

    // ===== 발사 준비: 목표와 속도를 지정받음 =====
    public void Launch(Vector3 target, float moveSpeed)
    {
        targetPosition = target;
        speed = moveSpeed;
        isLaunched = true;
    }

    void Update()
    {
        if (!isLaunched)
        {
            return;
        }

        // MoveTowards = 현재 위치에서 목표로 일정 속도로 이동
        // Lerp와 달리 속도가 일정함 (Lerp는 가까워질수록 느려짐)
        transform.position = Vector3.MoveTowards(
            transform.position, targetPosition, speed * Time.deltaTime);

        // 목표에 거의 도착했으면 사라짐
        // Distance = 두 점 사이의 거리
        if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
        {
            // Destroy = 오브젝트를 씬에서 제거
            Destroy(gameObject);
        }
    }
}