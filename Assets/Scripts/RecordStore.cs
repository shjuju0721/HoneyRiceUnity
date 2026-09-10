using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

// ============================================================
//  판 기록 저장소
//
//  게임 쪽에서는 이 한 줄만 부르면 된다:
//     RecordStore.Save(7, reps: 10, maxEffort: 0.163f, side: "left");
//
//  ★저장 방식(JSON 파일)은 이 안에만 있다.
//    나중에 SQLite나 서버로 바꿔도 게임 코드는 건드릴 필요가 없다.
//    바꿀 곳은 Load()·Write() 두 함수뿐.
//
//  저장 위치: Application.persistentDataPath/records.json
//    Windows 기준 C:/Users/(이름)/AppData/LocalLow/(회사)/(게임)/records.json
// ============================================================

// ===== 판 기록 한 줄 =====
[Serializable]
public class GameRecord
{
    public string nickname;      // 별명
    public string playedAt;      // 언제 (yyyy-MM-dd HH:mm:ss)
    public string day;           // 날짜만 (yyyy-MM-dd) — 하루 최고값을 뽑을 때 씀
    public int stage;            // 스테이지 번호 1~10
    public int reps;             // 반복 횟수
    public float maxEffort;      // 그 판에서 실제로 낸 최대 동작값
                                 //   ⚠연습에서 잰 값을 복사하지 말 것
    public bool hasEffort;       // 최대값이 없는 스테이지(8·10)는 false
    public string side;          // 좌우 구분 — "left" / "right" / "" (스테이지7만 씀)
}

// ===== 파일에 담기는 통 =====
[Serializable]
public class RecordBook
{
    public List<GameRecord> records = new List<GameRecord>();
}

public static class RecordStore
{
    private const string FILE_NAME = "records.json";
    private const string KEY_NICK = "user_nickname";

    private static RecordBook book = null;

    // ============================================================
    //  별명
    // ============================================================
    public static string Nickname
    {
        get { return PlayerPrefs.GetString(KEY_NICK, ""); }
        set
        {
            PlayerPrefs.SetString(KEY_NICK, value);
            PlayerPrefs.Save();
        }
    }

    public static bool HasNickname()
    {
        return !string.IsNullOrEmpty(Nickname);
    }

    // ============================================================
    //  저장
    // ============================================================

    // 최대값이 있는 스테이지
    public static void Save(int stage, int reps, float maxEffort, string side = "")
    {
        Add(stage, reps, maxEffort, true, side);
    }

    // 최대값이 없는 스테이지 (8·10 — 횟수만)
    public static void SaveCountOnly(int stage, int reps)
    {
        Add(stage, reps, 0f, false, "");
    }

    static void Add(int stage, int reps, float maxEffort, bool hasEffort, string side)
    {
        Load();

        DateTime now = DateTime.Now;

        GameRecord r = new GameRecord();
        r.nickname = Nickname;
        r.playedAt = now.ToString("yyyy-MM-dd HH:mm:ss");
        r.day = now.ToString("yyyy-MM-dd");
        r.stage = stage;
        r.reps = reps;
        r.maxEffort = maxEffort;
        r.hasEffort = hasEffort;
        r.side = side;

        book.records.Add(r);

        Write();

        Debug.Log("[Record] 저장 — 스테이지 " + stage
                  + " / " + reps + "회"
                  + (hasEffort ? " / 최대 " + maxEffort.ToString("F3") : "")
                  + (side != "" ? " / " + side : ""));
    }

    // ============================================================
    //  읽기·쓰기 — ★저장 방식을 바꿀 때 여기만 고치면 된다
    // ============================================================

    static string FilePath()
    {
        return Path.Combine(Application.persistentDataPath, FILE_NAME);
    }

    public static void Load()
    {
        if (book != null) return;   // 한 번만 읽는다

        book = new RecordBook();

        try
        {
            string path = FilePath();

            if (!File.Exists(path)) return;

            string json = File.ReadAllText(path);

            if (string.IsNullOrEmpty(json)) return;

            RecordBook loaded = JsonConvert.DeserializeObject<RecordBook>(json);

            if (loaded != null && loaded.records != null)
            {
                book = loaded;
            }
        }
        catch (Exception e)
        {
            // ★파일이 깨져도 게임은 계속 돌아야 한다
            Debug.LogWarning("[Record] 불러오기 실패 — 빈 기록으로 시작합니다: " + e.Message);
            book = new RecordBook();
        }
    }

