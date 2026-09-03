using UnityEngine;
using Mediapipe.Unity.Sample;
using Mediapipe.Unity.Sample.FaceLandmarkDetection;

// 웹캠 그림의 "입 안"을 들여다보고 혀 픽셀을 세는 스크립트
// ★파이썬 원본(tongue_direction_test.py)의 순서를 그대로 옮긴 것:
//   ① 입술 안쪽 20점으로 다각형 채우기(fillPoly)
//   ② 9x9 커널로 깎기(erode) = 사방 4픽셀 균등
//   ③ 세 가지 색(붉은/흰/누런)으로 혀 잡기
//   ④ 중앙선(다각형 bbox 한가운데) 기준 좌우 세기
public class TongueScanner : MonoBehaviour
{
    [Header("연결")]
    public MoonClimbFaceRunner faceRunner;

    // ===== ★★좌표 맞추기 (이게 어긋나면 엉뚱한 곳을 검사한다) =====
    [Header("★★좌표 맞추기 — 진단창을 보며 맞출 것")]
    public bool flipY = false;       // ★세로 뒤집기
                                     //   진단창이 내 입을 안 따라오고 반대로 움직이면 이 값을 바꾼다.
                                     //   ⚠원래 코드는 켜져 있었는데, 그 탓에 얼굴이 화면 위로 가면
                                     //     검사창은 아래(가슴팍)를 찍고 있었다.
    public bool flipX = false;       // 가로 뒤집기 (보통은 필요 없음)

    // ===== ★깎기(erode) 설정 =====
    [Header("입술 가장자리 깎기 (원본 erode)")]
    public int erodeRadius = 4;      // ★9x9 커널 = 사방 4픽셀. 원본 그대로.
    public int erodeRadiusMax = 10;  // 안전 상한

    // ===== ★혀 색 판정 (원본 HSV 범위 그대로) =====
    [Header("혀 색 판정")]
    public bool applyGlobalVMin = true;  // ★전역 밝기 하한을 쓸지
                                          //   원본 파이썬에는 없다.
                                          //   스테이지6 = true / 스테이지7 = false
    public int tongueVMin = 90;

    public bool countWhiteAsTongue = true;   // 흰 백태를 혀로 셀지
                                             //   스테이지6 = true / 스테이지7 = false
    public bool countYellowAsTongue = true;  // 누런 백태를 혀로 셀지
                                             //   ⚠피부색과 겹친다. 살색이 잡히면 끌 것.

    // ===== 바깥에서 읽어가는 결과 =====
    [Header("측정 결과")]
    public float ratio = 0f;
    public int mouthCount = 0;
    public int tongueCount = 0;
    public int redCount = 0;
    public int whiteCount = 0;
    public int yellowCount = 0;
    public float avgV = 0f;

    // ===== 좌우 판정용 (스테이지 7) =====
    [Header("좌우 판정 결과")]
    public int leftCount = 0;
    public int rightCount = 0;
    public float balance = 0f;       // ★원본과 동일: (왼 − 오) ÷ (왼 + 오)
    public float shift = 0f;         // 참고값 (무게중심). 판정에는 안 씀.

    [Header("좌우 판정 설정")]
    public bool mirrorLR = true;     // ★거울 보정

    // ===== ★진단 표시 =====
    [Header("★진단 표시")]
    public bool showDebug = false;
    public int debugViewSize = 260;
    public Vector2 debugViewPos = new Vector2(10f, 10f);

    private Texture2D debugTex;
    private Color32[] debugBuf;
    private bool debugReady = false;

    private float dbgCenterRatio = 0.5f;
    private int dbgBw, dbgBh;
    private int dbgErode = 0;
    private int dbgVw, dbgVh;
    private int dbgBx, dbgBy;

    // ===== 내부 작업용 =====
    private Texture2D workTex;
    private Vector2[] polygon = new Vector2[20];

    private byte[] maskRaw;
    private byte[] maskEroded;
    private byte[] maskTmp;
    private int maskW, maskH;

