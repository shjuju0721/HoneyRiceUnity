using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

// ============================================================
//  ⚠테스트용 — sqlite3.dll이 제대로 불러와지는지 확인
//
//  위치: Assets/Scripts/Editor/SqliteTest.cs
//  여는 법: 메뉴 Tools > 꿀떡꿀떡 > SQLite 확인
//
//  ★여기서 성공해야 RecordStore를 고칠 의미가 있다.
//    실패하면 DLL 위치·플랫폼 설정 문제이므로 그것부터 해결할 것.
//
//  ⚠확인이 끝나면 지울 것.
// ============================================================
public class SqliteTest : EditorWindow
{
    string log = "아직 확인하지 않았습니다.";
    Vector2 scroll;

    [MenuItem("Tools/꿀떡꿀떡/SQLite 확인")]
    static void Open()
    {
        SqliteTest w = GetWindow<SqliteTest>("SQLite 확인");
        w.minSize = new Vector2(420, 320);
    }

    // ============================================================
    //  sqlite3.dll 안의 함수를 직접 부른다 (P/Invoke)
    //
    //  ★래퍼 라이브러리 없이 DLL만으로 되는지 보는 것이 목적이다.
    //    "sqlite3"는 파일 이름(sqlite3.dll)에서 확장자를 뺀 것.
    // ============================================================

    // 버전 문자열 돌려주기 — 가장 단순한 함수라 연결 확인에 좋다
    [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr sqlite3_libversion();

    // 데이터베이스 열기 (UTF-8 파일 경로)
    [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl,
               CharSet = CharSet.Ansi)]
    static extern int sqlite3_open(string filename, out IntPtr db);

    // 닫기
    [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_close(IntPtr db);

    // SQL 한 줄 실행 (결과를 안 받는 명령용 — CREATE, INSERT 등)
    [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl,
               CharSet = CharSet.Ansi)]
    static extern int sqlite3_exec(IntPtr db, string sql, IntPtr callback,
                                   IntPtr arg, out IntPtr errmsg);

    // 오류 메시지
    [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr sqlite3_errmsg(IntPtr db);

    // ============================================================
    //  창 그리기
    // ============================================================
    void OnGUI()
    {
        EditorGUILayout.HelpBox("sqlite3.dll이 제대로 불러와지는지 확인합니다.\n"
                              + "여기서 성공해야 다음 단계로 갈 수 있습니다.", MessageType.Info);

        GUILayout.Space(8);

        if (GUILayout.Button("① 버전 확인 (가장 기본)", GUILayout.Height(32)))
        {
            CheckVersion();
        }

        GUILayout.Space(4);

        if (GUILayout.Button("② 파일 만들고 읽고 쓰기", GUILayout.Height(32)))
        {
            CheckReadWrite();
        }

        GUILayout.Space(10);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(log, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    // ============================================================
    //  ① 버전만 물어보기 — DLL을 찾을 수 있는지만 본다
    // ============================================================
    void CheckVersion()
    {
        try
        {
            IntPtr p = sqlite3_libversion();
            string version = Marshal.PtrToStringAnsi(p);

            log = "성공!\n\nSQLite 버전: " + version
                + "\n\nDLL을 제대로 찾았습니다. ②번으로 넘어가세요.";

            Debug.Log("[SQLite] 버전 " + version);
        }
        catch (DllNotFoundException)
        {
            log = "실패 — DLL을 찾을 수 없습니다.\n\n"
                + "확인할 것:\n"
                + "  · 파일이 Assets/Plugins/x86_64/sqlite3.dll 에 있는지\n"
                + "  · Inspector에서 Standalone 체크 + CPU가 x64 인지\n"
                + "  · 64비트 DLL을 받았는지 (win-x64)\n"
                + "  · Apply를 눌렀는지\n\n"
                + "설정을 바꿨다면 Unity를 껐다 켜야 반영될 때가 있습니다.";
        }
        catch (Exception e)
        {
            log = "실패 — " + e.GetType().Name + "\n\n" + e.Message;
        }
    }

    // ============================================================
    //  ② 실제로 파일을 만들고 표를 만들어 본다
    // ============================================================
    void CheckReadWrite()
    {
        string path = Path.Combine(Application.persistentDataPath, "sqlite_test.db");

        IntPtr db = IntPtr.Zero;

        try
        {
            // --- 열기 (파일이 없으면 새로 만든다) ---
            int rc = sqlite3_open(path, out db);

            if (rc != 0)
            {
                log = "실패 — 파일을 열 수 없습니다 (코드 " + rc + ")\n" + path;
                return;
            }

            // --- 표 만들고 한 줄 넣기 ---
            string sql = "CREATE TABLE IF NOT EXISTS test (id INTEGER PRIMARY KEY, memo TEXT);"
                       + "DELETE FROM test;"
                       + "INSERT INTO test (memo) VALUES ('꿀떡꿀떡 한글 테스트');";

            IntPtr err;
            rc = sqlite3_exec(db, sql, IntPtr.Zero, IntPtr.Zero, out err);

            if (rc != 0)
            {
                string msg = Marshal.PtrToStringAnsi(sqlite3_errmsg(db));
                log = "실패 — SQL 실행 오류\n" + msg;
                return;
            }

            log = "성공!\n\n"
                + "파일을 만들고 표에 한 줄을 넣었습니다.\n\n"
                + "파일 위치:\n" + path + "\n\n"
                + "이제 RecordStore를 SQLite로 바꿀 수 있습니다.";

            Debug.Log("[SQLite] 읽고 쓰기 성공 — " + path);
        }
        catch (Exception e)
        {
            log = "실패 — " + e.GetType().Name + "\n\n" + e.Message;
        }
        finally
        {
            // --- 꼭 닫는다 (안 닫으면 파일이 잠긴 채 남는다) ---
            if (db != IntPtr.Zero) sqlite3_close(db);
        }
    }
}