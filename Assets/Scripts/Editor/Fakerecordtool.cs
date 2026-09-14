using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;

// ============================================================
//  ⚠ 테스트용 — 대시보드 선그래프 확인을 위한 가짜 판 기록 넣기
//
//  위치: Assets/Editor/FakeRecordTool.cs  (★반드시 Editor 폴더 안)
//        Editor 폴더 스크립트는 빌드에 포함되지 않는다.
//
//  여는 법: 메뉴 Tools > 꿀떡꿀떡 > 가짜 기록 넣기
//
//  ★RecordStore는 건드리지 않는다. records.json 파일을 직접 읽어서
//    며칠치 기록을 덧붙이고 다시 쓴다. 파일 구조(RecordBook)는 같다.
//
//  ★넣은 가짜 기록의 playedAt 목록을 fake_keys.json에 따로 적어 둔다.
//    그래서 "가짜만 지우기"가 가능하다 (진짜 기록은 안 건드림).
//
//  ⚠ 제출 전 이 파일과 fake_keys.json을 지울 것.
// ============================================================
public class FakeRecordTool : EditorWindow
{
    // ----- 창에서 조절하는 값 -----
    string nickname = "";       // 어느 별명으로 넣을지
    int daysBack = 14;          // 며칠 전부터 오늘까지
    int minPlaysPerDay = 0;     // 하루 최소 판 수 (0이면 쉬는 날도 생김)
    int maxPlaysPerDay = 3;     // 하루 최대 판 수
    bool[] useStage = new bool[11];   // 1~10 어느 스테이지를 넣을지 (0번은 안 씀)
    bool trendUp = true;        // 날이 갈수록 조금씩 오르게 (회복 추이처럼)
    int seed = 1234;            // 같은 숫자면 같은 결과

    Vector2 scroll;
    string lastMessage = "";

    // ----- 파일 경로 -----
    static string RecordPath() { return Path.Combine(Application.persistentDataPath, "records.json"); }
    static string FakeKeyPath() { return Path.Combine(Application.persistentDataPath, "fake_keys.json"); }

    // 가짜 기록 표시용 (fake_keys.json에 담기는 것)
    [Serializable]
    class FakeKeys
    {
        public List<string> keys = new List<string>();   // "stage|playedAt" 문자열
    }

    // ============================================================
    //  메뉴
    // ============================================================
    [MenuItem("Tools/꿀떡꿀떡/가짜 기록 넣기")]
    static void Open()
    {
        FakeRecordTool w = GetWindow<FakeRecordTool>("가짜 기록");
        w.minSize = new Vector2(360, 520);
    }

    void OnEnable()
    {
        // 처음 열 때 지금 고른 별명을 기본값으로
        if (nickname == "") nickname = PlayerPrefs.GetString("user_nickname", "");

        // 스테이지는 전부 켜진 상태로 시작
        for (int i = 1; i <= 10; i++) useStage[i] = true;
    }

    // ============================================================
    //  창 그리기
    // ============================================================
    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.HelpBox("테스트 전용 도구입니다. 제출 전 이 파일을 지우세요.", MessageType.Warning);

        GUILayout.Space(6);
        nickname = EditorGUILayout.TextField("별명", nickname);
        daysBack = EditorGUILayout.IntSlider("며칠치", daysBack, 1, 60);
        minPlaysPerDay = EditorGUILayout.IntSlider("하루 최소 판 수", minPlaysPerDay, 0, 5);
        maxPlaysPerDay = EditorGUILayout.IntSlider("하루 최대 판 수", maxPlaysPerDay, 1, 6);
        if (maxPlaysPerDay < minPlaysPerDay) maxPlaysPerDay = minPlaysPerDay;
        trendUp = EditorGUILayout.Toggle("갈수록 조금씩 오르게", trendUp);
        seed = EditorGUILayout.IntField("랜덤 씨앗", seed);

