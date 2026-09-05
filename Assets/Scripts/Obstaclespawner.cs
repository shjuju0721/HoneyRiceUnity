using UnityEngine;

// ============================================================
//  장애물 내보내기 (위치 맞추기용 테스트 단계)
//
//  ★이 단계의 목적은 게임을 만드는 게 아니라
//    "장애물이 꼬마 앞에 알맞은 크기·높이로 서는지"를 눈으로 맞추는 것이다.
//
//  쓰는 법:
//   ① 빈 오브젝트를 만들어 이 스크립트를 붙인다 (이름 예: RunnerManager)
//   ② 프리팹 두 개를 연결한다
//   ③ Play 중에 Inspector에서 숫자를 바꾸며 눈으로 맞춘다
//   ④ 마음에 드는 값이 나오면 종이에 적어두고, 편집 모드에서 다시 넣는다
//      (★Play 중 변경은 저장되지 않는다)
// ============================================================
public class ObstacleSpawner : MonoBehaviour
{
    [Header("프리팹 연결")]
    public GameObject jumpPrefab;     // Obstacle_Jump (발판 + 가시)
    public GameObject duckPrefab;     // Obstacle_Duck (내려온 담)

    [Header("속도 (땅 배경과 같게)")]
    public float speed = 3f;

    // ============================================================
    //  ★★여기가 위치 맞추는 곳 — Play 중에 바꿔가며 눈으로 확인
    // ============================================================
    [Header("★장애물이 태어나는 자리")]
    public float spawnX = 14f;        // 화면 오른쪽 밖. 안 보이는 곳이어야 한다

    [Header("★장애물이 멈추는 자리")]
    public float stopX = -1.0f;       // ★꼬마 바로 앞. 꼬마 X보다 조금 오른쪽

    [Header("★점프 장애물 (발판 + 가시)")]
    public float jumpY = 0f;          // 위아래 위치
    public float jumpScale = 1f;      // 크기

    [Header("★숙이기 장애물 (내려온 담)")]
    public float duckY = 3f;          // ★위에서 얼마나 내려올지
                                      //   낮출수록 빈틈이 좁아진다
    public float duckScale = 1f;      // 크기

    [Header("사라지는 자리")]
    public float despawnX = -15f;

    // ============================================================
    //  ★테스트 버튼 (Play 중에 체크하면 장애물이 나온다)
    // ============================================================
    [Header("★테스트 — 체크하면 하나 내보냄")]
    public bool spawnJumpNow = false;
    public bool spawnDuckNow = false;
    public bool passNow = false;      // ★체크하면 지금 멈춰 있는 장애물을 통과시킴
    public bool clearAllNow = false;  // ★체크하면 전부 지움

    // 지금 멈춰 있는 장애물
    private Obstacle waitingOne;

    void Update()
    {
        // --- 인스펙터 체크박스를 눌렀을 때 ---
        if (spawnJumpNow)
        {
            spawnJumpNow = false;
            Spawn(Obstacle.Kind.Jump);
        }

        if (spawnDuckNow)
        {
            spawnDuckNow = false;
            Spawn(Obstacle.Kind.Duck);
        }

        if (passNow)
        {
            passNow = false;
            PassWaiting();
        }

        if (clearAllNow)
        {
            clearAllNow = false;
            ClearAll();
        }

        // --- Play 중에 숫자를 바꾸면 이미 나와 있는 장애물에도 바로 반영 ---
        // (눈으로 맞추기 편하게)
        Obstacle[] all = GetComponentsInChildren<Obstacle>();

        for (int i = 0; i < all.Length; i++)
        {
            all[i].speed = speed;
            all[i].stopX = stopX;
            all[i].despawnX = despawnX;

            Vector3 p = all[i].transform.position;

            if (all[i].kind == Obstacle.Kind.Jump)
            {
                p.y = jumpY;
                all[i].transform.localScale = Vector3.one * jumpScale;
            }
            else
            {
                p.y = duckY;
                all[i].transform.localScale = Vector3.one * duckScale;
            }

            all[i].transform.position = p;
        }
    }

    // ===== 장애물 하나 내보내기 =====
    public Obstacle Spawn(Obstacle.Kind kind)
    {
        GameObject prefab = (kind == Obstacle.Kind.Jump) ? jumpPrefab : duckPrefab;

        if (prefab == null)
        {
            Debug.LogError("[ObstacleSpawner] 프리팹이 연결되지 않았습니다: " + kind);
            return null;
        }

        float y = (kind == Obstacle.Kind.Jump) ? jumpY : duckY;
        float sc = (kind == Obstacle.Kind.Jump) ? jumpScale : duckScale;

        // ★이 오브젝트의 자식으로 만든다 (한꺼번에 관리하기 편하게)
        GameObject go = Instantiate(prefab, new Vector3(spawnX, y, 0f),
                                    Quaternion.identity, transform);

        go.transform.localScale = Vector3.one * sc;

        Obstacle ob = go.GetComponent<Obstacle>();

        if (ob == null)
        {
            // 프리팹에 Obstacle 스크립트가 없으면 붙여 준다
            ob = go.AddComponent<Obstacle>();
        }

        ob.kind = kind;
        ob.speed = speed;
        ob.stopX = stopX;
        ob.despawnX = despawnX;

        // 멈춤선에 도착하면 알려 달라고 부탁
        ob.onArrived = OnObstacleArrived;

        return ob;
    }

    // ===== 장애물이 멈춤선에 도착했을 때 =====
    void OnObstacleArrived(Obstacle ob)
    {
        waitingOne = ob;
        Debug.Log("[장애물 도착] " + ob.kind + " — " + ob.GuideText());
    }

    // ===== 멈춰 있는 장애물 통과시키기 =====
    public void PassWaiting()
    {
        if (waitingOne == null)
        {
            Debug.Log("[통과] 지금 멈춰 있는 장애물이 없습니다");
            return;
        }

        waitingOne.Pass();
        waitingOne = null;
    }

    // ===== 전부 지우기 =====
    public void ClearAll()
    {
        Obstacle[] all = GetComponentsInChildren<Obstacle>();

        for (int i = 0; i < all.Length; i++)
        {
            Destroy(all[i].gameObject);
        }

        waitingOne = null;
    }

    // ===== Scene 뷰에 기준선 그리기 (눈으로 맞추기 쉽게) =====
    void OnDrawGizmos()
    {
        // 멈춤선 = 초록 세로줄
        Gizmos.color = Color.green;
        Gizmos.DrawLine(new Vector3(stopX, -10f, 0f), new Vector3(stopX, 10f, 0f));

        // 태어나는 자리 = 파란 세로줄
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(new Vector3(spawnX, -10f, 0f), new Vector3(spawnX, 10f, 0f));

        // 사라지는 자리 = 빨간 세로줄
        Gizmos.color = Color.red;
        Gizmos.DrawLine(new Vector3(despawnX, -10f, 0f), new Vector3(despawnX, 10f, 0f));
    }
}