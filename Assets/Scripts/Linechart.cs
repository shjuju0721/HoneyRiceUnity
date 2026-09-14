using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

// ============================================================
//  대시보드 선그래프 (데이터 → 그래프 + 글자)
//
//  RecordStore.DailyMax()에서 날짜별 최고값을 받아
//  LineChartGraphic에 넘기고, 날짜·눈금·범례·판 수 글자를 붙인다.
//
//  탭 버튼에서 ShowStage(n)만 불러주면 된다.
//
//  ★"좋아졌다/나빠졌다" 같은 판정 문구는 절대 넣지 않는다.
//    숫자와 선만 보여준다.
// ============================================================
public class LineChart : MonoBehaviour
{
    [Header("연결")]
    public LineChartGraphic graphic;        // 선·점을 그리는 컴포넌트
    public TMP_Text labelPrefab;            // 눈금·날짜 글자 원본 (씬에 하나 만들어 끄고 연결)
    public TMP_Text titleText;              // 위 제목 ("입 벌린 크기" 등)
    public TMP_Text playsText;              // "지금까지 12판 했어요"
    public TMP_Text legendText;             // 범례 ("● 왼쪽   ● 오른쪽") — 선이 둘일 때만 보임
    public TMP_Text emptyText;              // 기록이 없을 때 안내

    [Header("선 색")]
    public Color color1 = new Color(0.88f, 0.48f, 0.37f);   // 첫 번째 선 (산호색)
    public Color color2 = new Color(0.24f, 0.55f, 0.79f);   // 두 번째 선 (파랑)

    [Header("글자")]
    public float labelFontSize = 36f;       // 눈금·날짜 글자 크기 (원본 프리팹 크기를 덮어씀)
    public int maxDateLabels = 7;           // 날짜 글자는 최대 몇 개까지 (겹침 방지)
    public float dateLabelOffset = 12f;     // 축 아래로 얼마나
    public float yLabelOffset = 14f;        // 축 왼쪽으로 얼마나

    // 만들어 둔 글자들 (다시 그릴 때 지우려고 기억)
    List<TMP_Text> spawned = new List<TMP_Text>();

    int currentStage = 1;

    // 스테이지별 제목 (세로축이 무엇인지)
    static readonly string[] TITLES =
    {
        "",
        "입 벌린 크기",          // 1
        "입술 다문 힘",          // 2
        "입술 오므린 크기",      // 3
        "볼 부풀린 크기",        // 4
        "미소 크기",             // 5
        "혀 내민 크기",          // 6
        "혀 좌우 뻗은 크기",     // 7
        "하루 판 수",            // 8 (최대값 없음)
        "기침 세기",             // 9
        "하루 판 수",            // 10 (최대값 없음)
    };

    void Start()
    {
        ShowStage(currentStage);
    }

    // ------------------------------------------------------------
    //  스테이지 하나 보여주기 (탭 버튼이 부른다)
    // ------------------------------------------------------------
    public void ShowStage(int stage)
    {
        currentStage = Mathf.Clamp(stage, 1, 10);

        ClearLabels();

        // ① 선 몇 개인지, 어떤 side인지 정하기
        string[] sides;
        string[] names;

        if (stage == 4) { sides = new[] { "cheek", "lip" }; names = new[] { "볼", "입술" }; }
        else if (stage == 7) { sides = new[] { "left", "right" }; names = new[] { "왼쪽", "오른쪽" }; }
        else { sides = new[] { "" }; names = new[] { "" }; }

        bool countOnly = (stage == 8 || stage == 10);    // 판 수로 보여주는 스테이지

        // ② 날짜별 값 모으기 — 선이 둘이면 날짜를 합쳐서 x축을 하나로
        List<List<RecordStore.DayPoint>> perSide = new List<List<RecordStore.DayPoint>>();
        SortedSet<string> days = new SortedSet<string>(StringComparer.Ordinal);

        for (int s = 0; s < sides.Length; s++)
        {
            List<RecordStore.DayPoint> pts = RecordStore.DailyMax(stage, sides[s]);
            perSide.Add(pts);
            for (int i = 0; i < pts.Count; i++) days.Add(pts[i].day);
        }

        List<string> dayList = new List<string>(days);

        // ③ 기록이 없으면 안내만
        if (dayList.Count == 0)
        {
            graphic.SetData(new List<LineChartGraphic.Series>(), 0);
            if (titleText != null) titleText.text = TITLES[stage];
            if (playsText != null) playsText.text = "";
            if (legendText != null) legendText.gameObject.SetActive(false);
            if (emptyText != null) { emptyText.gameObject.SetActive(true); emptyText.text = "아직 기록이 없어요"; }
            return;
        }
        if (emptyText != null) emptyText.gameObject.SetActive(false);

        // ④ 선 데이터 만들기 (그날 기록이 없으면 NaN → 선이 끊김)
        List<LineChartGraphic.Series> series = new List<LineChartGraphic.Series>();

        for (int s = 0; s < sides.Length; s++)
        {
            Dictionary<string, RecordStore.DayPoint> map = new Dictionary<string, RecordStore.DayPoint>();
            for (int i = 0; i < perSide[s].Count; i++) map[perSide[s][i].day] = perSide[s][i];

            LineChartGraphic.Series sr = new LineChartGraphic.Series();
            sr.name = names[s];
            sr.color = (s == 0) ? color1 : color2;
            sr.values = new List<float>();

            for (int d = 0; d < dayList.Count; d++)
            {
                if (map.ContainsKey(dayList[d]))
                {
                    RecordStore.DayPoint p = map[dayList[d]];
                    sr.values.Add(countOnly ? p.plays : p.value);
                }
                else
                {
                    sr.values.Add(float.NaN);
                }
            }

            series.Add(sr);
        }

        graphic.SetData(series, dayList.Count);

        // ⑤ 글자들
        if (titleText != null) titleText.text = TITLES[stage];

        int plays = RecordStore.PlayCount(stage);
        if (playsText != null) playsText.text = "지금까지 " + plays + "판 했어요";

        if (legendText != null)
        {
            bool two = sides.Length == 2;
            legendText.gameObject.SetActive(two);
            if (two)
            {
                legendText.text = "<color=#" + ColorUtility.ToHtmlStringRGB(color1) + ">●</color> " + names[0]
                                + "    <color=#" + ColorUtility.ToHtmlStringRGB(color2) + ">●</color> " + names[1];
            }
        }

        MakeDateLabels(dayList);
        MakeYLabels(countOnly);
    }

