using UnityEngine;

// ============================================================
//  배경 무한 스크롤 (시차 스크롤용)
//
//  하는 일:
//   같은 그림 두 장을 나란히 놓고 왼쪽으로 흘려보낸다.
//   왼쪽 그림이 화면 밖으로 완전히 나가면 오른쪽 그림 뒤로 순간이동시킨다.
//   → 끊김 없이 계속 흐르는 것처럼 보인다.
//
//  쓰는 법:
//   하늘·언덕·땅 각각에 이 스크립트를 붙이고 speedRate만 다르게 준다.
//   멀리 있는 것일수록 느리게 → 깊이감이 생긴다 (= 시차 스크롤)
//
//   하늘  speedRate 0.15
//   언덕  speedRate 0.40
//   땅    speedRate 1.00
// ============================================================
public class BackgroundScroller : MonoBehaviour
{
    [Header("흐르는 속도")]
    public float baseSpeed = 3f;      // ★기본 속도 (세 층이 같은 값을 씀)
                                      //   이 값만 바꾸면 배경 전체가 같이 빨라지고 느려진다
    public float speedRate = 1f;      // ★이 층의 배율. 멀수록 작게
                                      //   하늘 0.15 / 언덕 0.4 / 땅 1.0

    [Header("움직일지 말지")]
    public bool isRunning = true;     // ★false로 두면 멈춘다
                                      //   (장애물 앞에서 꼬마가 멈출 때 배경도 같이 멈춤)

    // ===== 내부 =====
    private SpriteRenderer[] pieces;  // 이어 붙일 그림 조각들
    private float pieceWidth = 0f;    // 그림 한 장의 가로 폭 (월드 단위)

    void Start()
    {
        // 자식으로 붙어 있는 그림 조각들을 모은다
        pieces = GetComponentsInChildren<SpriteRenderer>();

        if (pieces == null || pieces.Length < 2)
        {
            Debug.LogError("[BackgroundScroller] " + gameObject.name +
                           " : 자식으로 그림 조각이 2개 이상 있어야 합니다. " +
                           "같은 그림을 두 장 나란히 놓아 주세요.");
            return;
        }

        // 그림 한 장의 실제 가로 폭 재기
        // ★bounds = 화면에 실제로 보이는 크기. Scale을 키워도 알아서 반영된다
        pieceWidth = pieces[0].bounds.size.x;

        if (pieceWidth <= 0.01f)
        {
            Debug.LogError("[BackgroundScroller] " + gameObject.name +
                           " : 그림 폭을 못 쟀습니다. Sprite가 비어 있지 않은지 확인하세요.");
        }
    }

    void Update()
    {
        if (!isRunning || pieces == null || pieceWidth <= 0.01f)
        {
            return;
        }

        // --- ① 조각들을 왼쪽으로 옮긴다 ---
        float move = baseSpeed * speedRate * Time.deltaTime;

        for (int i = 0; i < pieces.Length; i++)
        {
            pieces[i].transform.position += Vector3.left * move;
        }

        // --- ② 가장 오른쪽 끝이 어디인지 찾는다 ---
        float rightMost = float.MinValue;

        for (int i = 0; i < pieces.Length; i++)
        {
            float x = pieces[i].transform.position.x;

            if (x > rightMost) rightMost = x;
        }

        // --- ③ 왼쪽으로 완전히 사라진 조각을 맨 뒤로 보낸다 ---
        // 기준: 그림의 오른쪽 끝이 카메라 왼쪽 밖으로 나갔을 때
        float camLeft = GetCameraLeftEdge();

        for (int i = 0; i < pieces.Length; i++)
        {
            Transform t = pieces[i].transform;

            // 이 조각의 오른쪽 끝 위치
            float myRightEdge = t.position.x + pieceWidth * 0.5f;

            if (myRightEdge < camLeft)
            {
                // 맨 오른쪽 조각 뒤에 딱 붙여 놓는다
                Vector3 p = t.position;
                p.x = rightMost + pieceWidth;
                t.position = p;

                rightMost = p.x;   // 방금 옮긴 게 새 오른쪽 끝
            }
        }
    }

    // ===== 카메라 화면의 왼쪽 끝 x좌표 =====
    float GetCameraLeftEdge()
    {
        Camera cam = Camera.main;

        if (cam == null)
        {
            return -20f;   // 카메라를 못 찾으면 넉넉히 왼쪽
        }

        // orthographicSize = 화면 세로 절반 크기
        // 가로 절반 = 세로 절반 × 화면 비율
        float halfW = cam.orthographicSize * cam.aspect;

        return cam.transform.position.x - halfW;
    }

    // ===== 바깥에서 부르는 함수 =====

    // 배경 흐름 멈추기 / 다시 흐르게 하기
    // ★장애물 앞에서 꼬마가 멈출 때 게임 스크립트가 부른다
    public void SetRunning(bool run)
    {
        isRunning = run;
    }
}