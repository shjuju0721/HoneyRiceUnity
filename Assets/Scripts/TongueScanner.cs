using UnityEngine;
using Mediapipe.Unity.Sample;
using Mediapipe.Unity.Sample.FaceLandmarkDetection;

// 웹캠 그림의 "입 안"을 들여다보고 혀 픽셀 비율을 재는 스크립트
// 원본 = 데스크톱 파이썬 → 웹(tongue.js) → 유니티, 세 번째 이식
public class TongueScanner : MonoBehaviour
{
    [Header("연결")]
    public MoonClimbFaceRunner faceRunner;

    [Header("혀 색 판정 기준 (웹에서 검증된 값)")]
    public int tongueVMin = 90;      // ★밝기 하한. 실측으로 정한 값
                                      //   50(원본 파이썬)으로 두면 "입만 벌려도" 입 안 혀가 잡힘
    public float lipMarginRatio = 0.008f;   // 입술 가장자리를 깎을 두께(영상 세로의 0.8%)
    public float lipMarginMax = 0.15f;      // 입이 작을 땐 입 높이의 15%까지만

    // ===== 바깥에서 읽어가는 결과 =====
    public float ratio = 0f;         // 혀 비율 (혀 점 ÷ 입 안 점)
    public int mouthCount = 0;       // 입 안 점 수
    public int tongueCount = 0;      // 혀로 본 점 수
    public int redCount = 0;      // 붉은 혀로 본 점
    public int whiteCount = 0;    // 흰 백태로 본 점
    public int yellowCount = 0;   // 누런 백태로 본 점
    public float avgV = 0f;       // 입 안 평균 밝기 (진단용)

    // ===== 내부 작업용 =====
    private Texture2D workTex;       // 웹캠에서 잘라온 조각을 담을 곳
    private Vector2[] polygon = new Vector2[20];   // 깎은 입 안 다각형(픽셀 좌표)
    private float lastScanTime = -1f;

    void Update()
    {
        if (faceRunner == null || !faceRunner.latestHasLip)
        {
            ratio = 0f;
            return;
        }

        Scan();
    }

    // ===== 입 안을 들여다보고 혀 픽셀 세기 =====
    void Scan()
    {
        var source = ImageSourceProvider.ImageSource;

        if (source == null)
        {
            return;
        }

        Texture src = source.GetCurrentTexture();

        if (src == null)
        {
            return;
        }

        int vw = src.width;
        int vh = src.height;

        // --- ① 입술 가장자리를 깎은 다각형 만들기 (픽셀 좌표로) ---
        if (!BuildPolygon(vw, vh))
        {
            return;
        }

        // --- ② 다각형을 감싸는 네모 구하기 (여기만 검사 = 가볍고 빠름) ---
        float minX = polygon[0].x, maxX = polygon[0].x;
        float minY = polygon[0].y, maxY = polygon[0].y;

        for (int i = 1; i < polygon.Length; i++)
        {
            if (polygon[i].x < minX) minX = polygon[i].x;
            if (polygon[i].x > maxX) maxX = polygon[i].x;
            if (polygon[i].y < minY) minY = polygon[i].y;
            if (polygon[i].y > maxY) maxY = polygon[i].y;
        }

        int bx = Mathf.Max(0, Mathf.FloorToInt(minX));
        int by = Mathf.Max(0, Mathf.FloorToInt(minY));
        int bw = Mathf.Min(vw, Mathf.CeilToInt(maxX)) - bx;
        int bh = Mathf.Min(vh, Mathf.CeilToInt(maxY)) - by;

        if (bw < 3 || bh < 3)
        {
            ratio = 0f;
            return;   // 입을 거의 다물었으면 검사 안 함
        }

        // --- ③ 그 네모만 잘라서 픽셀 읽어오기 ---
        RenderTexture rt = RenderTexture.GetTemporary(vw, vh, 0);
        Graphics.Blit(src, rt);

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        if (workTex == null || workTex.width != bw || workTex.height != bh)
        {
            if (workTex != null) Destroy(workTex);
            workTex = new Texture2D(bw, bh, TextureFormat.RGB24, false);
        }

        // ★유니티 텍스처는 아래가 원점이라 y를 뒤집어야 함
        workTex.ReadPixels(new Rect(bx, vh - by - bh, bw, bh), 0, 0);
        workTex.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        Color32[] pixels = workTex.GetPixels32();

        // --- ④ 입 안 점만 골라 혀 색인지 세기 ---
        int mouth = 0;
        int red = 0, white = 0, yellow = 0;
        float vSum = 0f;

        for (int py = 0; py < bh; py++)
        {
            for (int px = 0; px < bw; px++)
            {
                float wx = bx + px;
                float wy = by + (bh - 1 - py);

                if (!IsInsidePolygon(wx, wy)) continue;

                mouth++;

                Color32 c = pixels[py * bw + px];

                float v;
                int kind = TongueColorKind(c.r, c.g, c.b, out v);
                vSum += v;

                if (kind == 1) red++;
                else if (kind == 2) white++;
                else if (kind == 3) yellow++;
            }
        }

        mouthCount = mouth;
        redCount = red;
        whiteCount = white;
        yellowCount = yellow;
        tongueCount = red + white + yellow;
        avgV = mouth > 0 ? vSum / mouth : 0f;
        ratio = mouth > 0 ? (float)tongueCount / mouth : 0f;
    }