    static void Write()
    {
        try
        {
            string json = JsonConvert.SerializeObject(book, Formatting.Indented);
            File.WriteAllText(FilePath(), json);
        }
        catch (Exception e)
        {
            Debug.LogError("[Record] 저장 실패: " + e.Message);
        }
    }

    // ============================================================
    //  바깥에서 꺼내 쓰는 것들 (대시보드가 쓴다)
    // ============================================================

    // 지금 별명의 기록 전부
    public static List<GameRecord> All()
    {
        Load();

        string nick = Nickname;
        List<GameRecord> list = new List<GameRecord>();

        for (int i = 0; i < book.records.Count; i++)
        {
            if (book.records[i].nickname == nick)
            {
                list.Add(book.records[i]);
            }
        }

        return list;
    }

    // 특정 스테이지의 기록 (side를 주면 그것만)
    public static List<GameRecord> ByStage(int stage, string side = "")
    {
        List<GameRecord> all = All();
        List<GameRecord> list = new List<GameRecord>();

        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].stage != stage) continue;
            if (side != "" && all[i].side != side) continue;

            list.Add(all[i]);
        }

        return list;
    }

    // 지금까지 몇 판 했나 (전체 또는 특정 스테이지)
    public static int PlayCount(int stage = 0)
    {
        List<GameRecord> all = All();

        if (stage <= 0)
        {
            // ⚠스테이지7은 한 판에 좌·우 2줄이 저장되므로 그대로 세면 2배가 된다
            //   판 수를 셀 때는 같은 시각의 좌우를 한 판으로 묶는다
            HashSet<string> seen = new HashSet<string>();

            for (int i = 0; i < all.Count; i++)
            {
                seen.Add(all[i].stage + "|" + all[i].playedAt);
            }

            return seen.Count;
        }

        int n = 0;

        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].stage != stage) continue;

            // 스테이지7은 왼쪽 줄만 세어 한 판으로 본다
            if (stage == 7 && all[i].side == "right") continue;

            n++;
        }

        return n;
    }

    // ★하루 최고값 (대시보드 선그래프용)
    //   같은 날 여러 판을 하면 그날의 최고값 하나만 쓴다.
    //   ⚠평균을 쓰면 컨디션 나쁜 판이 끌어내려 추이가 흐려진다.
    public class DayPoint
    {
        public string day;      // yyyy-MM-dd
        public float value;     // 그날 최고값
        public int plays;       // 그날 판 수
    }

    public static List<DayPoint> DailyMax(int stage, string side = "")
    {
        List<GameRecord> list = ByStage(stage, side);

        // 날짜별로 모으기
        Dictionary<string, DayPoint> map = new Dictionary<string, DayPoint>();

        for (int i = 0; i < list.Count; i++)
        {
            GameRecord r = list[i];

            // 최대값이 없는 스테이지는 횟수를 값으로 쓴다
            float v = r.hasEffort ? r.maxEffort : r.reps;

            if (!map.ContainsKey(r.day))
            {
                DayPoint p = new DayPoint();
                p.day = r.day;
                p.value = v;
                p.plays = 1;
                map[r.day] = p;
            }
            else
            {
                if (v > map[r.day].value) map[r.day].value = v;
                map[r.day].plays++;
            }
        }

        // 날짜순으로 세우기
        List<DayPoint> points = new List<DayPoint>(map.Values);

        points.Sort((a, b) => string.Compare(a.day, b.day, StringComparison.Ordinal));

        return points;
    }

    // ============================================================
    //  지우기 (테스트용)
    // ============================================================
    public static void ClearAll()
    {
        book = new RecordBook();
        Write();

        Debug.Log("[Record] 기록을 모두 지웠습니다.");
    }

    // 저장 파일이 어디 있는지 (확인용)
    public static string WhereIsFile()
    {
        return FilePath();
    }
}