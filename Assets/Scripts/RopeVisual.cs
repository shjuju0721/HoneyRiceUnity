using UnityEngine;

// 밧줄의 모양을 제어하는 스크립트
// 팽팽함 정도(0~1)를 받아서 처짐과 굵기를 바꿈
public class RopeVisual : MonoBehaviour
{
    [Header("밧줄 양 끝 위치")]
    public Vector3 leftPoint = new Vector3(-3f, 0f, 0f);   // 상대편 쪽 끝
    public Vector3 rightPoint = new Vector3(3f, 0f, 0f);   // 플레이어 쪽 끝

    [Header("모양 설정")]
    public float maxSag = 1.5f;          // 완전히 느슨할 때 아래로 처지는 정도
    public float looseWidth = 0.12f;     // 느슨할 때 굵기
    public float tightWidth = 0.22f;     // 팽팽할 때 굵기

    [Header("색")]
    public Color looseColor = new Color(0.55f, 0.4f, 0.25f);  // 느슨할 때 (연한 갈색)
    public Color tightColor = new Color(0.9f, 0.75f, 0.3f);   // 팽팽할 때 (밝은 금색)

    private LineRenderer line;   // 이 오브젝트에 붙은 LineRenderer

    // Awake = Start보다 먼저 실행됨. 컴포넌트 찾아두는 용도로 자주 씀
    void Awake()
    {
        // GetComponent = 이 오브젝트에 붙은 컴포넌트를 코드로 가져오기
        line = GetComponent<LineRenderer>();
        line.positionCount = 3;   // 점 3개 (시작-중간-끝)
    }

    // ===== 외부에서 호출하는 함수 =====
    // tension: 0 = 완전 느슨, 1 = 완전 팽팽
    public void SetTension(float tension)
    {
        // Clamp01 = 값을 0~1 범위 안으로 강제로 가둠 (안전장치)
        tension = Mathf.Clamp01(tension);

        // --- 중간점의 처짐 계산 ---
        // 팽팽할수록(tension이 1에 가까울수록) 덜 처짐
        float sag = maxSag * (1f - tension);

        // 양 끝의 중간 지점을 구하고, 거기서 아래로 sag만큼 내림
        Vector3 midPoint = (leftPoint + rightPoint) / 2f;
        midPoint.y = midPoint.y - sag;

        // --- 점 3개의 위치를 LineRenderer에 전달 ---
        line.SetPosition(0, leftPoint);
        line.SetPosition(1, midPoint);
        line.SetPosition(2, rightPoint);

        // --- 굵기 ---
        // Lerp(A, B, t) = A와 B 사이의 t 지점. 카메라 따라가기에서 쓴 것과 같음
        float width = Mathf.Lerp(looseWidth, tightWidth, tension);
        line.startWidth = width;
        line.endWidth = width;

        // --- 색 ---
        Color color = Color.Lerp(looseColor, tightColor, tension);
        line.startColor = color;
        line.endColor = color;
    }
}