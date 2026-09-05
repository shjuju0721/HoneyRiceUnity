using UnityEngine;

// ============================================================
//  장애물 하나의 움직임
//
//  흐름:
//   ① 화면 오른쪽 밖에서 태어난다
//   ② 왼쪽으로 흘러온다
//   ③ 정해진 자리(멈춤선)에 오면 멈춘다  → 꼬마가 혀로 답할 때까지 기다림
//   ④ 통과하면 다시 흘러간다
//   ⑤ 화면 왼쪽 밖으로 나가면 스스로 사라진다
//
//  ★배경과 같은 속도로 움직여야 땅에 붙어 있는 것처럼 보인다.
//    그래서 BackgroundScroller의 땅 속도와 맞춰 준다.
// ============================================================
public class Obstacle : MonoBehaviour
{
    // 장애물 종류
    public enum Kind
    {
        Jump,   // 점프용 (발판 + 가시)  → 혀 위
        Duck    // 숙이기용 (내려온 담)   → 혀 아래
    }

    [Header("종류")]
    public Kind kind = Kind.Jump;

    [Header("속도 (땅 배경과 같게)")]
    public float speed = 3f;          // ★BackgroundScroller의 baseSpeed × 1.0과 같은 값

    [Header("멈추는 자리")]
    public float stopX = 0f;          // ★여기까지 오면 멈춘다
                                      //   꼬마보다 조금 오른쪽에 두면 된다

    [Header("사라지는 자리")]
    public float despawnX = -15f;     // ★여기보다 왼쪽으로 가면 삭제

    // ===== 상태 =====
    public bool isMoving = true;      // 지금 흐르고 있는가
    public bool isPassed = false;     // 이미 통과했는가
    public bool hasStopped = false;   // 멈춤선에 도착했는가

    // 멈춤선에 막 도착한 순간을 게임 스크립트에 알려주는 장치
    public System.Action<Obstacle> onArrived;

    void Update()
    {
        if (!isMoving)
        {
            return;
        }

        // --- 왼쪽으로 흐르기 ---
        transform.position += Vector3.left * (speed * Time.deltaTime);

        // --- 멈춤선에 도착했나? (아직 안 멈춰봤고, 통과 전일 때만) ---
        if (!hasStopped && !isPassed && transform.position.x <= stopX)
        {
            // 정확히 멈춤선에 맞춰 세운다 (조금 지나쳐도 딱 맞게)
            Vector3 p = transform.position;
            p.x = stopX;
            transform.position = p;

            isMoving = false;
            hasStopped = true;

            // 게임 스크립트에 "도착했어요" 알리기
            if (onArrived != null)
            {
                onArrived(this);
            }

            return;
        }

        // --- 화면 왼쪽 밖으로 나갔으면 사라지기 ---
        if (transform.position.x < despawnX)
        {
            Destroy(gameObject);
        }
    }

    // ===== 바깥에서 부르는 함수 =====

    // 통과 처리 — 다시 흘러가게 한다
    // ★꼬마가 정답을 맞혔을 때 게임 스크립트가 부른다
    public void Pass()
    {
        isPassed = true;
        isMoving = true;
    }

    // 이 장애물의 정답 방향
    // +1 = 혀 위(점프) / −1 = 혀 아래(숙이기)
    public int CorrectDirection()
    {
        return (kind == Kind.Jump) ? 1 : -1;
    }

    // 안내 문구
    public string GuideText()
    {
        if (kind == Kind.Jump)
        {
            return "혀를 위로 올려 폴짝 뛰어요";
        }

        return "혀를 아래로 내려 쏙 지나가요";
    }
}