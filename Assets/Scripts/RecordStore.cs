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
//  ★저장 방식(SQLite)은 이 안에만 있다.
//    나중에 서버로 바꿔도 게임 코드는 건드릴 필요가 없다.
//    바꿀 곳은 Load()·Write() 두 함수뿐.
//
//  ★2026-09-14 JSON 파일 → SQLite로 바꿈.
//    바깥에서 쓰는 함수(All·ByStage·PlayCount·DailyMax·Save)는 그대로다.
//    기존 records.json이 있으면 처음 한 번 자동으로 옮겨 온다.
//
//  저장 위치: Application.persistentDataPath/records.db
//    Windows 기준 C:/Users/(이름)/AppData/LocalLow/(회사)/(게임)/records.db
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

// ===== 메모리에 들고 있는 통 =====
//   ⚠예전 JSON 파일을 읽을 때도 이 모양을 쓴다(옮겨오기용)
[Serializable]
public class RecordBook
{
    public List<GameRecord> records = new List<GameRecord>();
}

public static class RecordStore
{
    private const string DB_NAME = "records.db";
    private const string OLD_JSON = "records.json";     // 예전 저장 파일
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

        // ★한 줄만 넣는다 (전체를 다시 쓰지 않는다)
        InsertOne(r);

        Debug.Log("[Record] 저장 — 스테이지 " + stage
                  + " / " + reps + "회"
                  + (hasEffort ? " / 최대 " + maxEffort.ToString("F3") : "")
                  + (side != "" ? " / " + side : ""));
    }

    // ============================================================
    //  읽기·쓰기 — ★저장 방식을 바꿀 때 여기만 고치면 된다
    // ============================================================

    static string DbPath()
    {
        return Path.Combine(Application.persistentDataPath, DB_NAME);
    }

    static string OldJsonPath()
    {
        return Path.Combine(Application.persistentDataPath, OLD_JSON);
    }

    // 표를 만드는 SQL — 없을 때만 만들어진다
    const string CREATE_SQL =
        "CREATE TABLE IF NOT EXISTS records ("
      + "  id       INTEGER PRIMARY KEY AUTOINCREMENT,"
      + "  nickname TEXT    NOT NULL DEFAULT '',"
      + "  playedAt TEXT    NOT NULL DEFAULT '',"
      + "  day      TEXT    NOT NULL DEFAULT '',"
      + "  stage    INTEGER NOT NULL DEFAULT 0,"
      + "  reps     INTEGER NOT NULL DEFAULT 0,"
      + "  maxEffort REAL   NOT NULL DEFAULT 0,"
      + "  hasEffort INTEGER NOT NULL DEFAULT 0,"   // SQLite에는 true/false가 없어 0/1로 넣는다
      + "  side     TEXT    NOT NULL DEFAULT ''"
      + ");"
      // 자주 찾는 조건이라 미리 색인을 만들어 둔다
      + "CREATE INDEX IF NOT EXISTS idx_nick_stage ON records (nickname, stage);";

    public static void Load()
    {
        if (book != null) return;   // 한 번만 읽는다

        book = new RecordBook();

        IntPtr db = IntPtr.Zero;
        IntPtr stmt = IntPtr.Zero;

        try
        {
            if (!Sqlite.Open(DbPath(), out db))
            {
                Debug.LogWarning("[Record] 데이터베이스를 열 수 없습니다 — 빈 기록으로 시작합니다.");
                return;
            }

            // --- 표 만들기 ---
            string err;
            if (!Sqlite.Exec(db, CREATE_SQL, out err))
            {
                Debug.LogWarning("[Record] 표 만들기 실패: " + err);
                return;
            }

            // --- 예전 JSON이 남아 있으면 한 번만 옮겨 온다 ---
            MigrateFromJson(db);

            // --- 전부 읽어 오기 ---
            //   ⚠id 순서 = 넣은 순서. 예전 JSON의 줄 순서가 그대로 유지된다
            if (!Sqlite.Prepare(db,
                "SELECT nickname, playedAt, day, stage, reps, maxEffort, hasEffort, side "
                + "FROM records ORDER BY id", out stmt))
            {
                Debug.LogWarning("[Record] 읽기 준비 실패: " + Sqlite.ErrorMessage(db));
                return;
            }

            while (Sqlite.Step(stmt) == Sqlite.ROW)
            {
                GameRecord r = new GameRecord();

                r.nickname  = Sqlite.ColText(stmt, 0);
                r.playedAt  = Sqlite.ColText(stmt, 1);
                r.day       = Sqlite.ColText(stmt, 2);
                r.stage     = Sqlite.ColInt(stmt, 3);
                r.reps      = Sqlite.ColInt(stmt, 4);
                r.maxEffort = Sqlite.ColFloat(stmt, 5);
                r.hasEffort = Sqlite.ColInt(stmt, 6) != 0;   // 0/1 → false/true
                r.side      = Sqlite.ColText(stmt, 7);

                book.records.Add(r);
            }

            Debug.Log("[Record] 불러오기 완료 — " + book.records.Count + "줄");
        }
        catch (Exception e)
        {
            // ★데이터베이스가 깨져도 게임은 계속 돌아야 한다
            Debug.LogWarning("[Record] 불러오기 실패 — 빈 기록으로 시작합니다: " + e.Message);
            book = new RecordBook();
        }
        finally
        {
            Sqlite.Finalize(stmt);
            Sqlite.Close(db);
        }
    }

    // ===== 한 줄 넣기 (게임이 끝날 때마다) =====
    static void InsertOne(GameRecord r)
    {
        IntPtr db = IntPtr.Zero;
        IntPtr stmt = IntPtr.Zero;

        try
        {
            if (!Sqlite.Open(DbPath(), out db))
            {
                Debug.LogError("[Record] 저장 실패 — 데이터베이스를 열 수 없습니다.");
                return;
            }

            string err;
            Sqlite.Exec(db, CREATE_SQL, out err);   // 혹시 표가 없으면 만든다

            if (!Sqlite.Prepare(db,
                "INSERT INTO records (nickname, playedAt, day, stage, reps, maxEffort, hasEffort, side) "
                + "VALUES (?, ?, ?, ?, ?, ?, ?, ?)", out stmt))
            {
                Debug.LogError("[Record] 저장 준비 실패: " + Sqlite.ErrorMessage(db));
                return;
            }

            BindRecord(stmt, r);

            if (Sqlite.Step(stmt) != Sqlite.DONE)
            {
                Debug.LogError("[Record] 저장 실패: " + Sqlite.ErrorMessage(db));
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[Record] 저장 실패: " + e.Message);
        }
        finally
        {
            Sqlite.Finalize(stmt);
            Sqlite.Close(db);
        }
    }

    // 값 끼워 넣기 (자리 번호는 1부터)
    static void BindRecord(IntPtr stmt, GameRecord r)
    {
        Sqlite.BindText(stmt, 1, r.nickname);
        Sqlite.BindText(stmt, 2, r.playedAt);
        Sqlite.BindText(stmt, 3, r.day);
        Sqlite.BindInt(stmt, 4, r.stage);
        Sqlite.BindInt(stmt, 5, r.reps);
        Sqlite.BindFloat(stmt, 6, r.maxEffort);
        Sqlite.BindInt(stmt, 7, r.hasEffort ? 1 : 0);
        Sqlite.BindText(stmt, 8, r.side);
    }

    // ===== 메모리에 있는 것을 전부 다시 쓰기 (지우기 등에 씀) =====
    static void Write()
    {
        IntPtr db = IntPtr.Zero;
        IntPtr stmt = IntPtr.Zero;

        try
        {
            if (!Sqlite.Open(DbPath(), out db)) return;

            string err;
            Sqlite.Exec(db, CREATE_SQL, out err);

            // ★한 덩어리로 묶어 쓴다 — 훨씬 빠르고, 중간에 끊겨도 반만 남지 않는다
            Sqlite.Exec(db, "BEGIN TRANSACTION;", out err);
            Sqlite.Exec(db, "DELETE FROM records;", out err);

            if (Sqlite.Prepare(db,
                "INSERT INTO records (nickname, playedAt, day, stage, reps, maxEffort, hasEffort, side) "
                + "VALUES (?, ?, ?, ?, ?, ?, ?, ?)", out stmt))
            {
                for (int i = 0; i < book.records.Count; i++)
                {
                    BindRecord(stmt, book.records[i]);
                    Sqlite.Step(stmt);
                    Sqlite.Reset(stmt);     // 다음 줄을 위해 되감기
                }
            }

            Sqlite.Finalize(stmt);
            stmt = IntPtr.Zero;

            Sqlite.Exec(db, "COMMIT;", out err);
        }
        catch (Exception e)
        {
            Debug.LogError("[Record] 다시 쓰기 실패: " + e.Message);
        }
        finally
        {
            Sqlite.Finalize(stmt);
            Sqlite.Close(db);
        }
    }

    // ============================================================
    //  예전 JSON 옮겨 오기 — 처음 한 번만
    //
    //  ★옮긴 뒤 원본은 records.json.backup 으로 이름만 바꿔 남긴다.
    //    지우지 않는 이유: 옮기기가 잘못됐을 때 되돌릴 수 있어야 한다.
    // ============================================================
    static void MigrateFromJson(IntPtr db)
    {
        string jsonPath = OldJsonPath();

        if (!File.Exists(jsonPath)) return;   // 옮길 것이 없다

        IntPtr stmt = IntPtr.Zero;

        try
        {
            string json = File.ReadAllText(jsonPath);

            if (string.IsNullOrEmpty(json)) return;

            RecordBook old = JsonConvert.DeserializeObject<RecordBook>(json);

            if (old == null || old.records == null || old.records.Count == 0) return;

            string err;
            Sqlite.Exec(db, "BEGIN TRANSACTION;", out err);

            if (Sqlite.Prepare(db,
                "INSERT INTO records (nickname, playedAt, day, stage, reps, maxEffort, hasEffort, side) "
                + "VALUES (?, ?, ?, ?, ?, ?, ?, ?)", out stmt))
            {
                for (int i = 0; i < old.records.Count; i++)
                {
                    BindRecord(stmt, old.records[i]);
                    Sqlite.Step(stmt);
                    Sqlite.Reset(stmt);
                }
            }

            Sqlite.Finalize(stmt);
            stmt = IntPtr.Zero;

            Sqlite.Exec(db, "COMMIT;", out err);

            // --- 원본은 지우지 않고 이름만 바꾼다 ---
            string backup = jsonPath + ".backup";

            if (File.Exists(backup)) File.Delete(backup);

            File.Move(jsonPath, backup);

            Debug.Log("[Record] 예전 기록 " + old.records.Count + "줄을 옮겼습니다. "
                      + "원본은 records.json.backup 으로 남겨 두었습니다.");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Record] 옮기기 실패 — 예전 기록은 그대로 둡니다: " + e.Message);
        }
        finally
        {
            Sqlite.Finalize(stmt);
        }
    }

    // ============================================================
    //  바깥에서 꺼내 쓰는 것들 (대시보드가 쓴다)
    //  ★아래는 저장 방식과 무관하다 — 한 줄도 바뀌지 않았다
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
        Load();

        book = new RecordBook();
        Write();

        Debug.Log("[Record] 기록을 모두 지웠습니다.");
    }

    // ★메모리에 들고 있는 것을 버리고 다음 번에 다시 읽게 한다
    //   (바깥에서 데이터베이스를 고쳤을 때 씀)
    public static void Reload()
    {
        book = null;
        Load();
    }

    // 저장 파일이 어디 있는지 (확인용)
    public static string WhereIsFile()
    {
        return DbPath();
    }
}