    // 지금 보고 있는 스테이지 다시 그리기 (기록이 새로 생겼을 때)
    public void Refresh()
    {
        ShowStage(currentStage);
    }

    // ------------------------------------------------------------
    //  날짜 글자 (아래 축)
    // ------------------------------------------------------------
    void MakeDateLabels(List<string> dayList)
    {
        int n = dayList.Count;

        // 너무 많으면 몇 개 건너뛰며 표시
        int step = Mathf.Max(1, Mathf.CeilToInt((float)n / maxDateLabels));

        for (int i = 0; i < n; i++)
        {
            bool show = (i % step == 0) || (i == n - 1);   // 마지막 날은 항상
            if (!show) continue;

            // "2026-09-11" → "9/11"
            string d = dayList[i];
            string text = d;
            if (d.Length >= 10)
            {
                int m = int.Parse(d.Substring(5, 2));
                int day = int.Parse(d.Substring(8, 2));
                text = m + "/" + day;
            }

            // 피벗을 위 가운데로 → 글자 윗변이 축 아래에 딱 붙는다
            TMP_Text t = Spawn(text, new Vector2(0.5f, 1f), new Vector2(160f, 60f));
            t.alignment = TextAlignmentOptions.Top;
            t.rectTransform.anchoredPosition = new Vector2(graphic.GetX(i), graphic.AxisY - dateLabelOffset);
        }
    }

    // ------------------------------------------------------------
    //  세로 눈금 글자 (왼쪽 축)
    // ------------------------------------------------------------
    void MakeYLabels(bool countOnly)
    {
        int lines = Mathf.Max(1, graphic.gridLines);
        float yMax = graphic.YMax;

        for (int g = 0; g <= lines; g++)
        {
            float v = yMax * g / lines;

            // 피벗을 오른쪽 가운데로 → 글자 오른쪽 끝이 축 왼쪽에 딱 붙는다 (축 안으로 안 넘어옴)
            TMP_Text t = Spawn(FormatValue(v, countOnly), new Vector2(1f, 0.5f), new Vector2(160f, 60f));
            t.alignment = TextAlignmentOptions.Right;
            t.rectTransform.anchoredPosition = new Vector2(graphic.AxisX - yLabelOffset, graphic.GetY(v));
        }
    }

    // 숫자를 보기 좋게 — 판 수는 정수, 나머지는 유효숫자 3자리
    //   ⚠소수 둘째 자리로 고정하면 볼 값(0.0079)이 전부 0.01로 뭉개진다
    static string FormatValue(float v, bool countOnly)
    {
        if (countOnly) return Mathf.RoundToInt(v).ToString();
        if (v == 0f) return "0";
        if (v >= 100f) return Mathf.RoundToInt(v).ToString();
        return v.ToString("G3");
    }

    // ------------------------------------------------------------
    //  글자 만들기·지우기
    // ------------------------------------------------------------
    // pivot = 글자 상자의 어느 점을 위치에 맞출지 / size = 글자 상자 크기
    TMP_Text Spawn(string text, Vector2 pivot, Vector2 size)
    {
        TMP_Text t = Instantiate(labelPrefab, graphic.rectTransform);
        t.gameObject.SetActive(true);
        t.text = text;
        t.fontSize = labelFontSize;
        t.enableWordWrapping = false;

        // ★그래픽의 피벗(가운데) 기준 로컬 좌표를 쓰려고 앵커를 가운데로 맞춘다
        RectTransform rt = t.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = pivot;
        rt.sizeDelta = size;

        spawned.Add(t);
        return t;
    }

    void ClearLabels()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null) Destroy(spawned[i].gameObject);
        }
        spawned.Clear();
    }
}