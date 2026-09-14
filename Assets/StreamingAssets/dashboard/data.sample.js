// ============================================================
//  data.sample.js — 개발 중 미리보기용 가짜 데이터
//
//  실제로는 Unity가 실행할 때 같은 폴더에 data.js를 만들어 넣습니다.
//  index.html은 data.js를 먼저 불러오고, 없을 때만 이 파일을 대신 씁니다.
//  (이 파일이 쓰일 때는 화면 위에 "미리보기용 가짜 데이터" 띠가 뜹니다)
//
//  모양은 data.js와 완전히 같습니다:
//    - avatar: 프로필 캐릭터 번호 0~7 (0 토끼 · 1 씨름 선수 · 2 펭귄 · 3 개구리 · 4 파리 · 5 포도 젤리 · 6 푸딩 젤리 · 7 토코)
//    - 스테이지 4는 한 판에 cheek/lip 두 줄, 7은 left/right 두 줄
//    - 스테이지 8·10은 hasEffort:false (maxEffort 의미 없음)
//    - 스테이지 5는 일부러 기록이 없음 (빈 카드 확인용)
//    - 8/15~8/24 열흘 쉼 (선 끊김 확인용)
// ============================================================
window.DASHBOARD_DATA = {
  "nickname": "신현주",
  "avatar": 0,
  "generatedAt": "2026-09-11 21:30",
  "totalPlays": 47,
  "records": [
    {
      "day": "2026-07-27",
      "playedAt": "2026-07-27 13:46:53",
      "stage": 1,
      "reps": 19,
      "maxEffort": 0.5587,
      "hasEffort": true,
      "side": ""
    },
    {
      "day": "2026-07-27",
      "playedAt": "2026-07-27 15:50:17",
      "stage": 6,
      "reps": 10,
      "maxEffort": 0.5446,
      "hasEffort": true,
      "side": ""
    },
    {
      "day": "2026-07-29",
      "playedAt": "2026-07-29 12:39:00",
      "stage": 7,
      "reps": 8,
      "maxEffort": 0.1599,
      "hasEffort": true,
      "side": "left"
    },
    {
      "day": "2026-07-29",
      "playedAt": "2026-07-29 12:39:00",
      "stage": 7,
      "reps": 5,
      "maxEffort": 0.161,
      "hasEffort": true,
      "side": "right"
    },
    {
      "day": "2026-07-29",
      "playedAt": "2026-07-29 14:08:35",
      "stage": 2,
      "reps": 3,
      "maxEffort": 0.2287,
      "hasEffort": true,
      "side": ""
    },
    {
      "day": "2026-07-31",
      "playedAt": "2026-07-31 15:33:22",
      "stage": 3,
      "reps": 10,
      "maxEffort": 0.2539,
      "hasEffort": true,
      "side": ""
    },
    {
      "day": "2026-07-31",
      "playedAt": "2026-07-31 17:46:20",
      "stage": 7,
      "reps": 7,
      "maxEffort": 0.1964,
      "hasEffort": true,
      "side": "left"
    },
    {
      "day": "2026-07-31",
      "playedAt": "2026-07-31 17:46:20",
      "stage": 7,
      "reps": 9,
      "maxEffort": 0.126,
      "hasEffort": true,
      "side": "right"
    },
    {
      "day": "2026-07-31",
      "playedAt": "2026-07-31 19:48:35",
      "stage": 1,
      "reps": 14,
      "maxEffort": 0.5924,
      "hasEffort": true,
      "side": ""
    },
    {
      "day": "2026-08-01",
      "playedAt": "2026-08-01 14:02:14",
      "stage": 8,
      "reps": 16,
      "maxEffort": 0,
      "hasEffort": false,
      "side": ""
    },
    {
      "day": "2026-08-03",
      "playedAt": "2026-08-03 15:07:04",
      "stage": 3,
      "reps": 12,
      "maxEffort": 0.2737,
      "hasEffort": true,
      "side": ""
    },
    {
      "day": "2026-08-05",
      "playedAt": "2026-08-05 15:28:34",
      "stage": 7,
      "reps": 9,
      "maxEffort": 0.198,
      "hasEffort": true,
      "side": "left"
    },
    {
      "day": "2026-08-05",
      "playedAt": "2026-08-05 15:28:34",
      "stage": 7,
      "reps": 8,
      "maxEffort": 0.1809,
      "hasEffort": true,
      "side": "right"
    },
    {
      "day": "2026-08-07",
      "playedAt": "2026-08-07 12:57:59",
      "stage": 4,
      "reps": 11,
      "maxEffort": 0.01926,
      "hasEffort": true,
      "side": "cheek"
    },
    {
      "day": "2026-08-07",
      "playedAt": "2026-08-07 12:57:59",
      "stage": 4,
      "reps": 11,
      "maxEffort": 0.2485,
      "hasEffort": true,
      "side": "lip"
    },
    {
      "day": "2026-08-09",
      "playedAt": "2026-08-09 15:25:29",
      "stage": 1,
      "reps": 18,
      "maxEffort": 0.6474,
      "hasEffort": true,
      "side": ""
    },
    {
      "day": "2026-08-09",
      "playedAt": "2026-08-09 16:31:36",
      "stage": 7,
      "reps": 12,
      "maxEffort": 0.199,
      "hasEffort": true,
      "side": "left"
    },
    {
      "day": "2026-08-09",
      "playedAt": "2026-08-09 16:31:36",
      "stage": 7,
      "reps": 10,
      "maxEffort": 0.1722,
      "hasEffort": true,
      "side": "right"
    },
    {
      "day": "2026-08-10",
      "playedAt": "2026-08-10 16:28:43",
      "stage": 8,
      "reps": 15,
      "maxEffort": 0,
      "hasEffort": false,
      "side": ""
    },
    {
      "day": "2026-08-12",
      "playedAt": "2026-08-12 12:23:15",
      "stage": 6,
      "reps": 5,
      "maxEffort": 0.6147,
      "hasEffort": true,
      "side": ""
    },
    {
      "day": "2026-08-13",
      "playedAt": "2026-08-13 14:06:36",
      "stage": 2,
      "reps": 5,
      "maxEffort": 0.268,
      "hasEffort": true,
      "side": ""
    },
    {
      "day": "2026-08-13",
      "playedAt": "2026-08-13 15:30:29",
      "stage": 10,
      "reps": 5,
      "maxEffort": 0,
      "hasEffort": false,
      "side": ""
    },
    {
      "day": "2026-08-14",
      "playedAt": "2026-08-14 16:26:05",
      "stage": 7,
      "reps": 7,
      "maxEffort": 0.2012,
      "hasEffort": true,
      "side": "left"
    },
    {
      "day": "2026-08-14",
      "playedAt": "2026-08-14 16:26:05",
      "stage": 7,
      "reps": 9,
      "maxEffort": 0.1913,
      "hasEffort": true,
      "side": "right"
    },
    {
      "day": "2026-08-25",
      "playedAt": "2026-08-25 17:50:03",
      "stage": 3,
      "reps": 8,
      "maxEffort": 0.3595,
      "hasEffort": true,
      "side": ""
    },
    {
      "day": "2026-08-25",
      "playedAt": "2026-08-25 20:24:38",
      "stage": 7,
      "reps": 7,
      "maxEffort": 0.2435,
      "hasEffort": true,
      "side": "left"
    },
    {
      "day": "2026-08-25",
      "playedAt": "2026-08-25 20:24:38",
      "stage": 7,
      "reps": 7,
      "maxEffort": 0.195,
      "hasEffort": true,
      "side": "right"
    },
    {
      "day": "2026-08-25",
      "playedAt": "2026-08-25 20:51:50",
      "stage": 9,
      "reps": 9,
      "maxEffort": 62.72,
      "hasEffort": true,
      "side": ""
    },
    {
      "day": "2026-08-26",
      "playedAt": "2026-08-26 12:28:33",
      "stage": 1,
      "reps": 23,
      "maxEffort": 0.8224,
      "hasEffort": true,
      "side": ""
    },
    {
      "day": "2026-08-26",
      "playedAt": "2026-08-26 15:37:49",
      "stage": 7,
      "reps": 11,
      "maxEffort": 0.2279,
      "hasEffort": true,
      "side": "left"
    },
    {
      "day": "2026-08-26",
      "playedAt": "2026-08-26 15:37:49",
      "stage": 7,
      "reps": 11,
      "maxEffort": 0.1793,
      "hasEffort": true,
      "side": "right"
    },
    {
      "day": "2026-08-28",
      "playedAt": "2026-08-28 15:24:06",
      "stage": 6,
      "reps": 10,
      "maxEffort": 0.7664,
      "hasEffort": true,
      "side": ""
    },
    {
      "day": "2026-08-29",
      "playedAt": "2026-08-29 15:01:27",
      "stage": 6,
      "reps": 12,
      "maxEffort": 0.8626,
      "hasEffort": true,
      "side": ""
    },
    {
      "day": "2026-08-31",
      "playedAt": "2026-08-31 14:50:41",
      "stage": 3,
      "reps": 10,
      "maxEffort": 0.3704,
      "hasEffort": true,
      "side": ""
    },
    {
      "day": "2026-08-31",
      "playedAt": "2026-08-31 16:31:37",
      "stage": 2,
      "reps": 5,
      "maxEffort": 0.2659,
      "hasEffort": true,
      "side": ""
    },
    {
      "day": "2026-09-02",
      "playedAt": "2026-09-02 12:39:20",
      "stage": 8,
      "reps": 15,
      "maxEffort": 0,
      "hasEffort": false,
      "side": ""
    },
    {
      "day": "2026-09-02",
      "playedAt": "2026-09-02 15:04:50",
      "stage": 7,
      "reps": 9,
      "maxEffort": 0.2285,
      "hasEffort": true,
      "side": "left"
    },
    {
      "day": "2026-09-02",
      "playedAt": "2026-09-02 15:04:50",
      "stage": 7,
      "reps": 5,
      "maxEffort": 0.2039,
      "hasEffort": true,
      "side": "right"
    },
    {
      "day": "2026-09-03",
      "playedAt": "2026-09-03 13:51:29",
      "stage": 7,
      "reps": 7,
      "maxEffort": 0.2504,
      "hasEffort": true,
      "side": "left"
    },
    {
      "day": "2026-09-03",
      "playedAt": "2026-09-03 13:51:29",
      "stage": 7,
      "reps": 6,
      "maxEffort": 0.1749,
      "hasEffort": true,
      "side": "right"
    },
    {
      "day": "2026-09-03",
      "playedAt": "2026-09-03 14:24:36",
      "stage": 1,
      "reps": 26,
      "maxEffort": 0.7931,
      "hasEffort": true,
      "side": ""
    },
    {
      "day": "2026-09-04",
      "playedAt": "2026-09-04 13:34:03",
      "stage": 8,
      "reps": 14,
      "maxEffort": 0,
      "hasEffort": false,
      "side": ""
    },
    {
      "day": "2026-09-04",
      "playedAt": "2026-09-04 15:31:12",
      "stage": 7,
      "reps": 6,
      "maxEffort": 0.2355,
      "hasEffort": true,
      "side": "left"
    },
    {
      "day": "2026-09-04",
      "playedAt": "2026-09-04 15:31:12",
      "stage": 7,
      "reps": 10,
      "maxEffort": 0.2192,
      "hasEffort": true,
      "side": "right"
    },
    {
      "day": "2026-09-04",
      "playedAt": "2026-09-04 17:40:54",
      "stage": 4,
      "reps": 9,
      "maxEffort": 0.01906,
      "hasEffort": true,
      "side": "cheek"
    },
    {
      "day": "2026-09-04",
      "playedAt": "2026-09-04 17:40:54",
      "stage": 4,
      "reps": 9,
      "maxEffort": 0.3414,
      "hasEffort": true,
      "side": "lip"
    },
    {
      "day": "2026-09-06",
      "playedAt": "2026-09-06 12:44:43",
      "stage": 8,
      "reps": 8,
      "maxEffort": 0,
      "hasEffort": false,
      "side": ""
    },
    {
      "day": "2026-09-06",
      "playedAt": "2026-09-06 15:56:43",
      "stage": 4,
      "reps": 11,
      "maxEffort": 0.02335,
      "hasEffort": true,
      "side": "cheek"
    },
    {
      "day": "2026-09-06",
      "playedAt": "2026-09-06 15:56:43",
      "stage": 4,
      "reps": 11,
      "maxEffort": 0.3415,
      "hasEffort": true,
      "side": "lip"
    },
    {
      "day": "2026-09-06",
      "playedAt": "2026-09-06 18:05:28",
      "stage": 7,
      "reps": 12,
      "maxEffort": 0.2251,
      "hasEffort": true,
      "side": "left"
    },
    {
      "day": "2026-09-06",
      "playedAt": "2026-09-06 18:05:28",
      "stage": 7,
      "reps": 8,
      "maxEffort": 0.2042,
      "hasEffort": true,
      "side": "right"
    },
    {
      "day": "2026-09-07",
      "playedAt": "2026-09-07 10:14:37",
      "stage": 1,
      "reps": 20,
      "maxEffort": 0.7875,
      "hasEffort": true,
      "side": ""
    },
    {
      "day": "2026-09-07",
      "playedAt": "2026-09-07 13:21:49",
      "stage": 3,
      "reps": 8,
      "maxEffort": 0.4399,
      "hasEffort": true,
      "side": ""
    },
    {
      "day": "2026-09-08",
      "playedAt": "2026-09-08 15:03:26",
      "stage": 1,
      "reps": 22,
      "maxEffort": 0.8551,
      "hasEffort": true,
      "side": ""
    },
    {
      "day": "2026-09-08",
      "playedAt": "2026-09-08 16:16:00",
      "stage": 8,
      "reps": 16,
      "maxEffort": 0,
      "hasEffort": false,
      "side": ""
    },
    {
      "day": "2026-09-09",
      "playedAt": "2026-09-09 15:58:42",
      "stage": 2,
      "reps": 4,
      "maxEffort": 0.2997,
      "hasEffort": true,
      "side": ""
    },
    {
      "day": "2026-09-09",
      "playedAt": "2026-09-09 16:18:50",
      "stage": 1,
      "reps": 26,
      "maxEffort": 0.86,
      "hasEffort": true,
      "side": ""
    },
    {
      "day": "2026-09-10",
      "playedAt": "2026-09-10 15:56:05",
      "stage": 8,
      "reps": 14,
      "maxEffort": 0,
      "hasEffort": false,
      "side": ""
    },
    {
      "day": "2026-09-10",
      "playedAt": "2026-09-10 17:57:27",
      "stage": 2,
      "reps": 4,
      "maxEffort": 0.2942,
      "hasEffort": true,
      "side": ""
    },
    {
      "day": "2026-09-11",
      "playedAt": "2026-09-11 11:22:34",
      "stage": 1,
      "reps": 16,
      "maxEffort": 0.7771,
      "hasEffort": true,
      "side": ""
    },
    {
      "day": "2026-09-11",
      "playedAt": "2026-09-11 13:01:03",
      "stage": 7,
      "reps": 12,
      "maxEffort": 0.2195,
      "hasEffort": true,
      "side": "left"
    },
    {
      "day": "2026-09-11",
      "playedAt": "2026-09-11 13:01:03",
      "stage": 7,
      "reps": 6,
      "maxEffort": 0.2288,
      "hasEffort": true,
      "side": "right"
    }
  ]
};