    void Update()
    {
        if (faceRunner == null || !faceRunner.latestHasLip)
        {
            ratio = 0f;
            balance = 0f;
            shift = 0f;
            leftCount = 0;
            rightCount = 0;
            mouthCount = 0;
            tongueCount = 0;
            debugReady = false;
            return;
        }

        Scan();
    }

    void Scan()
    {
        var source = ImageSourceProvider.ImageSource;

        if (source == null) return;

        Texture src = source.GetCurrentTexture();

        if (src == null) return;

        int vw = src.width;
        int vh = src.height;

        dbgVw = vw;
        dbgVh = vh;

        // --- ① 입술 안쪽 20점을 픽셀 좌표로 ---
        BuildPolygon(vw, vh);

        // --- ② 다각형을 감싸는 네모 ---
        float minX = polygon[0].x, maxX = polygon[0].x;
        float minY = polygon[0].y, maxY = polygon[0].y;

        for (int i = 1; i < polygon.Length; i++)
        {
            if (polygon[i].x < minX) minX = polygon[i].x;
            if (polygon[i].x > maxX) maxX = polygon[i].x;
            if (polygon[i].y < minY) minY = polygon[i].y;
            if (polygon[i].y > maxY) maxY = polygon[i].y;
        }

        int bx = Mathf.Clamp(Mathf.FloorToInt(minX), 0, vw - 1);
        int by = Mathf.Clamp(Mathf.FloorToInt(minY), 0, vh - 1);
        int bw = Mathf.Clamp(Mathf.CeilToInt(maxX) - bx, 1, vw - bx);
        int bh = Mathf.Clamp(Mathf.CeilToInt(maxY) - by, 1, vh - by);

        dbgBx = bx;
        dbgBy = by;

        if (bw < 3 || bh < 3)
        {
            ratio = 0f;
            balance = 0f;
            shift = 0f;
            leftCount = 0;
            rightCount = 0;
            debugReady = false;
            return;
        }

        // ★중앙선 = 다각형 bbox의 가로 한가운데 (원본의 mid_x)
        float midX = bx + bw * 0.5f;

        // --- ③ 마스크: fillPoly → erode ---
        EnsureMask(bw, bh);
        FillPolygonMask(bx, by, bw, bh);

        int er = Mathf.Clamp(erodeRadius, 0, erodeRadiusMax);
        int erLimit = Mathf.Min(bw, bh) / 4;
        if (er > erLimit) er = erLimit;
        if (er < 0) er = 0;

        ErodeMask(bw, bh, er);
        dbgErode = er;

        // --- ④ 그 네모만 잘라서 픽셀 읽어오기 ---
        RenderTexture rt = RenderTexture.GetTemporary(vw, vh, 0);
        Graphics.Blit(src, rt);

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        if (workTex == null || workTex.width != bw || workTex.height != bh)
        {
            if (workTex != null) Destroy(workTex);
            workTex = new Texture2D(bw, bh, TextureFormat.RGB24, false);
        }

        // ★★여기가 핵심. flipY가 켜져 있으면 세로를 뒤집어 읽는다.
        //   어긋나면 검사창이 얼굴을 안 따라오고 엉뚱한 곳(가슴팍 등)을 찍는다.
        int readY = flipY ? (vh - by - bh) : by;
        int readX = flipX ? (vw - bx - bw) : bx;

        readY = Mathf.Clamp(readY, 0, Mathf.Max(0, vh - bh));
        readX = Mathf.Clamp(readX, 0, Mathf.Max(0, vw - bw));

        workTex.ReadPixels(new Rect(readX, readY, bw, bh), 0, 0);
        workTex.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        Color32[] pixels = workTex.GetPixels32();

        if (showDebug)
        {
            PrepareDebugTexture(bw, bh);
        }

        // --- ⑤ 입 안 점만 골라 혀 색인지 세기 ---
        int mouth = 0;
        int red = 0, white = 0, yellow = 0;
        float vSum = 0f;

        int leftPix = 0;
        int rightPix = 0;

        double sumX = 0.0;
        int sumN = 0;

        for (int py = 0; py < bh; py++)
        {
            for (int px = 0; px < bw; px++)
            {
                int pi = py * bw + px;

                // ★픽셀 배열과 마스크의 짝 맞추기
                //   GetPixels32는 아래줄부터 담기므로, 마스크(위가 0)와 맞추려면 뒤집는다.
                //   ⚠flipY로 이미 뒤집어 읽었다면 여기서는 뒤집지 않는다.
                int my = flipY ? py : (bh - 1 - py);
                int mx = flipX ? (bw - 1 - px) : px;

                int maskIdx = my * bw + mx;

                bool inside = maskEroded[maskIdx] != 0;

                Color32 c = pixels[pi];

                if (showDebug && !inside)
                {
                    if (maskRaw[maskIdx] != 0)
                    {
                        SetDebugPixel(px, py, bw, 130, 40, 160);   // 깎여 나간 테두리 = 보라
                    }
                    else
                    {
                        SetDebugPixel(px, py, bw,
                            (byte)(c.r / 3), (byte)(c.g / 3), (byte)(c.b / 3));
                    }
                }

                if (!inside) continue;

                mouth++;

                float v;
                int kind = TongueColorKind(c.r, c.g, c.b, out v);
                vSum += v;

                if (kind == 1) red++;
                else if (kind == 2) white++;
                else if (kind == 3) yellow++;

                if (showDebug)
                {
                    if (kind == 1) SetDebugPixel(px, py, bw, 0, 255, 60);        // 붉은 혀 = 초록
                    else if (kind == 2) SetDebugPixel(px, py, bw, 0, 160, 255);  // 흰 백태 = 파랑
                    else if (kind == 3) SetDebugPixel(px, py, bw, 255, 200, 0);  // 누런 백태 = 노랑
                    else SetDebugPixel(px, py, bw,
                        (byte)(c.r * 0.7f), (byte)(c.g * 0.7f), (byte)(c.b * 0.7f));
                }

                // ★혀로 잡힌 점만 좌우로 나눠 센다
                //   ⚠좌우는 "마스크 좌표(mx)" 기준이어야 한다. 그래야 중앙선과 짝이 맞는다.
                if (kind != 0)
                {
                    float wx = bx + mx;

                    if (wx < midX) leftPix++;
                    else rightPix++;

                    sumX += wx;
                    sumN++;
                }
            }
        }

        mouthCount = mouth;
        redCount = red;
        whiteCount = white;
        yellowCount = yellow;
        tongueCount = red + white + yellow;
        avgV = mouth > 0 ? vSum / mouth : 0f;
        ratio = mouth > 0 ? (float)tongueCount / mouth : 0f;

        // --- ⑥ 좌우 결과 ---
        if (mirrorLR)
        {
            leftCount = rightPix;
            rightCount = leftPix;
        }
        else
        {
            leftCount = leftPix;
            rightCount = rightPix;
        }

        int lrSum = leftCount + rightCount;

        balance = lrSum > 0 ? (float)(leftCount - rightCount) / lrSum : 0f;

        float centerRatio = 0.5f;

        if (sumN > 0)
        {
            float meanX = (float)(sumX / sumN);
            centerRatio = (meanX - bx) / bw;

            float raw = (meanX - midX) / (bw * 0.5f);
            shift = mirrorLR ? -raw : raw;
        }
        else
        {
            shift = 0f;
        }

        // --- ★진단 그림 마무리 ---
        if (showDebug)
        {
            DrawDebugVLine(bw, bh, midX - bx, new Color32(255, 0, 200, 255));   // 분홍 = 중앙선

            if (sumN > 0)
            {
                DrawDebugVLine(bw, bh, centerRatio * bw, new Color32(255, 255, 255, 255));
            }

            debugTex.SetPixels32(debugBuf);
            debugTex.Apply();
            debugReady = true;

            dbgCenterRatio = centerRatio;
            dbgBw = bw;
            dbgBh = bh;
        }
        else
        {
            debugReady = false;
        }
    }