        GUILayout.Space(8);
        GUILayout.Label("넣을 스테이지", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("전부 켜기")) for (int i = 1; i <= 10; i++) useStage[i] = true;
        if (GUILayout.Button("전부 끄기")) for (int i = 1; i <= 10; i++) useStage[i] = false;
        EditorGUILayout.EndHorizontal();

        string[] names = { "", "1 계단", "2 줄다리기", "3 하트", "4 펭귄", "5 미소",
                           "6 개구리", "7 젤리", "8 러너", "9 민들레", "10 떠먹이기" };
        for (int i = 1; i <= 10; i++)
        {
            useStage[i] = EditorGUILayout.ToggleLeft(names[i], useStage[i]);
        }

        GUILayout.Space(12);

        // ----- 넣기 -----
        GUI.enabled = !string.IsNullOrEmpty(nickname);
        if (GUILayout.Button("가짜 기록 넣기", GUILayout.Height(36)))
        {
            Generate();
        }
        GUI.enabled = true;

        GUILayout.Space(6);

        // ----- 지우기 -----
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("가짜만 지우기"))
        {
            RemoveFakeOnly();
        }
        if (GUILayout.Button("기록 전부 지우기"))
        {
            if (EditorUtility.DisplayDialog("전부 지우기",
                "진짜 기록까지 모두 사라집니다. 정말 지울까요?", "지우기", "취소"))
            {
                File.WriteAllText(RecordPath(), JsonConvert.SerializeObject(new RecordBook(), Formatting.Indented));
                if (File.Exists(FakeKeyPath())) File.Delete(FakeKeyPath());
                lastMessage = "기록을 전부 지웠습니다.";
            }
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(6);
        if (GUILayout.Button("기록 파일 폴더 열기"))
        {
            EditorUtility.RevealInFinder(RecordPath());
        }

        GUILayout.Space(10);
        if (lastMessage != "") EditorGUILayout.HelpBox(lastMessage, MessageType.Info);

        GUILayout.Space(6);
        EditorGUILayout.LabelField("파일 위치", EditorStyles.miniLabel);
        EditorGUILayout.SelectableLabel(RecordPath(), EditorStyles.miniLabel, GUILayout.Height(30));

        EditorGUILayout.EndScrollView();
    }

    // ============================================================
    //  파일 읽고 쓰기 (RecordStore와 같은 구조)
    // ============================================================
    static RecordBook ReadBook()
    {
        try
        {
            string path = RecordPath();
            if (!File.Exists(path)) return new RecordBook();

            string json = File.ReadAllText(path);
            if (string.IsNullOrEmpty(json)) return new RecordBook();

            RecordBook b = JsonConvert.DeserializeObject<RecordBook>(json);
            if (b == null || b.records == null) return new RecordBook();
            return b;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Fake] 읽기 실패 — 빈 통으로 시작: " + e.Message);
            return new RecordBook();
        }
    }

    static void WriteBook(RecordBook b)
    {
        File.WriteAllText(RecordPath(), JsonConvert.SerializeObject(b, Formatting.Indented));
    }

    static FakeKeys ReadFakeKeys()
    {
        try
        {
            string path = FakeKeyPath();
            if (!File.Exists(path)) return new FakeKeys();
            FakeKeys k = JsonConvert.DeserializeObject<FakeKeys>(File.ReadAllText(path));
            if (k == null || k.keys == null) return new FakeKeys();
            return k;
        }
        catch
        {
            return new FakeKeys();
        }
    }

    static void WriteFakeKeys(FakeKeys k)
    {
        File.WriteAllText(FakeKeyPath(), JsonConvert.SerializeObject(k, Formatting.Indented));
    }

    // ============================================================
    //  가짜 기록 만들기
    // ============================================================
    void Generate()
    {
        RecordBook book = ReadBook();
        FakeKeys fake = ReadFakeKeys();

        System.Random rng = new System.Random(seed);
        int added = 0;

        // 오늘부터 daysBack-1일 전까지 거꾸로 돌면서 하루씩 만든다
        for (int d = daysBack - 1; d >= 0; d--)
        {
            DateTime day = DateTime.Today.AddDays(-d);

            // 진행도 0(처음) ~ 1(오늘) — 오를 때 쓰는 비율
            float t = daysBack <= 1 ? 1f : (float)(daysBack - 1 - d) / (daysBack - 1);
            if (!trendUp) t = 0.5f;

            // 오늘 몇 판 할지
            int plays = rng.Next(minPlaysPerDay, maxPlaysPerDay + 1);

            for (int p = 0; p < plays; p++)
            {
                // 켜진 스테이지 중 하나 고르기
                int stage = PickStage(rng);
                if (stage == 0) break;

                // 시각은 오전 9시 ~ 오후 8시 사이 아무 때나, 판마다 다르게
                DateTime at = day.AddHours(9 + rng.Next(0, 12)).AddMinutes(rng.Next(0, 60)).AddSeconds(rng.Next(0, 60));
                string playedAt = at.ToString("yyyy-MM-dd HH:mm:ss");
                string dayStr = at.ToString("yyyy-MM-dd");

                // 같은 시각이 이미 있으면 1초 뒤로 (PlayCount가 시각으로 묶으니 겹치면 안 됨)
                while (fake.keys.Contains(stage + "|" + playedAt))
                {
                    at = at.AddSeconds(1);
                    playedAt = at.ToString("yyyy-MM-dd HH:mm:ss");
                }

                // 스테이지별로 한 판 만들기 (4·7은 두 줄)
                List<GameRecord> rows = MakeRows(stage, t, rng);

                for (int i = 0; i < rows.Count; i++)
                {
                    GameRecord r = rows[i];
                    r.nickname = nickname;
                    r.playedAt = playedAt;
                    r.day = dayStr;
                    r.stage = stage;
                    book.records.Add(r);
                    added++;
                }

                fake.keys.Add(stage + "|" + playedAt);
            }
        }

        WriteBook(book);
        WriteFakeKeys(fake);

        lastMessage = nickname + " 별명으로 " + daysBack + "일치 가짜 기록 " + added + "줄을 넣었습니다.\n"
                    + "(Play 모드였다면 나갔다 다시 들어와야 반영됩니다)";
        Debug.Log("[Fake] " + lastMessage);
    }

    // 켜진 스테이지 중에서 하나 뽑기 (없으면 0)
    int PickStage(System.Random rng)
    {
        List<int> on = new List<int>();
        for (int i = 1; i <= 10; i++) if (useStage[i]) on.Add(i);
        if (on.Count == 0) return 0;
        return on[rng.Next(0, on.Count)];
    }

    // lo에서 hi 사이를 진행도 t만큼 올린 값 + 잡음
    static float Rise(System.Random rng, float lo, float hi, float t, float noise)
    {
        // 처음엔 lo 근처, 오늘쯤엔 lo와 hi 사이 70% 지점 — 완전히 hi까지 가진 않게
        float baseV = lo + (hi - lo) * t * 0.7f;
        float n = ((float)rng.NextDouble() * 2f - 1f) * noise;   // -noise ~ +noise
        return Mathf.Clamp(baseV + n, lo * 0.9f, hi);
    }

    static int RiseInt(System.Random rng, int lo, int hi, float t)
    {
        float v = lo + (hi - lo) * t;
        int n = rng.Next(-1, 2);   // -1 ~ +1
        return Mathf.Clamp(Mathf.RoundToInt(v) + n, lo, hi);
    }

    // ============================================================
    //  스테이지별 값 범위 — 실측·게임 규칙에 맞춘 그럴듯한 숫자
    //  (그래프 모양 확인용이라 정확할 필요는 없음)
    // ============================================================
    static List<GameRecord> MakeRows(int stage, float t, System.Random rng)
    {
        List<GameRecord> rows = new List<GameRecord>();

        switch (stage)
        {
            case 1:   // 계단 — 오른 계단 수 / jawOpen 최대 (실측 최대 0.89)
                rows.Add(Row(RiseInt(rng, 18, 21, t), Rise(rng, 0.55f, 0.88f, t, 0.05f), true, ""));
                break;

            case 2:   // 줄다리기 — 완료 세트 / mouthPress 최대 (실측 0.22~0.40)
                rows.Add(Row(3, Rise(rng, 0.15f, 0.36f, t, 0.03f), true, ""));
                break;

            case 3:   // 하트 — 도착 하트 수 / mouthFunnel 최대 (실측 0.45)
                rows.Add(Row(RiseInt(rng, 3, 6, t), Rise(rng, 0.28f, 0.47f, t, 0.03f), true, ""));
                break;

            case 4:   // 펭귄 — 발사 횟수 / 볼·입술 두 줄
                {
                    int shots = RiseInt(rng, 4, 8, t);
                    rows.Add(Row(shots, Rise(rng, 0.010f, 0.022f, t, 0.002f), true, "cheek"));
                    rows.Add(Row(shots, Rise(rng, 0.20f, 0.38f, t, 0.03f), true, "lip"));
                }
                break;

            case 5:   // 미소 — 사진 수 / mouthSmile 최대 (실측 0.70)
                rows.Add(Row(5, Rise(rng, 0.45f, 0.78f, t, 0.04f), true, ""));
                break;

            case 6:   // 개구리 — 잡은 파리 수 / 혀 비율 최대 (실측 0.93)
                rows.Add(Row(RiseInt(rng, 8, 12, t), Rise(rng, 0.60f, 0.95f, t, 0.05f), true, ""));
                break;

            case 7:   // 젤리 — 좌·우 두 줄, 일부러 비대칭 (실측 좌 0.163 / 우 0.232)
                rows.Add(Row(RiseInt(rng, 3, 6, t), Rise(rng, 0.10f, 0.20f, t, 0.02f), true, "left"));
                rows.Add(Row(RiseInt(rng, 4, 6, t), Rise(rng, 0.16f, 0.27f, t, 0.02f), true, "right"));
                break;

            case 8:   // 러너 — 통과 수만 (최대값 없음)
                rows.Add(Row(RiseInt(rng, 6, 10, t), 0f, false, ""));
                break;

            case 9:   // 민들레 — 기침 수 / 기침 세기 최대 (0~100점)
                rows.Add(Row(RiseInt(rng, 5, 9, t), Rise(rng, 45f, 88f, t, 6f), true, ""));
                break;

            case 10:  // 떠먹이기 — 완료 술 수만 (최대값 없음)
                rows.Add(Row(RiseInt(rng, 3, 5, t), 0f, false, ""));
                break;
        }

        return rows;
    }

    static GameRecord Row(int reps, float maxEffort, bool hasEffort, string side)
    {
        GameRecord r = new GameRecord();
        r.reps = reps;
        r.maxEffort = hasEffort ? (float)Math.Round(maxEffort, 4) : 0f;
        r.hasEffort = hasEffort;
        r.side = side;
        return r;
    }

    // ============================================================
    //  가짜만 지우기 — fake_keys.json에 적힌 것만 골라 뺀다
    // ============================================================
    void RemoveFakeOnly()
    {
        RecordBook book = ReadBook();
        FakeKeys fake = ReadFakeKeys();

        HashSet<string> keys = new HashSet<string>(fake.keys);
        int before = book.records.Count;

        book.records.RemoveAll(r => keys.Contains(r.stage + "|" + r.playedAt));

        WriteBook(book);
        if (File.Exists(FakeKeyPath())) File.Delete(FakeKeyPath());

        lastMessage = "가짜 기록 " + (before - book.records.Count) + "줄을 지웠습니다. (진짜 기록 " + book.records.Count + "줄은 그대로)";
        Debug.Log("[Fake] " + lastMessage);
    }
}