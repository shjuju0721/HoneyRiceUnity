using UnityEngine;

// 카메라가 플레이어를 부드럽게 따라 올라가게 하는 스크립트
public class CameraFollow : MonoBehaviour
{
    public Transform target;          // 따라갈 대상(플레이어). Inspector에서 연결
    public float followSpeed = 3f;    // 따라가는 속도. 클수록 빠릿, 작을수록 부드러움

    // ===== 매 프레임, 모든 Update가 끝난 뒤에 실행 =====
    // Update가 아니라 LateUpdate를 쓰는 이유:
    // 플레이어가 먼저 움직인 뒤에 카메라가 따라가야 화면이 안 떨림
    void LateUpdate()
    {
        // 연결 안 됐으면 아무것도 안 함 (에러 방지용 안전장치)
        if (target == null)
        {
            return;
        }

        // --- 카메라가 가야 할 목표 위치 계산 ---
        // 세로(y)만 플레이어를 따라감. 가로(x)는 고정
        // 플레이어가 지그재그로 움직이는데 카메라까지 좌우로 흔들리면 어지러움
        float targetY = target.position.y;

        // transform = 이 스크립트가 붙은 오브젝트(=카메라) 자신의 위치 정보
        // z는 -10 유지. 2D에서 카메라는 화면보다 뒤에 있어야 앞을 볼 수 있음
        Vector3 goalPosition = new Vector3(transform.position.x, targetY, transform.position.z);

        // Lerp = 현재 위치에서 목표 위치로 "조금씩" 이동 (부드러운 따라가기)
        // Time.deltaTime = 지난 프레임에서 이번 프레임까지 걸린 시간(초)
        //   → 이걸 곱해야 컴퓨터 성능이 달라도 같은 속도로 움직임
        transform.position = Vector3.Lerp(transform.position, goalPosition, followSpeed * Time.deltaTime);
    }
}