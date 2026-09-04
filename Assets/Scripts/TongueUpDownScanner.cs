using System.Collections.Generic;
using UnityEngine;
using Unity.InferenceEngine;
using Mediapipe.Unity.Sample;
using Mediapipe.Unity.Sample.FaceLandmarkDetection;

// ============================================================
//  스테이지8 「혀 위아래」 판정
//
//  하는 일:
//   ① 얼굴에서 입 둘레 31개 점을 받아 입 주변을 네모나게 오려낸다
//   ② 128×128로 줄여서 학습된 모델(tongue_model.onnx)에 넣는다
//   ③ 모델이 down / neutral / up 확률 3개를 돌려준다
//   ④ 흔들림을 잡아 "안정된 판정" 하나를 만든다
//
//  ★원본 = 파이썬 test_tongue.py + 웹 updown.js.
//    자르는 방법은 파이썬 그대로, 판정 안정화는 웹 값을 따랐다.
//    (웹 값은 사용자 실테스트 3차례로 겨우 맞춘 것 — 함부로 되돌리지 말 것)
// ============================================================
public class TongueUpDownScanner : MonoBehaviour
{
    [Header("연결")]
    public MoonClimbFaceRunner faceRunner;    // 얼굴 점을 주는 곳
    public ModelAsset modelAsset;             // ★Assets에 넣은 tongue_model.onnx를 여기 끌어다 놓기

    // ===== ★★좌표 맞추기 (실측으로 확정됨 — 2026-09-04) =====
    // 웹캠 그림과 얼굴 점의 좌표계가 어긋나면 입이 아닌 엉뚱한 곳을 오려낸다.
    // ★확정값: flipX = 켬 / flipY = 끔 / flipCropV = 끔
    //   (진단 창에 입이 가운데 나오는 것으로 확인. 바꾸지 말 것)
    [Header("★★좌표 맞추기")]
    public bool flipX = true;        // 얼굴 점의 좌우를 뒤집을지
    public bool flipY = false;       // 얼굴 점의 위아래를 뒤집을지
    public bool flipCropV = false;   // ★오려낸 그림이 거꾸로 보이면 이걸 켤 것
                                     //   (위아래가 뒤집히면 up/down이 정반대로 나온다!)

    [Header("자르는 방법 (파이썬과 동일 — 바꾸지 말 것)")]
    public float margin = 1.4f;      // 입 네모를 이만큼 넉넉하게 키워서 자름
    public int imgSize = 128;        // 모델이 받는 사진 크기

    // ============================================================
    //  ★입 벌림 게이트
    //  웹에서 "입 안 벌려도 판정돼서 캐릭터가 멋대로 움직인다"는
    //  피드백으로 넣은 안전장치. 스테이지6·7과 같은 0.55.
    //  ⚠제거 금지 — 입을 다문 채 나온 모델 출력은 전부 믿을 수 없다.
    // ============================================================
    [Header("★입 벌림 게이트")]
    public float jawMin = 0.55f;
    public bool useJawGate = true;

    // ============================================================
    //  ★판정 안정화 — 위/아래를 다르게 본다 (웹 updown.js와 동일)
    //
    //  왜 다르냐: "아래 판정이 느리다"는 실테스트 피드백 때문.
    //  아래(down)는 원래 모델이 약하게 잡는다.
    //  실측에서도 up 98.4% / neutral 97.2% / down 85.8%로 아래가 제일 낮았다.
    //  그래서 아래만 문턱을 낮추고 표도 적게 요구한다.
    // ============================================================
    [Header("★판정 안정화 (웹 updown.js와 동일 — 함부로 되돌리지 말 것)")]
    public float confUp = 40f;       // 위: 이 % 넘게 확신해야 한 표로 인정
    public float confDown = 30f;     // ★아래: 더 낮게 (완화)
    public float confNeutral = 40f;  // 가운데
    public int smoothWindow = 7;     // 최근 7번의 판정을 모아 다수결
    public int voteUp = 5;           // 위: 7표 중 5표 필요
    public int voteDown = 4;         // ★아래: 7표 중 4표면 됨 (완화)

    [Header("속도 조절")]
    public int runEveryNFrames = 2;  // 몇 프레임마다 한 번 모델을 돌릴지
                                     //   웹의 UD_INFER_EVERY(2)와 같음

