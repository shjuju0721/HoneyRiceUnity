using UnityEngine;
using Mediapipe.Unity.Sample.FaceLandmarkDetection;

// ============================================================
//  스테이지10 준비 — 머리 각도(pitch) 실측용 진단 화면
//
//  이걸로 확인할 것 두 가지:
//   ① ⓐ와 ⓑ 중 어느 쪽이 고개 끄덕임에 제대로 반응하는가
//   ② 고개를 숙였을 때 값이 커지는가(+) 작아지는가(−)
//
//  ⚠부호를 문서나 원본 주석으로 판단하지 말 것.
//    파이썬 원본과 테스트 코드의 주석이 서로 반대로 적혀 있었고,
//    원저자도 abs()로 회피했다. 우리는 "아래로 숙일 때만" 인정해야
//    하므로 회피할 수 없다 — 반드시 눈으로 재고 넘어갈 것.
//
//  확인이 끝나면 이 스크립트는 지워도 된다.
// ============================================================
public class PitchProbe : MonoBehaviour
{
    [Header("연결")]
    public MoonClimbFaceRunner faceRunner;

    [Header("표시")]
    public bool show = true;
    public int fontSize = 20;

    // ===== 기준 잡기 (스페이스로 지금 자세를 0으로) =====
    private float baseA = 0f;
    private float baseB = 0f;
    private float baseNoseY = 0f;
    private bool hasBase = false;

    // 최대·최소를 기억해 움직임 폭을 본다
    private float minA = 999f, maxA = -999f;
    private float minB = 999f, maxB = -999f;
    private float minNose = 999f, maxNose = -999f;

    void Update()
    {
        if (faceRunner == null) return;

        // 스페이스 = 지금 자세를 기준으로
        if (Input.GetKeyDown(KeyCode.Space))
        {
            baseA = faceRunner.latestPitchA;
            baseB = faceRunner.latestPitchB;
            baseNoseY = faceRunner.latestNose.y;
            hasBase = true;

            minA = maxA = 0f;
            minB = maxB = 0f;
            minNose = maxNose = 0f;
        }

        if (!hasBase) return;

        float dA = faceRunner.latestPitchA - baseA;
        float dB = faceRunner.latestPitchB - baseB;
        float dN = faceRunner.latestNose.y - baseNoseY;

        if (dA < minA) minA = dA;
        if (dA > maxA) maxA = dA;
        if (dB < minB) minB = dB;
        if (dB > maxB) maxB = dB;
        if (dN < minNose) minNose = dN;
        if (dN > maxNose) maxNose = dN;
    }

    void OnGUI()
    {
        if (!show || faceRunner == null) return;

        GUIStyle st = new GUIStyle(GUI.skin.label);
        st.fontSize = fontSize;
        st.normal.textColor = Color.white;

        string info;

        if (!faceRunner.latestHasPitch)
        {
            info = "[pitch 진단]\n"
                 + "★각도를 못 받고 있어요.\n"
                 + "OutputFacialTransformationMatrixes가 켜졌는지,\n"
                 + "얼굴이 화면에 있는지 확인하세요.";
        }
        else if (!hasBase)
        {
            info = "[pitch 진단]\n"
                 + "고개를 편하게 세운 자세로\n"
                 + "★스페이스를 눌러 기준을 잡으세요.\n\n"
                 + "지금 값  ⓐ " + faceRunner.latestPitchA.ToString("F1")
                 + "   ⓑ " + faceRunner.latestPitchB.ToString("F1")
                 + "   코끝y " + faceRunner.latestNose.y.ToString("F3");
        }
        else
        {
            float dA = faceRunner.latestPitchA - baseA;
            float dB = faceRunner.latestPitchB - baseB;
            float dN = faceRunner.latestNose.y - baseNoseY;

            info = "[pitch 진단]  (스페이스 = 기준 다시 잡기)\n"
                 + "고개를 천천히 숙였다 젖혔다 해보세요.\n\n"
                 + "ⓐ " + dA.ToString("+0.0;-0.0")
                 + "   폭 " + minA.ToString("F1") + " ~ " + maxA.ToString("F1") + "\n"
                 + "ⓑ " + dB.ToString("+0.0;-0.0")
                 + "   폭 " + minB.ToString("F1") + " ~ " + maxB.ToString("F1") + "\n"
                 + "코끝y " + dN.ToString("+0.000;-0.000")
                 + "   폭 " + minNose.ToString("F3") + " ~ " + maxNose.ToString("F3") + "\n\n"
                 + "★숙였을 때 코끝y는 반드시 +(아래)로 커져야 정상입니다.\n"
                 + "  그때 ⓐ·ⓑ 중 크게 움직이는 쪽이 쓸 값이고,\n"
                 + "  그 부호(+인지 −인지)를 적어 두세요.";
        }

        GUI.Label(new Rect(20f, 20f, 700f, 320f), info, st);
    }
}