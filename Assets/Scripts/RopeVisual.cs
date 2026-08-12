using UnityEngine;

// 밧줄의 모양을 제어하는 스크립트
// 여러 개의 점을 곡선으로 배치해서 자연스러운 처짐을 만듦
public class RopeVisual : MonoBehaviour
{
    [Header("밧줄 양 끝 위치")]
    public Vector3 leftPoint = new Vector3(-2.5f, 0f, 0f);
    public Vector3 rightPoint = new Vector3(2.5f, 0f, 0f);

    [Header("모양 설정")]
    public int pointCount = 20;          // 밧줄을 이루는 점의 개수. 많을수록 부드러움
    public float maxSag = 1.5f;          // 완전히 느슨할 때 아래로 처지는 정도
    public float pullDistance = 1.2f;    // 팽팽할 때 끝점이 당겨지는 거리
    public float looseWidth = 0.12f;
    public float tightWidth = 0.22f;

    [Header("부드러움")]
    public float smoothSpeed = 8f;       // 클수록 빠릿, 작을수록 부드러움

    [Header("색")]
    public Color looseColor = new Color(0.55f, 0.4f, 0.25f);
    public Color tightColor = new Color(0.9f, 0.75f, 0.3f);

    private LineRenderer line;
    private float currentTension = 0f;   // 화면에 실제로 그려지는 팽팽함 (부드럽게 따라감)
    private float targetTension = 0f;    // 목표 팽팽함 (외부에서 지정한 값)

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.positionCount = pointCount;   // 점 개수를 코드에서 지정
    }

    // ===== 외부에서 호출: 목표값만 저장 =====
    public void SetTension(float tension)
    {
        targetTension = Mathf.Clamp01(tension);
    }

    // ===== 매 프레임 실제 모양을 그림 =====
    void Update()
    {
        // 현재값이 목표값을 부드럽게 따라감 (카메라 따라가기와 같은 원리)
        currentTension = Mathf.Lerp(currentTension, targetTension, smoothSpeed * Time.deltaTime);

        DrawRope(currentTension);
    }

    void DrawRope(float tension)
    {
        // --- 오른쪽 끝점 당기기 ---
        Vector3 pulledRight = rightPoint;
        pulledRight.x = rightPoint.x + (pullDistance * tension);

        // --- 처짐 정도 ---
        float sag = maxSag * (1f - tension);

        // --- 점을 하나씩 계산해서 배치 ---
        for (int i = 0; i < pointCount; i++)
        {
            // t = 0(왼쪽 끝) ~ 1(오른쪽 끝) 사이의 진행도
            // (pointCount - 1)로 나누는 이유: 점이 20개면 구간은 19개
            float t = (float)i / (pointCount - 1);

            // 양 끝 사이를 t 비율로 나눈 지점 (직선상의 위치)
            Vector3 point = Vector3.Lerp(leftPoint, pulledRight, t);

            // --- 처짐 곡선 계산 ---
            // 양 끝(t=0, t=1)에서는 0, 가운데(t=0.5)에서 최대가 되는 곡선
            // 4 * t * (1-t) 는 t=0.5일 때 정확히 1이 되는 포물선 공식
            float sagCurve = 4f * t * (1f - t);
            point.y = point.y - (sag * sagCurve);

            line.SetPosition(i, point);
        }

        // --- 굵기 ---
        float width = Mathf.Lerp(looseWidth, tightWidth, tension);
        line.startWidth = width;
        line.endWidth = width;

        // --- 색 ---
        Color color = Color.Lerp(looseColor, tightColor, tension);
        line.startColor = color;
        line.endColor = color;
    }

    // ===== 밧줄 오른쪽 끝 위치를 바꾸는 함수 =====
    // 선수가 늘어날 때마다 호출해서 밧줄을 늘림
    public void SetRightPoint(Vector3 newRight)
    {
        rightPoint = newRight;
    }
}