    // ===== 입술 안쪽 20개 점을 픽셀 좌표로 바꾸고 가장자리 깎기 =====
    bool BuildPolygon(int vw, int vh)
    {
        var lip = faceRunner.latestInnerLip;

        // 중심과 위아래 끝 구하기
        float cx = 0f, cy = 0f;
        float minY = lip[0].y * vh, maxY = lip[0].y * vh;

        for (int i = 0; i < 20; i++)
        {
            float x = lip[i].x * vw;
            float y = lip[i].y * vh;

            polygon[i] = new Vector2(x, y);

            cx += x;
            cy += y;

            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        cx /= 20f;
        cy /= 20f;

        // 깎을 두께 정하기 (원본 파이썬의 erode를 흉내낸 것)
        float margin = vh * lipMarginRatio;
        float mouthH = maxY - minY;

        if (margin > mouthH * lipMarginMax)
        {
            margin = mouthH * lipMarginMax;
        }

        // 중심 쪽으로 margin만큼 당기기 = 입술 픽셀을 검사에서 제외
        for (int i = 0; i < 20; i++)
        {
            float dx = polygon[i].x - cx;
            float dy = polygon[i].y - cy;
            float len = Mathf.Sqrt(dx * dx + dy * dy);

            if (len <= margin)
            {
                polygon[i] = new Vector2(cx, cy);
            }
            else
            {
                float t = (len - margin) / len;
                polygon[i] = new Vector2(cx + dx * t, cy + dy * t);
            }
        }

        return true;
    }

    // ===== 점이 다각형 안에 있는가? (웹의 canvas fill 대신 직접 계산) =====
    // 표준 알고리즘: 점에서 오른쪽으로 반직선을 그어 변과 몇 번 만나는지 셈
    // 홀수 번 만나면 안, 짝수 번이면 밖
    bool IsInsidePolygon(float x, float y)
    {
        bool inside = false;
        int n = polygon.Length;

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            float xi = polygon[i].x, yi = polygon[i].y;
            float xj = polygon[j].x, yj = polygon[j].y;

            bool crosses = ((yi > y) != (yj > y))
                && (x < (xj - xi) * (y - yi) / (yj - yi) + xi);

            if (crosses)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    // ===== 이 색이 혀인가? (붉은 혀 + 흰 백태 + 누런 백태) =====
    // ★백태 있는 분도 인식되게 흰색·누런색까지 혀로 봄
    // 0 = 혀 아님 / 1 = 붉은 혀 / 2 = 흰 백태 / 3 = 누런 백태
    int TongueColorKind(byte r, byte g, byte b, out float outV)
    {
        float h, s, v;
        RgbToHsv(r, g, b, out h, out s, out v);
        outV = v;

        if (v < tongueVMin) return 0;

        if (s >= 60 && v >= 50 && (h <= 12 || h >= 160)) return 1;
        if (s <= 60 && v >= 130) return 2;
        if (h >= 13 && h <= 35 && s >= 30 && s <= 180 && v >= 80) return 3;

        return 0;
    }

    // ===== RGB → HSV =====
    // ★H는 0~180, S·V는 0~255 (OpenCV 눈금 — 원본 파이썬 숫자를 그대로 쓰려고)
    void RgbToHsv(byte r, byte g, byte b, out float h, out float s, out float v)
    {
        float mx = Mathf.Max(r, Mathf.Max(g, b));
        float mn = Mathf.Min(r, Mathf.Min(g, b));
        float d = mx - mn;

        h = 0f;

        if (d != 0f)
        {
            if (mx == r) h = 60f * (((g - b) / d) % 6f);
            else if (mx == g) h = 60f * ((b - r) / d + 2f);
            else h = 60f * ((r - g) / d + 4f);
        }

        if (h < 0f) h += 360f;

        h = h / 2f;                          // ★0~180으로
        s = mx == 0f ? 0f : (d / mx) * 255f;
        v = mx;
    }

    void OnDestroy()
    {
        if (workTex != null)
        {
            Destroy(workTex);
        }
    }
}