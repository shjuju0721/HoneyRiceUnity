using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  선그래프 그리기 담당 (uGUI Graphic — 이미지처럼 캔버스에 올라간다)
//
//  ★이 스크립트는 "선과 점"만 그린다. 글자(날짜·눈금)는 LineChart.cs가 붙인다.
//    Image 컴포넌트를 상속한 것처럼 Canvas 안 아무 오브젝트에 붙이면 된다.
//
//  쓰는 법:
//     chart.SetData(seriesList, pointCount);   // 값 넣기
//     chart.GetPoint(i, value)                 // i번째 값의 화면 위치 (라벨 붙일 때)
// ============================================================
[RequireComponent(typeof(CanvasRenderer))]
public class LineChartGraphic : MaskableGraphic
{
    // ===== 선 하나 =====
    public class Series
    {
        public string name;               // 범례 이름 ("왼쪽", "입술" 등)
        public List<float> values;        // 값들 (x축 순서대로)
        public Color color;               // 선 색
    }

    [Header("여백 (글자 자리)")]
    public float padLeft = 110f;      // 세로 눈금 글자 자리
    public float padRight = 20f;
    public float padTop = 20f;
    public float padBottom = 70f;    // 날짜 글자 자리

    [Header("선·점")]
    public float lineWidth = 9f;
    public float dotRadius = 13f;
    public bool dotOutline = true;           // 점에 흰 테두리
    public Color dotOutlineColor = Color.white;

    [Header("축·격자")]
    public Color axisColor = new Color(0.45f, 0.40f, 0.35f, 1f);
    public float axisWidth = 4f;
    public Color gridColor = new Color(0.45f, 0.40f, 0.35f, 0.35f);
    public int gridLines = 4;                // 가로 격자 개수 (세로 눈금 개수와 같음)

    // ===== 데이터 =====
    List<Series> seriesList = new List<Series>();
    int pointCount = 0;      // x축 칸 수 (날짜 수)
    float yMax = 1f;         // 세로축 맨 위 값 (보기 좋게 반올림한 값)

    public int PointCount { get { return pointCount; } }
    public float YMax { get { return yMax; } }
    public List<Series> SeriesList { get { return seriesList; } }

    // ------------------------------------------------------------
    //  값 넣기
    // ------------------------------------------------------------
    public void SetData(List<Series> series, int count)
    {
        seriesList = series != null ? series : new List<Series>();
        pointCount = count;

        // 세로축 위 끝 = 가장 큰 값보다 조금 위, 보기 좋은 숫자로
        float maxV = 0f;
        for (int s = 0; s < seriesList.Count; s++)
        {
            List<float> v = seriesList[s].values;
            for (int i = 0; i < v.Count; i++)
                if (v[i] > maxV) maxV = v[i];
        }
        yMax = NiceMax(maxV);

        SetVerticesDirty();   // 다시 그리라고 알림
    }

