using System.Collections.Generic;
using UnityEngine;
using Mediapipe.Tasks.Vision.FaceLandmarker;

// MediaPipe 결과에서 원하는 블렌드셰이프 점수를 꺼내주는 도구 모음
// static 클래스 = 인스턴스를 만들지 않고 바로 함수를 부를 수 있는 유틸리티
public static class FaceBlendshapeReader
{
    // ===== 이름으로 블렌드셰이프 점수 찾기 =====
    // 예: GetScore(result, "jawOpen")  →  입 벌린 정도 0~1
    // 찾지 못하면 0을 돌려줌 (얼굴이 안 잡혔을 때 등)
    public static float GetScore(FaceLandmarkerResult result, string blendshapeName)
    {
        if (result.faceBlendshapes == null || result.faceBlendshapes.Count == 0)
        {
            return 0f;
        }

        var categories = result.faceBlendshapes[0].categories;

        if (categories == null)
        {
            return 0f;
        }

        foreach (var category in categories)
        {
            if (category.categoryName == blendshapeName)
            {
                return category.score;
            }
        }

        return 0f;
    }

    // ===== 좌우 쌍으로 된 블렌드셰이프의 평균 =====
    public static float GetAverageScore(FaceLandmarkerResult result, string nameA, string nameB)
    {
        float a = GetScore(result, nameA);
        float b = GetScore(result, nameB);
        return (a + b) / 2f;
    }

    // ===== 볼 너비 비율 계산 =====
    public static float GetCheekWidth(FaceLandmarkerResult result)
    {
        if (result.faceLandmarks == null || result.faceLandmarks.Count == 0)
        {
            return 0f;
        }

        var landmarks = result.faceLandmarks[0].landmarks;

        if (landmarks == null || landmarks.Count < 400)
        {
            return 0f;
        }

        float w1 = Distance(landmarks, 61, 135);
        float w2 = Distance(landmarks, 291, 364);
        float w3 = Distance(landmarks, 137, 366);
        float widthAvg = (w1 + w2 + w3) / 3f;

        float faceHeight = Distance(landmarks, 10, 152);

        if (faceHeight < 0.0001f)
        {
            return 0f;
        }

        return widthAvg / faceHeight;
    }

