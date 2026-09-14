using System;
using System.Runtime.InteropServices;
using System.Text;

// ============================================================
//  sqlite3.dll 다루기 (저수준)
//
//  ★RecordStore가 쓰는 도우미다. 게임 코드는 이 파일을 볼 일이 없다.
//
//  래퍼 라이브러리 없이 DLL 함수를 직접 부른다(P/Invoke).
//  라이브러리를 안 쓰는 이유:
//    · 설치할 것이 sqlite3.dll 하나뿐이라 빌드가 단순하다
//    · 버전 충돌·플랫폼 설정 문제가 생기지 않는다
//
//  ⚠한글은 반드시 UTF-8 바이트로 주고받는다.
//    C# string을 그대로 넘기면 한글이 깨진다.
// ============================================================
public static class Sqlite
{
    // 결과 코드
    public const int OK = 0;
    public const int ROW = 100;    // 읽을 줄이 더 있다
    public const int DONE = 101;   // 다 읽었다

    // 문자열을 넘길 때 "복사해 가라"고 알리는 값
    static readonly IntPtr TRANSIENT = new IntPtr(-1);

    // ============================================================
    //  DLL 함수 선언
    // ============================================================

    [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_open(byte[] filename, out IntPtr db);

    [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_close(IntPtr db);

    [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_exec(IntPtr db, byte[] sql, IntPtr cb, IntPtr arg, out IntPtr err);

    [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr sqlite3_errmsg(IntPtr db);

    [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int nByte,
                                         out IntPtr stmt, IntPtr tail);

    [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_step(IntPtr stmt);

    [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_finalize(IntPtr stmt);

    [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_reset(IntPtr stmt);

    [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_bind_text(IntPtr stmt, int idx, byte[] val, int n, IntPtr free);

    [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_bind_int(IntPtr stmt, int idx, int val);

    [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_bind_double(IntPtr stmt, int idx, double val);

    [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr sqlite3_column_text(IntPtr stmt, int col);

    [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_column_int(IntPtr stmt, int col);

    [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern double sqlite3_column_double(IntPtr stmt, int col);

    // ============================================================
    //  문자열 변환 도우미
    // ============================================================

    // C# 문자열 → UTF-8 바이트 (끝에 0을 붙인다. C 언어 방식)
    static byte[] ToUtf8(string s)
    {
        if (s == null) s = "";

        byte[] raw = Encoding.UTF8.GetBytes(s);
        byte[] withNull = new byte[raw.Length + 1];

        Array.Copy(raw, withNull, raw.Length);
        withNull[raw.Length] = 0;

        return withNull;
    }

    // DLL이 준 UTF-8 주소 → C# 문자열
    static string FromUtf8(IntPtr p)
    {
        if (p == IntPtr.Zero) return "";

        // 0이 나올 때까지 세어서 길이를 구한다
        int len = 0;
        while (Marshal.ReadByte(p, len) != 0) len++;

        if (len == 0) return "";

        byte[] buf = new byte[len];
        Marshal.Copy(p, buf, 0, len);

        return Encoding.UTF8.GetString(buf);
    }

    // ============================================================
    //  바깥에서 쓰는 것들
    // ============================================================

    // 데이터베이스 열기 (파일이 없으면 새로 만든다)
    public static bool Open(string path, out IntPtr db)
    {
        int rc = sqlite3_open(ToUtf8(path), out db);
        return rc == OK;
    }

    public static void Close(IntPtr db)
    {
        if (db != IntPtr.Zero) sqlite3_close(db);
    }

    // 결과를 안 받는 SQL 실행 (CREATE, DELETE, BEGIN, COMMIT 등)
    public static bool Exec(IntPtr db, string sql, out string error)
    {
        IntPtr err;
        int rc = sqlite3_exec(db, ToUtf8(sql), IntPtr.Zero, IntPtr.Zero, out err);

        if (rc != OK)
        {
            error = FromUtf8(sqlite3_errmsg(db));
            return false;
        }

        error = "";
        return true;
    }

    // SQL 준비 (값을 끼워 넣거나 결과를 읽을 때)
    public static bool Prepare(IntPtr db, string sql, out IntPtr stmt)
    {
        int rc = sqlite3_prepare_v2(db, ToUtf8(sql), -1, out stmt, IntPtr.Zero);
        return rc == OK;
    }

    // 한 줄 실행 / 한 줄 읽기
    //   INSERT 등은 DONE이 돌아오고, SELECT는 줄이 있는 동안 ROW가 돌아온다
    public static int Step(IntPtr stmt)
    {
        return sqlite3_step(stmt);
    }

    public static void Finalize(IntPtr stmt)
    {
        if (stmt != IntPtr.Zero) sqlite3_finalize(stmt);
    }

    // 같은 SQL을 값만 바꿔 다시 쓰기 위해 되감기
    public static void Reset(IntPtr stmt)
    {
        if (stmt != IntPtr.Zero) sqlite3_reset(stmt);
    }

    // ===== 값 끼워 넣기 (자리 번호는 1부터) =====
    public static void BindText(IntPtr stmt, int idx, string val)
    {
        byte[] b = ToUtf8(val);
        // ⚠길이에서 끝의 0은 빼고 알려준다
        sqlite3_bind_text(stmt, idx, b, b.Length - 1, TRANSIENT);
    }

    public static void BindInt(IntPtr stmt, int idx, int val)
    {
        sqlite3_bind_int(stmt, idx, val);
    }

    public static void BindFloat(IntPtr stmt, int idx, float val)
    {
        sqlite3_bind_double(stmt, idx, val);
    }

    // ===== 값 읽기 (칸 번호는 0부터) =====
    public static string ColText(IntPtr stmt, int col)
    {
        return FromUtf8(sqlite3_column_text(stmt, col));
    }

    public static int ColInt(IntPtr stmt, int col)
    {
        return sqlite3_column_int(stmt, col);
    }

    public static float ColFloat(IntPtr stmt, int col)
    {
        return (float)sqlite3_column_double(stmt, col);
    }

    // 마지막 오류 메시지
    public static string ErrorMessage(IntPtr db)
    {
        return FromUtf8(sqlite3_errmsg(db));
    }
}