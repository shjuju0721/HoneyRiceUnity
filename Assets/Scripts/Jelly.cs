using UnityEngine;

// ============================================================
//  스테이지7 「젤리 분류」 — 젤리 한 개의 움직임
//
//  ★이 스크립트는 "껍데기(빈 오브젝트)"에 붙는다.
//    젤리 그림·애니메이터는 그 안의 자식에 있다.
//    이렇게 나눠야 애니메이션이 건드리는 크기와
//    우리가 정하는 크기가 서로 싸우지 않는다.
//
//  상태 흐름:
//   Falling  위에서 뚝 떨어짐
//     ↓
//   Bouncing 착지해서 통통
//     ↓
//   Waiting  판정을 기다림 (시간 제한 없음)
//     ↓            ↘ 틀리면 Hopping(폴짝) → 다시 Waiting
//   Flying   맞으면 그쪽 접시로 슝 (날아가며 작아짐)
//     ↓
//   Landed   접시에 도착 (끝)
// ============================================================
public class Jelly : MonoBehaviour
{
    public enum State
    {
        Falling,
        Bouncing,
        Waiting,
        Hopping,
        Flying,
        Landed
    }

    [Header("상태 (보기용)")]
    public State state = State.Falling;

    [Header("떨어지기")]
    public float fallSpeed = 14f;        // 떨어지는 빠르기
    public float bounceHeight = 0.6f;    // 착지 후 통통 높이
    public float bounceSec = 0.45f;      // 통통 지속 시간

    [Header("오답 폴짝")]
    public float hopHeight = 0.5f;       // 폴짝 높이
    public float hopSec = 0.9f;          // 폴짝 지속 시간
    public int hopCount = 2;             // 몇 번 뛸지

    [Header("접시로 날아가기")]
    public float flySec = 0.55f;         // 날아가는 시간
    public float flyArc = 1.8f;          // 포물선 높이
    public float spinDegree = 60f;       // 날아갈 때 최대 기울기(도)

    // ★어느 쪽 젤리인가: +1 왼쪽(포도) / −1 오른쪽(푸딩)
    public int correctDir = 1;

    // ===== 내부 =====
    private Vector3 landPos;
    private Vector3 flyFrom;
    private Vector3 flyTo;
    private float t = 0f;

    private float scaleNow = 1f;         // 지금 크기
    private float scaleFrom = 1f;        // 날아가기 시작할 때 크기
    private float scaleTo = 1f;          // 접시에 놓일 때 크기

    // 접시에 도착했을 때 알려주기
    public System.Action<Jelly> onArrived;

    // ===== 시작: 위에서 떨어뜨리기 =====
    public void DropFrom(Vector3 from, Vector3 to, float scale)
    {
        transform.position = from;
        landPos = to;

        scaleNow = scale;
        ApplyScale();

        state = State.Falling;
        t = 0f;

        transform.rotation = Quaternion.identity;
    }

    // ===== 정답: 접시로 날려보내기 =====
    public void FlyTo(Vector3 target, float targetScale)
    {
        flyFrom = transform.position;
        flyTo = target;

        scaleFrom = scaleNow;
        scaleTo = targetScale;

        state = State.Flying;
        t = 0f;
    }

    // ===== 오답: 폴짝폴짝 =====
    public void Hop()
    {
        if (state != State.Waiting) return;

        state = State.Hopping;
        t = 0f;
    }

    public bool IsWaiting()
    {
        return state == State.Waiting;
    }

    void ApplyScale()
    {
        transform.localScale = Vector3.one * scaleNow;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        t += dt;

        switch (state)
        {
            case State.Falling:
                UpdateFalling(dt);
                break;

            case State.Bouncing:
                UpdateBouncing();
                break;

            case State.Hopping:
                UpdateHopping();
                break;

            case State.Flying:
                UpdateFlying();
                break;
        }
    }

    // --- 떨어지는 중 ---
    void UpdateFalling(float dt)
    {
        Vector3 p = transform.position;

        // 점점 빨라지게 (중력 느낌)
        p.y -= fallSpeed * dt * (1f + t * 0.8f);

        if (p.y <= landPos.y)
        {
            transform.position = landPos;

            state = State.Bouncing;
            t = 0f;
            return;
        }

        transform.position = p;
    }

    // --- 착지 통통 (위치만 — 몸의 눌림은 애니메이션이 맡는다) ---
    void UpdateBouncing()
    {
        float p = Mathf.Clamp01(t / bounceSec);

        float damp = 1f - p;
        float h = Mathf.Abs(Mathf.Sin(p * Mathf.PI * 2f)) * bounceHeight * damp;

        transform.position = landPos + Vector3.up * h;

        if (p >= 1f)
        {
            transform.position = landPos;

            state = State.Waiting;
            t = 0f;
        }
    }

    // --- 오답 폴짝폴짝 (제자리) ---
    void UpdateHopping()
    {
        float p = Mathf.Clamp01(t / hopSec);

        float h = Mathf.Abs(Mathf.Sin(p * Mathf.PI * hopCount)) * hopHeight;

        transform.position = landPos + Vector3.up * h;

        if (p >= 1f)
        {
            transform.position = landPos;

            state = State.Waiting;   // 다시 판정 대기
            t = 0f;
        }
    }

    // --- 접시로 날아가기 ---
    void UpdateFlying()
    {
        float p = Mathf.Clamp01(t / flySec);

        float ease = 1f - (1f - p) * (1f - p);          // easeOutQuad
        Vector3 pos = Vector3.Lerp(flyFrom, flyTo, ease);

        pos.y += Mathf.Sin(p * Mathf.PI) * flyArc;       // 포물선

        transform.position = pos;

        // 날아가면서 접시에 놓일 크기로 줄어든다
        scaleNow = Mathf.Lerp(scaleFrom, scaleTo, ease);
        ApplyScale();

        // ★데굴 회전 — sin 곡선이라 착지 순간 0으로 돌아온다.
        //   t에 비례시키면 착지할 때 툭 끊긴다. (웹 검증에서 확정된 것)
        float dirSign = (flyTo.x > flyFrom.x) ? -1f : 1f;
        float ang = Mathf.Sin(p * Mathf.PI) * spinDegree * dirSign;

        transform.rotation = Quaternion.Euler(0f, 0f, ang);

        if (p >= 1f)
        {
            transform.position = flyTo;
            transform.rotation = Quaternion.identity;

            scaleNow = scaleTo;
            ApplyScale();

            state = State.Landed;

            if (onArrived != null) onArrived(this);
        }
    }
}