    // ===== 바깥에서 읽어가는 결과 =====
    [Header("판정 결과")]
    public string stableLabel = "neutral";   // ★게임에서 실제로 쓸 값
    public string rawLabel = "-";            // 이번 프레임의 날것 판정
    public float rawConfidence = 0f;         // 그 확신도 (%)
    public float pDown = 0f;                 // down 확률 (%)
    public float pNeutral = 0f;              // neutral 확률 (%)
    public float pUp = 0f;                   // up 확률 (%)
    public bool hasFace = false;
    public bool jawOpenEnough = false;       // 입을 충분히 벌렸는가
    public string noticeText = "";           // ★게임 화면에 띄울 안내 문구

    // ===== ★진단 표시 =====
    [Header("★진단 표시")]
    public bool showDebug = true;            // 오려낸 그림을 화면 왼쪽 위에 띄움
    public int debugViewSize = 200;
    public Vector2 debugViewPos = new Vector2(10f, 10f);

    // ===== 내부 작업용 =====
    private Worker worker;                   // 모델을 돌리는 일꾼
    private Texture2D cropTex;               // 오려낸 128×128 사진
    private float[] inputData;               // 모델에 넣을 숫자 다발
    private Queue<string> recentPreds = new Queue<string>();   // 최근 판정들
    private int frameCounter = 0;
    private int dbgX1, dbgY1, dbgX2, dbgY2;  // 오려낸 자리 (진단 글자용)
    private int dbgCropW = 0;                // 오려낸 원본 크기 (카메라 거리 확인용)

    // ============================================================
    //  바깥에서 쓰는 함수
    // ============================================================

    // 지금 방향을 숫자로: +1 = 위 / −1 = 아래 / 0 = 없음
    // ★게임 스크립트는 이걸 쓰면 편하다
    public int Direction()
    {
        if (stableLabel == "up") return 1;
        if (stableLabel == "down") return -1;
        return 0;
    }

    // 지금 판정할 수 있는 상태인가 (얼굴 있고, 입 벌렸고, 모델 준비됨)
    public bool CanMeasure()
    {
        return hasFace && jawOpenEnough && worker != null;
    }

    // 게임 화면에 띄울 안내 문구
    public string NoticeText()
    {
        return noticeText;
    }

    // ============================================================
    //  준비
    // ============================================================
    void Start()
    {
        if (modelAsset == null)
        {
            Debug.LogError("[TongueUpDown] 모델이 연결되지 않았습니다. " +
                           "Inspector의 Model Asset 칸에 tongue_model.onnx를 끌어다 놓으세요.");
            return;
        }

        // ONNX 파일을 실행할 수 있는 형태로 불러오기
        Model runtimeModel = ModelLoader.Load(modelAsset);

        // 일꾼 만들기. CPU로 돌린다 (모델이 작아서 충분하고, 기기를 안 가림)
        worker = new Worker(runtimeModel, BackendType.CPU);

        // 사진 담을 곳과 숫자 다발 미리 만들어두기 (매 프레임 새로 만들면 느려짐)
        cropTex = new Texture2D(imgSize, imgSize, TextureFormat.RGB24, false);
        inputData = new float[imgSize * imgSize * 3];

        Debug.Log("[TongueUpDown] 모델 준비 완료");
    }

    // ============================================================
    //  매 프레임
    // ============================================================
    void Update()
    {
        // --- 얼굴이 없으면 아무것도 안 함 ---
        if (faceRunner == null || !faceRunner.latestHasMouth)
        {
            hasFace = false;
            jawOpenEnough = false;
            ResetJudge("얼굴이 화면에 보이게 앉아 주세요");
            return;
        }

        hasFace = true;

        // --- ★입 벌림 게이트 ---
        // 입을 다문 채 나온 모델 출력은 전부 무시한다.
        jawOpenEnough = !useJawGate || (faceRunner.latestJawOpen >= jawMin);

        if (!jawOpenEnough)
        {
            ResetJudge("입을 아~ 벌리면 혀를 봐요");
            return;
        }

        // --- 속도 조절: N프레임마다 한 번만 모델을 돌린다 ---
        frameCounter++;

        if (frameCounter < runEveryNFrames)
        {
            return;
        }

        frameCounter = 0;

        // --- ① 입 주변을 오려서 128×128 사진 만들기 ---
        if (!MakeCrop())
        {
            return;
        }

        // --- ★카메라 거리 안내 ---
        // 오려낸 그림이 128보다 작으면 억지로 늘린 것이라 흐려진다.
        // 들어갈 땐 128, 나갈 땐 140 (경계에서 깜빡이지 않게 — 웹과 같은 방식)
        if (dbgCropW < 128)
        {
            noticeText = "카메라에 조금 더 가까이 와 주세요";
        }
        else if (dbgCropW >= 140)
        {
            noticeText = "";
        }

        // --- ② 모델에 넣고 답 받기 ---
        RunModel();

        // --- ③ 흔들림 잡기 ---
        Stabilize();
    }