    // 최대값을 1·2·5 단위의 "보기 좋은" 숫자로 올림
    //   예) 0.87 → 1.0 / 0.23 → 0.25 / 58.9 → 60 / 7 → 8
    static float NiceMax(float v)
    {
        if (v <= 0f) return 1f;

        float raised = v * 1.15f;                             // 15% 여유
        float mag = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(raised)));   // 자릿수
        float n = raised / mag;                               // 1.0 ~ 9.99

        float nice;
        if (n <= 1f) nice = 1f;
        else if (n <= 2f) nice = 2f;
        else if (n <= 2.5f) nice = 2.5f;
        else if (n <= 4f) nice = 4f;
        else if (n <= 5f) nice = 5f;
        else if (n <= 8f) nice = 8f;
        else nice = 10f;

        return nice * mag;
    }

    // ------------------------------------------------------------
    //  좌표 계산 (LineChart가 글자를 붙일 때도 이걸 쓴다)
    //  ★돌려주는 위치는 이 RectTransform의 로컬 좌표(피벗 기준)다.
    // ------------------------------------------------------------
    Rect PlotRect()
    {
        Rect r = rectTransform.rect;
        return new Rect(r.xMin + padLeft, r.yMin + padBottom,
                        r.width - padLeft - padRight,
                        r.height - padTop - padBottom);
    }

    // i번째 칸의 x 위치 (점이 하나면 가운데)
    public float GetX(int i)
    {
        Rect p = PlotRect();
        if (pointCount <= 1) return p.xMin + p.width * 0.5f;
        return p.xMin + p.width * ((float)i / (pointCount - 1));
    }

    // 값 v의 y 위치
    public float GetY(float v)
    {
        Rect p = PlotRect();
        float t = yMax <= 0f ? 0f : Mathf.Clamp01(v / yMax);
        return p.yMin + p.height * t;
    }

    public Vector2 GetPoint(int i, float v)
    {
        return new Vector2(GetX(i), GetY(v));
    }

    // 왼쪽 축 x 위치·아래 축 y 위치 (라벨 정렬용)
    public float AxisX { get { return PlotRect().xMin; } }
    public float AxisY { get { return PlotRect().yMin; } }

    // ------------------------------------------------------------
    //  실제 그리기
    // ------------------------------------------------------------
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect p = PlotRect();

        // ① 가로 격자
        for (int g = 1; g <= gridLines; g++)
        {
            float y = p.yMin + p.height * ((float)g / gridLines);
            AddLine(vh, new Vector2(p.xMin, y), new Vector2(p.xMax, y), 2f, gridColor);
        }

        // ② 축 두 개
        AddLine(vh, new Vector2(p.xMin, p.yMin), new Vector2(p.xMax, p.yMin), axisWidth, axisColor);   // 아래
        AddLine(vh, new Vector2(p.xMin, p.yMin), new Vector2(p.xMin, p.yMax), axisWidth, axisColor);   // 왼쪽

        // ③ 선
        for (int s = 0; s < seriesList.Count; s++)
        {
            Series sr = seriesList[s];
            int n = Mathf.Min(sr.values.Count, pointCount);

            for (int i = 0; i < n - 1; i++)
            {
                // 값이 없는 날(NaN)은 선을 끊는다
                if (float.IsNaN(sr.values[i]) || float.IsNaN(sr.values[i + 1])) continue;

                AddLine(vh, GetPoint(i, sr.values[i]), GetPoint(i + 1, sr.values[i + 1]), lineWidth, sr.color);
            }
        }

        // ④ 점 (선 위에 그리려고 따로 돈다)
        for (int s = 0; s < seriesList.Count; s++)
        {
            Series sr = seriesList[s];
            int n = Mathf.Min(sr.values.Count, pointCount);

            for (int i = 0; i < n; i++)
            {
                if (float.IsNaN(sr.values[i])) continue;

                Vector2 c = GetPoint(i, sr.values[i]);
                if (dotOutline) AddCircle(vh, c, dotRadius + 3f, dotOutlineColor);
                AddCircle(vh, c, dotRadius, sr.color);
            }
        }
    }

    // 두 점 사이 굵은 선 = 사각형 하나
    static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width, Color col)
    {
        Vector2 dir = (b - a).normalized;
        Vector2 nrm = new Vector2(-dir.y, dir.x) * (width * 0.5f);   // 수직 방향

        UIVertex v = UIVertex.simpleVert;
        v.color = col;

        int start = vh.currentVertCount;

        v.position = a - nrm; vh.AddVert(v);
        v.position = a + nrm; vh.AddVert(v);
        v.position = b + nrm; vh.AddVert(v);
        v.position = b - nrm; vh.AddVert(v);

        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    // 원 = 부채꼴 삼각형 여러 개
    static void AddCircle(VertexHelper vh, Vector2 c, float r, Color col)
    {
        const int SEG = 20;

        UIVertex v = UIVertex.simpleVert;
        v.color = col;

        int center = vh.currentVertCount;
        v.position = c; vh.AddVert(v);

        for (int i = 0; i < SEG; i++)
        {
            float ang = (float)i / SEG * Mathf.PI * 2f;
            v.position = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
            vh.AddVert(v);
        }

        for (int i = 0; i < SEG; i++)
        {
            int a = center + 1 + i;
            int b = center + 1 + (i + 1) % SEG;
            vh.AddTriangle(center, a, b);
        }
    }

    // 크기가 바뀌면 다시 그린다
    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        SetVerticesDirty();
    }
}