    // ===== 두 랜드마크 사이의 거리 =====
    private static float Distance(
        IReadOnlyList<Mediapipe.Tasks.Components.Containers.NormalizedLandmark> pts,
        int indexA, int indexB)
    {
        var a = pts[indexA];
        var b = pts[indexB];

        float dx = a.x - b.x;
        float dy = a.y - b.y;

        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    // ===== 입술 안쪽 둘레 20개 점 번호 =====
    // ★순서를 바꾸면 다각형이 꼬입니다
    public static readonly int[] INNER_LIP = {
        78, 95, 88, 178, 87, 14, 317, 402, 318, 324,
        308, 415, 310, 311, 312, 13, 82, 81, 80, 191
    };

    // ===== 입술 안쪽 20개 점의 좌표를 뽑아 담기 =====
    public static bool FillInnerLip(FaceLandmarkerResult result, Vector2[] dest)
    {
        if (dest == null || dest.Length < INNER_LIP.Length)
        {
            return false;
        }

        if (result.faceLandmarks == null || result.faceLandmarks.Count == 0)
        {
            return false;
        }

        var landmarks = result.faceLandmarks[0].landmarks;

        if (landmarks == null || landmarks.Count < 478)
        {
            return false;
        }

        for (int i = 0; i < INNER_LIP.Length; i++)
        {
            var p = landmarks[INNER_LIP[i]];
            dest[i] = new Vector2(p.x, p.y);
        }

        return true;
    }

    // ============================================================
    //  스테이지8(혀 위아래)용
    // ============================================================

    // ===== 입 둘레 31개 점 번호 =====
    // ★파이썬 학습·수집 코드의 MOUTH_POINTS와 순서까지 완전히 같습니다.
    public static readonly int[] MOUTH_31 = {
        61, 146,  91, 181,  84,  17, 314, 405, 321, 375,
       291, 308, 324, 318, 402, 317,  14,  87, 178,  88,
        95, 185,  40,  39,  37,   0, 267, 269, 270, 409, 415
    };

    // ===== 입 둘레 31개 점의 좌표를 뽑아 담기 =====
    public static bool FillMouth31(FaceLandmarkerResult result, Vector2[] dest)
    {
        if (dest == null || dest.Length < MOUTH_31.Length)
        {
            return false;
        }

        if (result.faceLandmarks == null || result.faceLandmarks.Count == 0)
        {
            return false;
        }

        var landmarks = result.faceLandmarks[0].landmarks;

        if (landmarks == null || landmarks.Count < 478)
        {
            return false;
        }

        for (int i = 0; i < MOUTH_31.Length; i++)
        {
            var p = landmarks[MOUTH_31[i]];
            dest[i] = new Vector2(p.x, p.y);
        }

        return true;
    }

    // ============================================================
    //  ★★ 여기부터 스테이지10(떠먹여주기)용으로 새로 추가한 부분
    //     — 턱 당기기(chin tuck) 판정에 쓴다
    // ============================================================

    // ===== 코끝 좌표 =====
    // 랜드마크 1번 = 코끝. 0~1 정규화 좌표(y는 위가 0).
    // ★고개를 숙이면 코끝이 화면에서 "아래"로 내려간다 = y가 커진다.
    //   각도만 보면 부호가 기기마다 달라 고개 젖히기도 통과하므로,
    //   각도와 코끝을 함께 봐야 "정말 숙였을 때"만 걸린다.
    public static bool GetNoseTip(FaceLandmarkerResult result, out Vector2 nose)
    {
        nose = Vector2.zero;

        if (result.faceLandmarks == null || result.faceLandmarks.Count == 0)
        {
            return false;
        }

        var landmarks = result.faceLandmarks[0].landmarks;

        if (landmarks == null || landmarks.Count < 478)
        {
            return false;
        }

        var p = landmarks[1];
        nose = new Vector2(p.x, p.y);

        return true;
    }

    // ===== 머리 위아래 각도(pitch) =====
    // ⚠★가장 조심할 곳 — 두 값을 함께 돌려주고 실측으로 고른다.
    //
    //   원본(파이썬/웹)은 행 우선(row-major) 평면 배열에서
    //   atan2(-m[2][1], m[2][2]) 로 계산했다.
    //   그런데 Unity의 Matrix4x4는 열 우선이라 숫자 인덱스를 그대로 옮기면
    //   엉뚱한 칸을 가리킨다. 그래서 이름(m21·m22)으로 접근한다.
    //
    //   플러그인이 행렬을 옮겨 담을 때 전치(행↔열)했을 가능성이 남아 있어
    //   ⓐ와 ⓑ 두 가지를 모두 계산해 둔다.
    //   ★고개를 끄덕여 보고 "제대로 반응하는 쪽"을 실측으로 고를 것.
    //   ★부호("숙이면 +인지 −인지")도 반드시 실측할 것 —
    //     원본 코드에도 주석이 서로 반대로 적혀 있었고, 원저자는
    //     abs()로 회피했다. 우리는 아래 방향만 인정해야 하므로 회피 불가.
    public static bool GetHeadPitch(FaceLandmarkerResult result,
                                    out float pitchA, out float pitchB)
    {
        pitchA = 0f;
        pitchB = 0f;

        if (result.facialTransformationMatrixes == null
            || result.facialTransformationMatrixes.Count == 0)
        {
            return false;
        }

        Matrix4x4 m = result.facialTransformationMatrixes[0];

        // ⓐ 원본 식 그대로 (행 우선이라고 보고)
        pitchA = Mathf.Atan2(-m.m21, m.m22) * Mathf.Rad2Deg;

        // ⓑ 전치된 경우 (열 우선으로 담겼다면 이쪽이 맞다)
        pitchB = Mathf.Atan2(-m.m12, m.m22) * Mathf.Rad2Deg;

        return true;
    }
}