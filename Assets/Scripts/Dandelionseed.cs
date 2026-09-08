using UnityEngine;

// ============================================================
//  날아가는 홀씨 하나
//
//  민들레에서 태어나 바람을 타고 위로·옆으로 흘러가다
//  화면 밖으로 나가면 스스로 사라진다.
//
//  ★홀씨는 무겁지 않아서 "떨어지지" 않는다.
//    위로 떠오르면서 좌우로 하늘하늘 흔들리는 게 자연스럽다.
// ============================================================
public class DandelionSeed : MonoBehaviour
{
    [Header("움직임")]
    public float speedX = 2.0f;      // 오른쪽으로 흘러가는 속도
    public float speedY = 1.2f;      // 위로 떠오르는 속도
    public float wobbleAmp = 0.35f;  // 좌우로 하늘거리는 폭
    public float wobbleSpeed = 2.0f; // 하늘거리는 빠르기
    public float spinSpeed = 40f;    // 빙글 도는 빠르기(도/초)

    [Header("사라지기")]
    public float lifeSec = 4.0f;     // 이만큼 지나면 사라짐
    public float fadeSec = 1.2f;     // 마지막에 서서히 옅어지는 시간

    // ===== 내부 =====
    private float t = 0f;            // 태어난 뒤 흐른 시간
    private float phase = 0f;        // 하늘거림의 시작 위상(홀씨마다 다르게)
    private Vector3 basePos;         // 하늘거림을 뺀 기준 위치
    private SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        phase = Random.Range(0f, 10f);
        basePos = transform.position;
    }

    // ★태어날 때 게임 스크립트가 불러 준다
    //  strength = 기침 세기(1 살살 / 2 보통 / 3 세게)
    public void Launch(int strength)
    {
        // 세게 기침일수록 더 빠르게·더 멀리 날아간다
        float boost = 1f + (strength - 1) * 0.45f;   // 1.0 / 1.45 / 1.9

        speedX = Random.Range(1.2f, 3.0f) * boost;
        speedY = Random.Range(0.8f, 2.0f) * boost;

        // 가끔 왼쪽으로 날아가는 것도 섞으면 훨씬 자연스럽다
        if (Random.value < 0.4f)
        {
            speedX = -speedX * 0.6f;
        }

        wobbleAmp = Random.Range(0.2f, 0.5f);
        wobbleSpeed = Random.Range(1.4f, 2.6f);
        spinSpeed = Random.Range(-70f, 70f);

        // 크기도 조금씩 다르게(다 똑같으면 붙여넣은 티가 난다)
        float s = Random.Range(0.7f, 1.15f);
        transform.localScale = Vector3.one * s;

        basePos = transform.position;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        t += dt;

        // --- 기준 위치를 위·옆으로 옮기고 ---
        basePos += new Vector3(speedX, speedY, 0f) * dt;

        // --- 거기에 좌우 하늘거림을 얹는다 ---
        float wob = Mathf.Sin((t + phase) * wobbleSpeed) * wobbleAmp;

        transform.position = basePos + new Vector3(wob, 0f, 0f);

        // --- 빙글 돌기 ---
        transform.Rotate(0f, 0f, spinSpeed * dt);

        // --- 마지막에 서서히 옅어지기 ---
        if (sr != null && t > lifeSec - fadeSec)
        {
            float left = Mathf.Clamp01((lifeSec - t) / fadeSec);

            Color c = sr.color;
            c.a = left;
            sr.color = c;
        }

        // --- 다 살았으면 사라지기 ---
        if (t >= lifeSec)
        {
            Destroy(gameObject);
        }
    }
}