    // 판정을 중립으로 되돌리고 안내 문구를 바꾼다
    void ResetJudge(string notice)
    {
        recentPreds.Clear();
        stableLabel = "neutral";
        rawLabel = "-";
        rawConfidence = 0f;
        noticeText = notice;
    }

    // ============================================================
    //  ① 입 주변 오려내기
    //     파이썬 test_tongue.py의 자르는 계산을 그대로 옮긴 부분
    // ============================================================
    bool MakeCrop()
    {
        var source = ImageSourceProvider.ImageSource;

        if (source == null)
        {
            return false;
        }

        Texture src = source.GetCurrentTexture();

        if (src == null)
        {
            return false;
        }

        int vw = src.width;
        int vh = src.height;

        var pts = faceRunner.latestMouth31;

        // --- 31개 점을 픽셀 좌표로 바꾸면서 가장 바깥 값 찾기 ---
        // ★y는 "위가 0"인 기준으로 다룬다 (파이썬과 같게)
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        for (int i = 0; i < 31; i++)
        {
            float nx = pts[i].x;
            float ny = pts[i].y;

            if (flipX) nx = 1f - nx;
            if (flipY) ny = 1f - ny;

            float px = nx * vw;
            float py = ny * vh;

            if (px < minX) minX = px;
            if (px > maxX) maxX = px;
            if (py < minY) minY = py;
            if (py > maxY) maxY = py;
        }

        // --- 네모의 한가운데와 크기 ---
        float cx = (minX + maxX) * 0.5f;
        float cy = (minY + maxY) * 0.5f;
        float boxW = maxX - minX;
        float boxH = maxY - minY;

        // ★가로세로 중 "큰 쪽"을 기준으로 정사각형을 만든다 (파이썬과 동일)
        float half = Mathf.Max(boxW, boxH) * margin * 0.5f;

        int x1 = Mathf.Max(Mathf.RoundToInt(cx - half), 0);
        int y1 = Mathf.Max(Mathf.RoundToInt(cy - half), 0);
        int x2 = Mathf.Min(Mathf.RoundToInt(cx + half), vw);
        int y2 = Mathf.Min(Mathf.RoundToInt(cy + half), vh);

        int cw = x2 - x1;
        int ch = y2 - y1;

        if (cw < 8 || ch < 8)
        {
            return false;   // 너무 작으면 검사 안 함
        }

        dbgX1 = x1; dbgY1 = y1; dbgX2 = x2; dbgY2 = y2;
        dbgCropW = cw;

        // --- 오려내면서 동시에 128×128로 줄이기 ---
        // Graphics.Blit에 배율·위치를 주면 "잘라서 늘리기"를 한 번에 해준다.
        // ★주의: 여기서 쓰는 좌표(uv)는 "아래가 0"이라 y를 뒤집어 계산해야 한다.
        float u0 = (float)x1 / vw;
        float uW = (float)cw / vw;
        float v0 = 1f - (float)y2 / vh;
        float vH = (float)ch / vh;

        RenderTexture rt = RenderTexture.GetTemporary(imgSize, imgSize, 0);
        Graphics.Blit(src, rt, new Vector2(uW, vH), new Vector2(u0, v0));

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        cropTex.ReadPixels(new Rect(0, 0, imgSize, imgSize), 0, 0);
        cropTex.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        // --- 사진을 모델이 먹을 수 있는 숫자 다발로 바꾸기 ---
        // 모델은 "맨 윗줄부터" 차례로 들어오길 기대한다.
        // 유니티 사진은 "맨 아랫줄"이 0번이라 뒤집어서 넣는다.
        Color32[] px32 = cropTex.GetPixels32();
        int k = 0;

        for (int row = 0; row < imgSize; row++)
        {
            // row 0 = 그림의 맨 윗줄
            int srcRow = flipCropV ? row : (imgSize - 1 - row);

            for (int col = 0; col < imgSize; col++)
            {
                Color32 c = px32[srcRow * imgSize + col];

                // ★0~255 값을 그대로 넣는다.
                //   -1~1로 바꾸는 계산은 모델 안에 들어 있다.
                inputData[k++] = c.r;
                inputData[k++] = c.g;
                inputData[k++] = c.b;
            }
        }

        return true;
    }

