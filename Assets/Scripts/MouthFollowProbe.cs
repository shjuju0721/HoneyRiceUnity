using UnityEngine;
using Mediapipe.Unity.Sample;
using Mediapipe.Unity.Sample.FaceLandmarkDetection;

// ============================================================
//  스테이지10 준비 — "입을 따라오는 점" 좌표 맞추기
//
//  왜 필요한가:
//   숟가락이 입까지 다가오려면 랜드마크(0~1 정규화 좌표)를
//   화면 위 실제 자리로 바꿔야 한다.
//   ⚠그런데 그 변환은 웹캠 화면이 어떻게 놓였는지·뒤집혔는지에 달려 있다.
//     (스테이지7에서 검사 영역이 얼굴을 안 따라오던 것과 같은 종류의 문제)
//   그래서 점 하나로 먼저 맞춰 두고, 그 다음에 숟가락을 붙인다.
//
//  ★쓰는 법:
//   ① 빈 오브젝트에 이 스크립트를 붙이고 Face Runner에 Solution 연결
//   ② Marker 칸에 따라다닐 오브젝트(스프라이트 하나)를 연결
//   ③ 재생하고 얼굴을 좌우·위아래로 움직여 본다
//   ④ 점이 반대로 가면 flipX / flipY를 켜고 끄며 맞춘다
//   ⑤ 맞으면 그 조합을 적어 두고 숟가락에 그대로 쓴다
// ============================================================
public class MouthFollowProbe : MonoBehaviour
{
    [Header("연결")]
    public MoonClimbFaceRunner faceRunner;
    public Transform marker;              // 입을 따라다닐 물건 (스프라이트 하나면 충분)
    public Camera targetCamera;           // 비워 두면 Camera.main

    [Header("★좌표 맞추기 — 진단을 보며 맞출 것")]
    public bool flipX = false;            // 좌우가 반대면 켠다
    public bool flipY = false;            // 위아래가 반대면 켠다

    [Header("웹캠 화면 영역")]
    // ★웹캠이 화면 전체를 채우지 않으면 여기를 조절한다.
    //   0~1 비율. (0,0) = 화면 왼쪽 아래, (1,1) = 오른쪽 위.
    //   전체 화면이면 그대로 두면 된다.
    public Vector2 viewMin = new Vector2(0f, 0f);
    public Vector2 viewMax = new Vector2(1f, 1f);

    [Header("놓을 자리")]
    public float zDepth = 0f;             // 마커를 놓을 z (2D면 0 근처)
    public Vector2 offset = Vector2.zero; // 입에서 조금 떨어뜨리고 싶을 때

    [Header("따라오는 느낌")]
    public float followTau = 0.06f;       // 클수록 부드럽고 느리게 따라온다
                                          //   ⚠0이면 랜드마크 떨림이 그대로 보인다

    [Header("★진단 표시")]
    public bool showDebug = true;

    // ===== 바깥에서 읽어갈 결과 =====
    public Vector3 mouthWorld = Vector3.zero;   // 입의 월드 좌표
    public bool hasMouth = false;

    // ===== 내부 =====
    private Vector3 smoothed = Vector3.zero;
    private bool hasSmoothed = false;

    private Vector2 dbgNorm = Vector2.zero;     // 정규화 좌표 (뒤집기 전)
    private Vector2 dbgUsed = Vector2.zero;     // 뒤집기 적용 후

    void Update()
    {
        if (faceRunner == null)
        {
            hasMouth = false;
            return;
        }

        if (!faceRunner.latestHasLip)
        {
            hasMouth = false;
            return;
        }

        // --- ① 입 중심 구하기 ---
        // 입술 안쪽 20점의 평균 = 입 한가운데
        // ⚠13·14번만 쓰면 입을 벌릴 때 위아래로 출렁인다. 평균이 안정적이다.
        var lip = faceRunner.latestInnerLip;

        float sx = 0f, sy = 0f;

        for (int i = 0; i < 20; i++)
        {
            sx += lip[i].x;
            sy += lip[i].y;
        }

        float nx = sx / 20f;
        float ny = sy / 20f;

        dbgNorm = new Vector2(nx, ny);

        // --- ② 뒤집기 적용 ---
        if (flipX) nx = 1f - nx;
        if (flipY) ny = 1f - ny;

        dbgUsed = new Vector2(nx, ny);

        // --- ③ 화면 좌표로 ---
        // ★랜드마크의 y는 "위가 0"이고, 유니티 화면 좌표는 "아래가 0"이다.
        //   그래서 y를 한 번 뒤집어 준다. (flipY는 그 위에 더하는 보정)
        float vx = Mathf.Lerp(viewMin.x, viewMax.x, nx);
        float vy = Mathf.Lerp(viewMin.y, viewMax.y, 1f - ny);

        Camera cam = targetCamera != null ? targetCamera : Camera.main;

        if (cam == null)
        {
            hasMouth = false;
            return;
        }

        // 카메라에서 얼마나 떨어진 곳에 놓을지
        float dist = Mathf.Abs(zDepth - cam.transform.position.z);

        Vector3 world = cam.ViewportToWorldPoint(new Vector3(vx, vy, dist));
        world.z = zDepth;
        world.x += offset.x;
        world.y += offset.y;

        mouthWorld = world;
        hasMouth = true;

        // --- ④ 부드럽게 따라오기 ---
        if (!hasSmoothed)
        {
            smoothed = world;
            hasSmoothed = true;
        }
        else
        {
            float k = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.001f, followTau));
            smoothed = Vector3.Lerp(smoothed, world, k);
        }

        if (marker != null)
        {
            marker.position = smoothed;
        }
    }

    void OnGUI()
    {
        if (!showDebug) return;

        GUIStyle st = new GUIStyle(GUI.skin.label);
        st.fontSize = 18;
        st.normal.textColor = Color.white;

        string info;

        if (!hasMouth)
        {
            info = "[입 따라가기 진단]\n얼굴(입)을 못 찾고 있어요.";
        }
        else
        {
            info = "[입 따라가기 진단]   flipX " + (flipX ? "켬" : "끔")
                 + "   flipY " + (flipY ? "켬" : "끔") + "\n"
                 + "랜드마크  (" + dbgNorm.x.ToString("F3") + ", " + dbgNorm.y.ToString("F3") + ")\n"
                 + "뒤집은 뒤 (" + dbgUsed.x.ToString("F3") + ", " + dbgUsed.y.ToString("F3") + ")\n"
                 + "월드 좌표 (" + mouthWorld.x.ToString("F2") + ", " + mouthWorld.y.ToString("F2") + ")\n\n"
                 + "★얼굴을 좌우·위아래로 움직여 점이 입을 따라오는지 보세요.\n"
                 + "  반대로 가면 flipX / flipY를 바꿔 가며 맞추세요.";
        }

        GUI.Label(new Rect(20f, 20f, 700f, 220f), info, st);
    }
}