    // ===== 입술 안쪽 20개 점을 픽셀 좌표로 =====
    void BuildPolygon(int vw, int vh)
    {
        var lip = faceRunner.latestInnerLip;

        for (int i = 0; i < 20; i++)
        {
            polygon[i] = new Vector2(lip[i].x * vw, lip[i].y * vh);
        }
    }

    void EnsureMask(int bw, int bh)
    {
        if (maskRaw == null || maskW != bw || maskH != bh)
        {
            maskRaw = new byte[bw * bh];
            maskEroded = new byte[bw * bh];
            maskTmp = new byte[bw * bh];
            maskW = bw;
            maskH = bh;
        }
    }

    // ===== ① 다각형 채우기 (원본 cv2.fillPoly) =====
    void FillPolygonMask(int bx, int by, int bw, int bh)
    {
        for (int i = 0; i < maskRaw.Length; i++) maskRaw[i] = 0;

        int n = polygon.Length;
        float[] xs = new float[n];

        for (int y = 0; y < bh; y++)
        {
            float wy = by + y + 0.5f;
            int cnt = 0;

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float yi = polygon[i].y, yj = polygon[j].y;

                if ((yi > wy) != (yj > wy))
                {
                    float xi = polygon[i].x, xj = polygon[j].x;
                    xs[cnt++] = (xj - xi) * (wy - yi) / (yj - yi) + xi;
                }
            }

            if (cnt < 2) continue;

            for (int a = 1; a < cnt; a++)
            {
                float key = xs[a];
                int b = a - 1;

                while (b >= 0 && xs[b] > key)
                {
                    xs[b + 1] = xs[b];
                    b--;
                }

                xs[b + 1] = key;
            }

            for (int a = 0; a + 1 < cnt; a += 2)
            {
                int x0 = Mathf.CeilToInt(xs[a] - bx - 0.5f);
                int x1 = Mathf.FloorToInt(xs[a + 1] - bx - 0.5f);

                if (x0 < 0) x0 = 0;
                if (x1 > bw - 1) x1 = bw - 1;

                for (int x = x0; x <= x1; x++)
                {
                    maskRaw[y * bw + x] = 255;
                }
            }
        }
    }

    // ===== ② 깎기 (원본 cv2.erode) =====
    void ErodeMask(int bw, int bh, int r)
    {
        if (r <= 0)
        {
            System.Array.Copy(maskRaw, maskEroded, maskRaw.Length);
            return;
        }

        for (int y = 0; y < bh; y++)
        {
            int rowBase = y * bw;

            for (int x = 0; x < bw; x++)
            {
                byte keep = 255;

                for (int d = -r; d <= r; d++)
                {
                    int xx = x + d;

                    if (xx < 0 || xx >= bw || maskRaw[rowBase + xx] == 0)
                    {
                        keep = 0;
                        break;
                    }
                }

                maskTmp[rowBase + x] = keep;
            }
        }

        for (int y = 0; y < bh; y++)
        {
            for (int x = 0; x < bw; x++)
            {
                byte keep = 255;

                for (int d = -r; d <= r; d++)
                {
                    int yy = y + d;

                    if (yy < 0 || yy >= bh || maskTmp[yy * bw + x] == 0)
                    {
                        keep = 0;
                        break;
                    }
                }

                maskEroded[y * bw + x] = keep;
            }
        }
    }

    // ===== 이 색이 혀인가? (★원본 HSV 범위 그대로) =====
    // 0 = 혀 아님 / 1 = 붉은 혀 / 2 = 흰 백태 / 3 = 누런 백태
    int TongueColorKind(byte r, byte g, byte b, out float outV)
    {
        float h, s, v;
        RgbToHsv(r, g, b, out h, out s, out v);
        outV = v;

        if (applyGlobalVMin && v < tongueVMin) return 0;

        // 붉은 혀: (H 0~12 또는 160~180) + S≥60 + V≥50
        if (s >= 60f && v >= 50f && (h <= 12f || h >= 160f)) return 1;

        // 흰 백태: S≤60 + V≥130
        if (s <= 60f && v >= 130f)
        {
            if (countWhiteAsTongue) return 2;
            return 0;
        }

        // 누런 백태: H 13~35 + S 30~180 + V≥80  (★피부색과 겹침)
        if (h >= 13f && h <= 35f && s >= 30f && s <= 180f && v >= 80f)
        {
            if (countYellowAsTongue) return 3;
            return 0;
        }

        return 0;
    }

    // ===== RGB → HSV (H 0~180, S·V 0~255 = OpenCV 눈금) =====
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

        h = h / 2f;
        s = mx == 0f ? 0f : (d / mx) * 255f;
        v = mx;
    }

    // ===== ★진단 =====
    void PrepareDebugTexture(int bw, int bh)
    {
        if (debugTex == null || debugTex.width != bw || debugTex.height != bh)
        {
            if (debugTex != null) Destroy(debugTex);

            debugTex = new Texture2D(bw, bh, TextureFormat.RGB24, false);
            debugTex.filterMode = FilterMode.Point;
            debugBuf = new Color32[bw * bh];
        }

        for (int i = 0; i < debugBuf.Length; i++)
        {
            debugBuf[i] = new Color32(0, 0, 0, 255);
        }
    }

    void SetDebugPixel(int px, int py, int bw, byte r, byte g, byte b)
    {
        if (debugBuf == null) return;

        int idx = py * bw + px;

        if (idx < 0 || idx >= debugBuf.Length) return;

        debugBuf[idx] = new Color32(r, g, b, 255);
    }

    void DrawDebugVLine(int bw, int bh, float localX, Color32 col)
    {
        if (debugBuf == null) return;

        int mx = Mathf.RoundToInt(localX);

        for (int d = -1; d <= 1; d++)
        {
            int x = mx + d;

            if (x < 0 || x >= bw) continue;

            for (int y = 0; y < bh; y++)
            {
                debugBuf[y * bw + x] = col;
            }
        }
    }

    void OnGUI()
    {
        if (!showDebug || !debugReady || debugTex == null) return;

        float w = debugViewSize;
        float h = debugViewSize * ((float)dbgBh / dbgBw);

        Rect box = new Rect(debugViewPos.x, debugViewPos.y, w, h);

        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(box.x - 2, box.y - 2, box.width + 4, box.height + 4),
                        Texture2D.whiteTexture);

        // 잘라온 그림 그대로 (위아래 뒤집어 사람이 보기 편하게)
        GUI.DrawTextureWithTexCoords(box, debugTex, new Rect(0f, 1f, 1f, -1f));

        GUIStyle st = new GUIStyle(GUI.skin.label);
        st.fontSize = 14;
        st.normal.textColor = Color.white;

        string info =
            "flipY " + (flipY ? "켬" : "끔") + "   flipX " + (flipX ? "켬" : "끔") + "\n" +
            "초록=붉은혀  보라=깎인테두리  분홍선=중앙선\n" +
            "R" + redCount + " W" + whiteCount + " Y" + yellowCount +
            "  /입안 " + mouthCount + "  ratio " + ratio.ToString("F2") + "\n" +
            "★Balance " + balance.ToString("F3") + "\n" +
            "영상 " + dbgVw + "x" + dbgVh +
            "  입위치 (" + dbgBx + "," + dbgBy + ")  네모 " + dbgBw + "x" + dbgBh +
            "  깎기 " + dbgErode + "px";

        GUI.Label(new Rect(box.x, box.y + box.height + 4, 500f, 100f), info, st);
    }

    void OnDestroy()
    {
        if (workTex != null) Destroy(workTex);
        if (debugTex != null) Destroy(debugTex);
    }
}