    // ============================================================
    //  ② 모델 돌리기
    // ============================================================
    void RunModel()
    {
        if (worker == null)
        {
            return;
        }

        // 모양 = (사진 1장, 세로 128, 가로 128, 색 3개)
        using (Tensor<float> input = new Tensor<float>(
                   new TensorShape(1, imgSize, imgSize, 3), inputData))
        {
            worker.Schedule(input);

            Tensor<float> output = worker.PeekOutput() as Tensor<float>;

            if (output == null)
            {
                return;
            }

            float[] probs = output.DownloadToArray();

            if (probs == null || probs.Length < 3)
            {
                return;
            }

            // ★순서는 알파벳순: [0]=down, [1]=neutral, [2]=up
            pDown = probs[0] * 100f;
            pNeutral = probs[1] * 100f;
            pUp = probs[2] * 100f;

            // 가장 확률이 높은 것 고르기
            int best = 0;

            if (probs[1] > probs[best]) best = 1;
            if (probs[2] > probs[best]) best = 2;

            rawLabel = LabelOf(best);
            rawConfidence = probs[best] * 100f;
        }
    }

    // ============================================================
    //  ③ 흔들림 잡기 — 위/아래를 다르게 본다 (웹 updown.js와 동일)
    // ============================================================
    void Stabilize()
    {
        // --- 방향마다 다른 확신 문턱 ---
        float threshold;

        if (rawLabel == "down") threshold = confDown;        // ★아래는 관대하게
        else if (rawLabel == "up") threshold = confUp;
        else threshold = confNeutral;

        // 문턱을 넘은 판정만 줄에 넣는다 (애매한 건 버림)
        if (rawConfidence >= threshold)
        {
            recentPreds.Enqueue(rawLabel);
        }

        while (recentPreds.Count > smoothWindow)
        {
            recentPreds.Dequeue();
        }

        if (recentPreds.Count == 0)
        {
            stableLabel = "neutral";
            return;
        }

        // --- 표 세기 ---
        int downCount = 0, upCount = 0;

        foreach (string s in recentPreds)
        {
            if (s == "down") downCount++;
            else if (s == "up") upCount++;
        }

        // --- ★아래를 먼저 본다 (표를 적게 요구하므로) ---
        if (downCount >= voteDown)
        {
            stableLabel = "down";
        }
        else if (upCount >= voteUp)
        {
            stableLabel = "up";
        }
        else
        {
            stableLabel = "neutral";
        }
    }

    string LabelOf(int i)
    {
        if (i == 0) return "down";
        if (i == 1) return "neutral";
        return "up";
    }

    // ============================================================
    //  ★진단 창 — 모델이 실제로 보고 있는 그림을 그대로 띄운다
    // ============================================================
    void OnGUI()
    {
        if (!showDebug || cropTex == null)
        {
            return;
        }

        Rect box = new Rect(debugViewPos.x, debugViewPos.y, debugViewSize, debugViewSize);

        // 오려낸 그림 (테두리는 Box로 그린다)
        GUI.Box(new Rect(box.x - 3, box.y - 3, box.width + 6, box.height + 6), GUIContent.none);
        GUI.DrawTexture(box, cropTex, ScaleMode.StretchToFill, false);

        GUIStyle st = new GUIStyle(GUI.skin.label);
        st.fontSize = 15;
        st.normal.textColor = Color.white;

        float jaw = (faceRunner != null) ? faceRunner.latestJawOpen : 0f;

        string info =
            "★모델이 보는 그림 (입이 가운데 나와야 정상)\n" +
            "STABLE : " + stableLabel +
            (jawOpenEnough ? "" : "   [입 다묾 — 판정 안 함]") + "\n" +
            "raw    : " + rawLabel + "  (" + rawConfidence.ToString("F1") + "%)\n" +
            "down " + pDown.ToString("F1") + "%  " +
            "neutral " + pNeutral.ToString("F1") + "%  " +
            "up " + pUp.ToString("F1") + "%\n" +
            "jawOpen " + jaw.ToString("F2") + " (기준 " + jawMin.ToString("F2") + ")" +
            "   원본크기 " + dbgCropW + "px\n" +
            (noticeText == "" ? "" : "안내: " + noticeText);

        GUI.Label(new Rect(box.x, box.y + box.height + 4, 470f, 130f), info, st);
    }

    void OnDestroy()
    {
        worker?.Dispose();

        if (cropTex != null)
        {
            Destroy(cropTex);
        }
    }
}