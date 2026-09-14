/* ============================================================
   꿀떡꿀떡 기록 대시보드 — app.js

   - file:// 로 열리는 정적 페이지. fetch·모듈·CDN 없음.
   - window.DASHBOARD_DATA (Unity가 만든 data.js) 를 읽어 화면을 채운다.
   - ★판정·진단 문구는 절대 넣지 않는다. 숫자·선·날짜만 보여준다.
   - 데이터가 없거나 일부가 깨져도 화면은 깨지지 않아야 한다.
   ============================================================ */
(function () {
  'use strict';

  // ============================================================
  //  상수
  // ============================================================

  // 스테이지 정보 (이름·운동은 쉬운 말로)
  var STAGES = [
    { id: 1,  name: '계단 오르기',   move: '입 크게 벌리기',       repsLabel: '오른 계단', unit: '개',   effortLabel: '입 벌린 크기' },
    { id: 2,  name: '줄다리기',      move: '입술 다물기',           repsLabel: '끝낸 횟수', unit: '번',   effortLabel: '입술 다문 힘' },
    { id: 3,  name: '하트',          move: '입술 오므리기',         repsLabel: '하트',      unit: '개',   effortLabel: '입술 오므린 크기' },
    { id: 4,  name: '펭귄 슬라이딩', move: '볼 부풀리기',           repsLabel: '발사',      unit: '번',   effortLabel: '볼·입술 크기', separateScales: true,
      sides: [ { key: 'cheek', label: '볼',     effortLabel: '볼 크기' },
               { key: 'lip',   label: '입술',   effortLabel: '입술 크기' } ] },
    { id: 5,  name: '미소 사진관',   move: '미소 짓기',             repsLabel: '사진',      unit: '장',   effortLabel: '미소 크기' },
    { id: 6,  name: '개구리',        move: '혀 내밀기',             repsLabel: '잡은 파리', unit: '마리', effortLabel: '혀 내민 크기' },
    { id: 7,  name: '젤리 분류',     move: '혀 좌우로 움직이기',    repsLabel: '옮긴 젤리', unit: '개',   effortLabel: '혀 좌우로 뻗은 크기',
      sides: [ { key: 'left',  label: '왼쪽',   effortLabel: '왼쪽으로 뻗은 크기' },
               { key: 'right', label: '오른쪽', effortLabel: '오른쪽으로 뻗은 크기' } ] },
    { id: 8,  name: '러너',          move: '혀 위아래로 움직이기',  repsLabel: '통과',      unit: '번',   countOnly: true },
    { id: 9,  name: '민들레',        move: '기침하기',              repsLabel: '기침',      unit: '번',   effortLabel: '기침 세기' },
    { id: 10, name: '떠먹이기',      move: '삼킴 순서 연습',        repsLabel: '떠먹인 횟수', unit: '번', countOnly: true }
  ];

  var STAGE_BY_ID = {};
  STAGES.forEach(function (s) { STAGE_BY_ID[s.id] = s; });

  // 성장 단계 (누적 판 수 기준 — 절대 내려가지 않는다)
  var GROWTH = [
    { min: 0,  title: '씨앗을 심었어요' },
    { min: 5,  title: '새싹이 났어요' },
    { min: 15, title: '잎이 여럿 났어요' },
    { min: 30, title: '작은 나무가 됐어요' },
    { min: 50, title: '큰 나무가 됐어요' },
    { min: 80, title: '열매가 열렸어요' }
  ];
  var GROWTH_FINAL_MESSAGE = '나무가 다 자랐어요. 함께해 온 시간이 열매가 됐어요.';

  // 추이 그래프: 기록 사이가 이 날수보다 벌어지면 선을 끊는다
  var MAX_GAP_DAYS = 7;

  // 기간 버튼 (0 = 전체)
  var RANGES = [
    { days: 30, label: '한 달' },
    { days: 90, label: '석 달' },
    { days: 0,  label: '전체' }
  ];

  // 선 색 (style.css 팔레트와 같은 값)
  var COLOR = {
    coral: '#E87B5E',
    teal: '#8FBFD0',
    tealLine: '#5FA0B8',   // 두 번째 선 — 팔레트 청록을 흰 바탕에서 보이도록 조금 진하게
    tealInk: '#3E7F96',
    ink: '#4A3B33',
    inkSoft: '#8A7A70',
    line: '#E6D9CF'
  };

  var DOW = ['일', '월', '화', '수', '목', '금', '토'];

  // 프로필 캐릭터 (Unity ProfileStore.AVATAR_NAMES 와 같은 순서) — icons/0.png ~ icons/7.png
  var AVATAR_NAMES = ['토끼', '씨름 선수', '펭귄', '개구리', '파리', '포도 젤리', '푸딩 젤리', '토코'];

  // 아이콘 파일 위치 (미리보기 번들은 window.DASHBOARD_ICON_SRC 로 data URI 를 넣어 줄 수 있다)
  function avatarSrc(n) {
    var map = window.DASHBOARD_ICON_SRC;
    if (map && map[n]) return map[n];
    return 'icons/' + n + '.png';
  }

  // ============================================================
  //  작은 도구들
  // ============================================================

  function $(id) { return document.getElementById(id); }

  function clear(node) {
    while (node && node.firstChild) node.removeChild(node.firstChild);
  }

  // HTML 요소 만들기: el('div', { class: 'a', 'aria-label': 'x' }, ['글자', otherEl])
  function el(tag, attrs, children) {
    var node = document.createElement(tag);
    if (attrs) {
      Object.keys(attrs).forEach(function (k) {
        var v = attrs[k];
        if (v === null || v === undefined || v === false) return;
        node.setAttribute(k, String(v));
      });
    }
    appendChildren(node, children);
    return node;
  }

  // SVG 요소 만들기
  var SVG_NS = 'http://www.w3.org/2000/svg';
  function svgEl(tag, attrs, children) {
    var node = document.createElementNS(SVG_NS, tag);
    if (attrs) {
      Object.keys(attrs).forEach(function (k) {
        var v = attrs[k];
        if (v === null || v === undefined || v === false) return;
        node.setAttribute(k, String(v));
      });
    }
    appendChildren(node, children);
    return node;
  }

  function appendChildren(node, children) {
    if (children === null || children === undefined) return;
    if (!Array.isArray(children)) children = [children];
    children.forEach(function (c) {
      if (c === null || c === undefined || c === false) return;
      if (typeof c === 'string' || typeof c === 'number') node.appendChild(document.createTextNode(String(c)));
      else node.appendChild(c);
    });
  }

  function pad2(n) { return (n < 10 ? '0' : '') + n; }

  // ----- 날짜 (yyyy-MM-dd 문자열 ↔ 일 번호) -----
  var DAY_RE = /^(\d{4})-(\d{2})-(\d{2})$/;

  function dayToNum(day) {
    var m = DAY_RE.exec(day || '');
    if (!m) return null;
    var y = +m[1], mo = +m[2], d = +m[3];
    var t = Date.UTC(y, mo - 1, d);
    var dt = new Date(t);
    if (dt.getUTCFullYear() !== y || dt.getUTCMonth() !== mo - 1 || dt.getUTCDate() !== d) return null;
    return Math.round(t / 86400000);
  }

  function isDay(day) { return typeof day === 'string' && dayToNum(day) !== null; }

  function numToDay(n) {
    var dt = new Date(n * 86400000);
    return dt.getUTCFullYear() + '-' + pad2(dt.getUTCMonth() + 1) + '-' + pad2(dt.getUTCDate());
  }

  function addDays(day, n) { return numToDay(dayToNum(day) + n); }

  function dayParts(day) {
    var m = DAY_RE.exec(day);
    return { y: +m[1], m: +m[2], d: +m[3] };
  }

  function weekdayOf(day) { return new Date(dayToNum(day) * 86400000).getUTCDay(); }

  function todayLocal() {
    var d = new Date();
    return d.getFullYear() + '-' + pad2(d.getMonth() + 1) + '-' + pad2(d.getDate());
  }

  // "2026-09-11" → "9월 11일" / withYear → "2026년 9월 11일" / withDow → "9월 11일 (금)"
  function fmtDay(day, withYear, withDow) {
    if (!isDay(day)) return String(day || '');
    var p = dayParts(day);
    var s = (withYear ? p.y + '년 ' : '') + p.m + '월 ' + p.d + '일';
    if (withDow) s += ' (' + DOW[weekdayOf(day)] + ')';
    return s;
  }

  // "2026-09-11 21:30" → "2026년 9월 11일 21:30"
  function fmtDateTime(s) {
    var m = /^(\d{4}-\d{2}-\d{2})[ T](\d{2}:\d{2})/.exec(s || '');
    if (!m || !isDay(m[1])) return String(s || '');
    return fmtDay(m[1], true) + ' ' + m[2];
  }

  // "2026-09-11 16:41:51" → "16:41"
  function fmtTime(playedAt) {
    var m = /[ T](\d{2}:\d{2})/.exec(playedAt || '');
    return m ? m[1] : '';
  }

  // 유효숫자 3자리 (볼 0.0159 처럼 작은 값이 뭉개지지 않게)
  function fmtSig(v) {
    if (typeof v !== 'number' || !isFinite(v)) return '-';
    if (v === 0) return '0';
    if (Math.abs(v) >= 999.5) return String(Math.round(v));   // toPrecision(3)의 반올림 경계와 맞춤
    var s = v.toPrecision(3);
    if (s.indexOf('e') !== -1) s = v.toFixed(6);   // 아주 작은 값
    return s;
  }

  // 세로축 범위: 보이는 값의 최저~최고에 여유를 조금 두고 "보기 좋은" 눈금 간격으로 맞춘다.
  // 변화가 보이도록 0부터 시작하지 않을 수 있다. 값이 하나뿐이거나 전부 같으면 폭을 임의로 잡는다.
  function niceRange(minV, maxV) {
    if (!isFinite(minV) || !isFinite(maxV)) { minV = 0; maxV = 1; }
    if (maxV < minV) { var t = minV; minV = maxV; maxV = t; }
    var nonNegative = minV >= 0;
    var span = maxV - minV;
    // 값 차이가 아주 작으면(예: 0.3414 vs 0.3415) 그 차이를 과장하지 않도록 값 크기의 10%를 최소 폭으로 둔다
    var minSpan = Math.max(Math.abs(minV), Math.abs(maxV)) * 0.1;
    if (span < minSpan) {
      var center = (minV + maxV) / 2;
      minV = center - minSpan / 2;
      maxV = center + minSpan / 2;
      span = minSpan;
    }
    var pad = span > 0 ? span * 0.15 : 0.5;   // 전부 0이면 임의의 폭
    var lo = minV - pad;
    var hi = maxV + pad;
    if (lo < 0 && nonNegative) lo = 0;        // 값이 음수가 아니면 0 아래로 내려가지 않는다
    var step = niceStep((hi - lo) / 4);
    var count = Math.ceil(hi / step) - Math.floor(lo / step);
    var guard = 0;
    while (count > 7 && guard++ < 6) {        // 눈금이 너무 많으면 간격을 키운다
      step = niceStep(step * 2.01);
      count = Math.ceil(hi / step) - Math.floor(lo / step);
    }
    lo = Math.floor(lo / step + 1e-9) * step;
    hi = Math.ceil(hi / step - 1e-9) * step;
    if (hi <= lo) hi = lo + step;
    var decimals = decimalsFor(step);
    var ticks = [];
    var n = Math.round((hi - lo) / step);
    for (var i = 0; i <= n; i++) ticks.push(Number((lo + i * step).toFixed(decimals + 2)));
    return { lo: lo, hi: hi, step: step, decimals: decimals, ticks: ticks, fromZero: lo <= 0 };
  }

  // 1·2·2.5·5·10 × 10^k 중 raw 이상인 가장 작은 값
  function niceStep(raw) {
    if (!(raw > 0)) return 1;
    var exp = Math.floor(Math.log(raw) / Math.LN10);
    var mag = Math.pow(10, exp);
    var n = raw / mag;
    var nice = (n <= 1 + 1e-9) ? 1 : (n <= 2 + 1e-9) ? 2 : (n <= 2.5 + 1e-9) ? 2.5 : (n <= 5 + 1e-9) ? 5 : 10;
    return nice * mag;
  }

  // 눈금 간격을 표시하는 데 필요한 소수 자릿수
  function decimalsFor(step) {
    for (var dec = 0; dec < 8; dec++) {
      var x = step * Math.pow(10, dec);
      if (Math.abs(x - Math.round(x)) < 1e-6) return dec;
    }
    return 8;
  }

  function debounce(fn, ms) {
    var t = null;
    return function () {
      if (t) clearTimeout(t);
      t = setTimeout(fn, ms);
    };
  }

  // ============================================================
  //  데이터 정리
  //  raw(window.DASHBOARD_DATA) → 화면이 쓰기 좋은 모양으로
  // ============================================================
  function buildModel(raw) {
    var m = {
      nickname: '',
      generatedAt: '',
      avatar: 0,          // 캐릭터 번호 0~7 (없거나 범위 밖이면 0)
      today: '',
      totalPlays: 0,
      records: [],
      plays: [],          // 한 판 = { key, stage, day, playedAt, rows[] }  (4·7번은 rows가 2줄)
      playsByDay: {},     // day → plays[]
      stage: {},          // stageId → { plays[], daily: { sideKey: { day: {day, value, plays} } } }
      days: [],           // 기록이 있는 날 (정렬)
      firstDay: null,
      lastDay: null
    };

    if (!raw || typeof raw !== 'object') raw = {};

    m.nickname = (typeof raw.nickname === 'string') ? raw.nickname.trim() : '';
    m.generatedAt = (typeof raw.generatedAt === 'string') ? raw.generatedAt.trim() : '';

    var t = m.generatedAt.slice(0, 10);
    m.today = isDay(t) ? t : todayLocal();

    var av = Number(raw.avatar);
    m.avatar = (isFinite(av) && Math.round(av) === av && av >= 0 && av < AVATAR_NAMES.length) ? av : 0;

    var list = Array.isArray(raw.records) ? raw.records : [];
    var playMap = {};

    for (var i = 0; i < list.length; i++) {
      var r = list[i];
      if (!r || typeof r !== 'object') continue;

      var stage = Math.round(Number(r.stage));
      if (!(stage >= 1 && stage <= 10)) continue;
      var def = STAGE_BY_ID[stage];

      var playedAt = (typeof r.playedAt === 'string') ? r.playedAt.trim() : '';
      var day = isDay(r.day) ? r.day : (isDay(playedAt.slice(0, 10)) ? playedAt.slice(0, 10) : null);
      if (!day) continue;
      if (!playedAt) playedAt = day + ' 00:00:00';

      var reps = Number(r.reps);
      reps = isFinite(reps) ? Math.max(0, Math.round(reps)) : 0;

      // 숫자(또는 숫자 문자열)만 값으로 인정한다. null·true 같은 깨진 값을 0·1로 그리지 않는다.
      var rawEffort = r.maxEffort;
      var maxEffort = (typeof rawEffort === 'number') ? rawEffort
        : ((typeof rawEffort === 'string' && rawEffort.trim() !== '') ? Number(rawEffort) : NaN);
      // 8·10번은 최대값이 없다. hasEffort가 false면 값이 있어도 쓰지 않는다.
      var hasEffort = !def.countOnly && r.hasEffort !== false && isFinite(maxEffort);

      var side = (typeof r.side === 'string') ? r.side.trim().toLowerCase() : '';
      if (!def.sides) side = '';

      var rec = {
        stage: stage,
        day: day,
        playedAt: playedAt,
        reps: reps,
        maxEffort: hasEffort ? maxEffort : null,
        hasEffort: hasEffort,
        side: side
      };
      m.records.push(rec);

      // ★4·7번은 한 판에 두 줄 → stage|playedAt 으로 묶어 한 판으로 센다
      var key = stage + '|' + playedAt;
      var play = playMap[key];
      if (!play) {
        play = { key: key, stage: stage, day: day, playedAt: playedAt, rows: [] };
        playMap[key] = play;
        m.plays.push(play);
      }
      play.rows.push(rec);
    }

    m.plays.sort(function (a, b) {
      if (a.playedAt !== b.playedAt) return a.playedAt < b.playedAt ? -1 : 1;
      return a.stage - b.stage;
    });

    var tp = Number(raw.totalPlays);
    m.totalPlays = (isFinite(tp) && tp >= 0) ? Math.round(tp) : m.plays.length;

    m.plays.forEach(function (p) {
      if (!m.playsByDay[p.day]) m.playsByDay[p.day] = [];
      m.playsByDay[p.day].push(p);

      var st = m.stage[p.stage];
      if (!st) st = m.stage[p.stage] = { plays: [], daily: {} };
      st.plays.push(p);

      p.rows.forEach(function (rec) {
        if (!rec.hasEffort) return;
        var dm = st.daily[rec.side];
        if (!dm) dm = st.daily[rec.side] = {};
        var dp = dm[rec.day];
        // ★같은 날 여러 판이면 최고값 하나만 (평균 금지)
        if (!dp) dm[rec.day] = { day: rec.day, value: rec.maxEffort, plays: 1 };
        else {
          dp.plays += 1;
          if (rec.maxEffort > dp.value) dp.value = rec.maxEffort;
        }
      });
    });

    m.days = Object.keys(m.playsByDay).sort();
    m.firstDay = m.days.length ? m.days[0] : null;
    m.lastDay = m.days.length ? m.days[m.days.length - 1] : null;

    return m;
  }

  // 스테이지·side의 날짜별 최고값 목록 (날짜순)
  function dailyPoints(m, stageId, sideKey) {
    var st = m.stage[stageId];
    if (!st || !st.daily[sideKey]) return [];
    var dm = st.daily[sideKey];
    return Object.keys(dm).sort().map(function (d) { return dm[d]; });
  }

  // ============================================================
  //  헤더
  // ============================================================
  function renderHeader(m) {
    var who = m.nickname ? m.nickname + ' 님의 기록' : '나의 기록';
    $('page-title').textContent = '꿀떡꿀떡 · ' + who;
    $('page-sub').textContent = m.generatedAt ? fmtDateTime(m.generatedAt) + ' 기준' : fmtDay(m.today, true) + ' 기준';
    document.title = '꿀떡꿀떡 · ' + who;

    // 캐릭터 아이콘 — 다 읽힌 뒤에만 보여 주고, 못 읽으면 기본 꿀떡 그림을 그대로 둔다
    var img = $('avatar-img');
    var fallback = $('brand-fallback');
    if (!img) return;
    var n = m.avatar;
    img.alt = AVATAR_NAMES[n] + ' 캐릭터';
    img.onload = function () {
      img.hidden = false;
      if (fallback) fallback.hidden = true;
      // 아주 작은 도트 그림(16px 등)은 뭉개지지 않게 픽셀 그대로 키운다
      if (img.naturalWidth > 0 && img.naturalWidth < 64) img.classList.add('is-pixel');
    };
    img.onerror = function () {
      img.hidden = true;
      if (fallback) fallback.hidden = false;
    };
    img.src = avatarSrc(n);
  }

  // ============================================================
  //  요약 숫자 카드 3개
  // ============================================================
  function renderSummary(m) {
    // 1) 이번 주 판 수 — 오늘 포함 최근 7일
    var from = addDays(m.today, -6);
    var week = m.plays.filter(function (p) { return p.day >= from && p.day <= m.today; }).length;
    $('stat-week').textContent = week;
    $('stat-week-desc').textContent = fmtDay(from) + ' ~ ' + fmtDay(m.today) + ', 최근 7일이에요';

    // 2) 꾸준히 한 날 — 기록이 있는 날의 수
    var days = m.days.length;
    $('stat-days').textContent = days;
    $('stat-days-desc').textContent = days > 0 ? '지금까지 ' + days + '일 했어요' : '아직 기록이 없어요';

    // 3) 가장 많이 한 운동 — 판 수가 제일 많은 스테이지 (같으면 더 최근에 한 쪽)
    var best = null;
    STAGES.forEach(function (s) {
      var st = m.stage[s.id];
      if (!st) return;
      var n = st.plays.length;
      var last = st.plays[n - 1].playedAt;
      if (!best || n > best.n || (n === best.n && last > best.last)) best = { def: s, n: n, last: last };
    });
    if (best) {
      $('stat-top').textContent = best.def.name;
      $('stat-top-desc').textContent = best.def.move + ' · ' + best.n + '판 했어요';
    } else {
      $('stat-top').textContent = '아직 없어요';
      $('stat-top-desc').textContent = '게임을 하면 여기에 나와요';
    }
  }

  // ============================================================
  //  성장 카드 — 누적 판 수로 식물이 자란다
  // ============================================================
  function renderGrowth(m) {
    var total = m.totalPlays;
    var idx = 0;
    for (var i = 0; i < GROWTH.length; i++) {
      if (total >= GROWTH[i].min) idx = i;
    }
    var g = GROWTH[idx];
    var next = GROWTH[idx + 1];

    var fig = $('growth-figure');
    fig.innerHTML = plantSvg(idx + 1);

    $('growth-stage').textContent = g.title;
    $('growth-total').textContent = total;
    $('growth-next').textContent = next
      ? '다음 단계까지 ' + (next.min - total) + '판'
      : GROWTH_FINAL_MESSAGE;

    var steps = $('growth-steps');
    clear(steps);
    for (i = 0; i < GROWTH.length; i++) {
      steps.appendChild(el('span', { 'class': 'growth-step' + (i <= idx ? ' is-done' : '') }));
    }
    steps.setAttribute('aria-label', GROWTH.length + '단계 중 ' + (idx + 1) + '단계');
  }

  // ============================================================
  //  선그래프 (추이 카드·좌우 균형 카드가 같이 쓴다)
  //
  //  opts = {
  //    container, series: [{ name, color, points: [{day, value}] }],
  //    startDay, endDay, valueLabel(툴팁용), ariaLabel, height, maxGapDays
  //  }
  //  - 가로 = 날짜(간격 그대로), 세로 = 그날 최고값
  //  - 기록 사이가 maxGapDays보다 벌어지면 선을 끊는다 (0으로 잇지 않는다)
  // ============================================================
  function drawLineChart(opts) {
    var box = opts.container;
    clear(box);

    var width = Math.max(280, Math.floor(box.clientWidth || 600));
    var height = opts.height || 340;
    var margin = { top: 24, right: 44, bottom: 58, left: 78 };
    var plotW = width - margin.left - margin.right;
    var plotH = height - margin.top - margin.bottom;

    var startN = dayToNum(opts.startDay);
    var endN = dayToNum(opts.endDay);
    if (endN < startN) { var tmp = startN; startN = endN; endN = tmp; }
    var nDays = endN - startN;

    // 보이는 기간 안의 최저·최고값으로 세로축 범위를 잡는다
    var minV = Infinity, maxV = -Infinity;
    opts.series.forEach(function (s) {
      s.points.forEach(function (p) {
        var n = dayToNum(p.day);
        if (n < startN || n > endN) return;
        if (p.value < minV) minV = p.value;
        if (p.value > maxV) maxV = p.value;
      });
    });
    if (minV === Infinity) { minV = 0; maxV = 1; }
    var scale = niceRange(minV, maxV);

    function xOf(n) {
      if (nDays === 0) return margin.left + plotW / 2;
      return margin.left + ((n - startN) / nDays) * plotW;
    }
    function yOf(v) {
      return margin.top + plotH - ((v - scale.lo) / (scale.hi - scale.lo)) * plotH;
    }

    var root = svgEl('svg', {
      width: width, height: height,
      viewBox: '0 0 ' + width + ' ' + height,
      role: 'img',
      'aria-label': opts.ariaLabel || ''
    });

    // 가로 격자 + 세로축 숫자 (맨 아래 눈금이 축)
    scale.ticks.forEach(function (v, i) {
      var y = yOf(v);
      var isAxis = i === 0;
      root.appendChild(svgEl('line', {
        x1: margin.left, x2: margin.left + plotW, y1: y, y2: y,
        stroke: isAxis ? COLOR.ink : COLOR.line,
        'stroke-width': isAxis ? 2 : 1.5,
        'stroke-dasharray': isAxis ? null : '4 6',
        'stroke-opacity': isAxis ? 0.6 : 1
      }));
      root.appendChild(svgEl('text', {
        x: margin.left - 12, y: y + 6, 'text-anchor': 'end', 'font-size': 16
      }, v.toFixed(scale.decimals)));
    });

    // 날짜 글자 (오늘부터 거꾸로 일정 간격으로 — 마지막 날은 항상 표시)
    var maxLabels = Math.max(2, Math.floor(plotW / 100));
    var stepDays = nDays === 0 ? 1 : Math.max(1, Math.ceil(nDays / (maxLabels - 1)));
    var axisY = margin.top + plotH;
    for (var n = endN; n >= startN; n -= stepDays) {
      var x = xOf(n);
      root.appendChild(svgEl('line', {
        x1: x, x2: x, y1: axisY, y2: axisY + 6, stroke: COLOR.ink, 'stroke-opacity': 0.6, 'stroke-width': 2
      }));
      root.appendChild(svgEl('text', {
        x: x, y: axisY + 30, 'text-anchor': 'middle', 'font-size': 16
      }, fmtDay(numToDay(n))));
      if (nDays === 0) break;
    }

    // 툴팁
    var tip = el('div', { 'class': 'chart-tooltip', role: 'status' });
    tip.hidden = true;
    var pinned = false;

    function showTip(cx, cy, html) {
      tip.innerHTML = html;
      tip.hidden = false;
      tip.style.left = '0px';
      var w = tip.offsetWidth || 0;
      var minX = w / 2 + 4, maxX = width - w / 2 - 4;
      tip.style.left = Math.max(minX, Math.min(maxX, cx)) + 'px';
      tip.style.top = cy + 'px';
    }
    function hideTip() { if (!pinned) tip.hidden = true; }

    // 점 → 선 → 누르는 영역 순서로 그린다 (선이 점 위를 지나가 끊겨 보이지 않게)
    var maxGap = (typeof opts.maxGapDays === 'number') ? opts.maxGapDays : MAX_GAP_DAYS;
    var dots = [];
    var paths = [];

    opts.series.forEach(function (s) {
      var d = '';
      var prevN = null;
      s.points.forEach(function (p) {
        var pn = dayToNum(p.day);
        if (pn < startN || pn > endN) return;
        var px = xOf(pn), py = yOf(p.value);
        if (prevN === null || pn - prevN > maxGap) d += 'M' + px + ' ' + py;
        else d += ' L' + px + ' ' + py;
        prevN = pn;
        dots.push({ x: px, y: py, series: s, point: p });
      });
      if (d) paths.push({ d: d, color: s.color });
    });

    dots.forEach(function (dot) {
      root.appendChild(svgEl('circle', {
        cx: dot.x, cy: dot.y, r: 6.5, fill: dot.series.color, stroke: '#FFFFFF', 'stroke-width': 2
      }));
    });

    // 모든 선은 같은 굵기의 실선 — 구분은 색으로만
    paths.forEach(function (p) {
      root.appendChild(svgEl('path', {
        d: p.d, fill: 'none', stroke: p.color, 'stroke-width': 4,
        'stroke-linecap': 'round', 'stroke-linejoin': 'round'
      }));
    });

    // 누르거나 올려놓을 수 있는 넓은 영역 (점 위에 투명하게)
    dots.forEach(function (dot) {
      var hit = svgEl('circle', {
        cx: dot.x, cy: dot.y, r: 18, fill: 'transparent', 'class': 'chart-hit', tabindex: 0,
        'aria-label': fmtDay(dot.point.day) + ' ' + (dot.series.name ? dot.series.name + ' ' : '') + fmtSig(dot.point.value)
      });
      var html = '<strong>' + fmtDay(dot.point.day, false, true) + '</strong> · '
        + (dot.series.name ? dot.series.name + ' ' : '') + '<strong>' + fmtSig(dot.point.value) + '</strong>';
      hit.addEventListener('mouseenter', function () { if (!pinned) showTip(dot.x, dot.y, html); });
      hit.addEventListener('mouseleave', hideTip);
      hit.addEventListener('focus', function () { if (!pinned) showTip(dot.x, dot.y, html); });
      hit.addEventListener('blur', hideTip);
      hit.addEventListener('click', function (e) {
        e.stopPropagation();
        pinned = true;
        showTip(dot.x, dot.y, html);
      });
      root.appendChild(hit);
    });

    // 빈 곳을 누르면 고정된 툴팁을 닫는다
    root.addEventListener('click', function () { pinned = false; tip.hidden = true; });

    box.appendChild(root);
    box.appendChild(tip);
    return scale;
  }

  function showChartEmpty(box, message) {
    clear(box);
    box.appendChild(el('div', { 'class': 'chart-empty' }, message));
  }

  function renderLegend(container, series) {
    clear(container);
    if (series.length < 2) return;
    series.forEach(function (s) {
      container.appendChild(el('span', { 'class': 'legend-item' }, [
        el('span', { 'class': 'legend-dot', style: 'background:' + s.color }),
        s.name
      ]));
    });
  }

  // ============================================================
  //  회복 추이 카드
  // ============================================================
  var trendState = { stage: 1, range: 30 };

  function renderTrendControls(m) {
    var tabs = $('trend-tabs');
    var ranges = $('trend-range');

    // 버튼은 처음 한 번만 만든다 (누른 버튼이 사라지면 키보드 포커스가 날아가므로)
    if (!tabs.firstChild) {
      STAGES.forEach(function (s) {
        if (s.countOnly) return;
        var has = !!m.stage[s.id];   // 한 번도 안 한 게임은 흐리게 (눌 수는 있음)
        var b = el('button', {
          type: 'button',
          'class': 'tab' + (has ? '' : ' is-empty'),
          'data-stage': s.id,
          'aria-label': has ? null : s.name + ', 아직 기록 없음'
        }, s.name);
        b.addEventListener('click', function () {
          trendState.stage = s.id;
          syncTrendControls();
          renderTrend(m);
        });
        tabs.appendChild(b);
      });
      RANGES.forEach(function (r) {
        var b = el('button', { type: 'button', 'class': 'range-btn', 'data-days': r.days }, r.label);
        b.addEventListener('click', function () {
          trendState.range = r.days;
          syncTrendControls();
          renderTrend(m);
        });
        ranges.appendChild(b);
      });
    }
    syncTrendControls();
  }

  // 눌린 상태(aria-pressed)만 맞춘다
  function syncTrendControls() {
    var i, list;
    list = $('trend-tabs').querySelectorAll('.tab');
    for (i = 0; i < list.length; i++) {
      list[i].setAttribute('aria-pressed', String(+list[i].getAttribute('data-stage') === trendState.stage));
    }
    list = $('trend-range').querySelectorAll('.range-btn');
    for (i = 0; i < list.length; i++) {
      list[i].setAttribute('aria-pressed', String(+list[i].getAttribute('data-days') === trendState.range));
    }
  }

  function renderTrend(m) {
    var def = STAGE_BY_ID[trendState.stage];
    var st = m.stage[def.id];
    var box = $('trend-chart');

    $('trend-title').textContent = def.effortLabel;
    $('trend-sub').textContent = def.move + (st ? ' · 지금까지 ' + st.plays.length + '판 했어요' : '');

    var sides = def.sides || [{ key: '', label: '' }];
    var colors = [COLOR.coral, COLOR.tealLine];
    var series = sides.map(function (s, i) {
      return { name: s.label, color: colors[i], points: dailyPoints(m, def.id, s.key) };
    });
    // 볼·입술처럼 값 크기가 10배 넘게 다른 두 선은 각자 눈금으로 따로 그린다 → 범례 대신 소제목
    renderLegend($('trend-legend'), def.separateScales ? [] : series);

    var allPoints = [];
    series.forEach(function (s) { allPoints = allPoints.concat(s.points); });

    var end = m.today;
    var start;
    if (trendState.range === 0) {
      var first = null;
      allPoints.forEach(function (p) { if (!first || p.day < first) first = p.day; });
      start = first || addDays(end, -29);
      if (dayToNum(end) - dayToNum(start) < 13) start = addDays(end, -13);
    } else {
      start = addDays(end, -(trendState.range - 1));
    }

    var caption = $('trend-caption');
    var recent = $('trend-recent');
    var note = $('trend-note');
    box.className = 'chart-box';
    note.textContent = '';

    if (allPoints.length === 0) {
      showChartEmpty(box, '아직 기록이 없어요. 게임을 하면 여기에 나와요.');
      caption.textContent = '';
      recent.textContent = '';
      return;
    }

    var inWindow = allPoints.some(function (p) { return p.day >= start && p.day <= end; });
    var hasPast = allPoints.some(function (p) { return p.day <= end; });

    if (!inWindow) {
      if (trendState.range === 0 || !hasPast) {
        showChartEmpty(box, '이 기간에는 그릴 기록이 없어요');
      } else {
        var rangeLabel = trendState.range + '일';
        RANGES.forEach(function (r) { if (r.days === trendState.range) rangeLabel = r.label; });
        showChartEmpty(box, '최근 ' + rangeLabel + '에는 기록이 없어요. 위의 ‘전체’를 누르면 지난 기록을 볼 수 있어요.');
      }
    } else if (def.separateScales) {
      // 선마다 자기 눈금으로, 위아래로 쌓아서
      clear(box);
      box.className = 'chart-box chart-box-stack';
      series.forEach(function (s) {
        var panel = el('div', { 'class': 'chart-panel' });
        panel.appendChild(el('h4', { 'class': 'chart-panel-title' }, [
          el('span', { 'class': 'legend-dot', style: 'background:' + s.color }),
          s.name
        ]));
        var inner = el('div', { 'class': 'chart-panel-box' });
        panel.appendChild(inner);
        box.appendChild(panel);
        var sc = drawLineChart({
          container: inner,
          series: [s],
          startDay: start,
          endDay: end,
          ariaLabel: def.name + ' ' + s.name + ' 추이 그래프',
          height: 250
        });
        if (sc && !sc.fromZero) note.textContent = '변화가 잘 보이도록 0부터 시작하지 않아요';
      });
    } else {
      var sc1 = drawLineChart({
        container: box,
        series: series,
        startDay: start,
        endDay: end,
        ariaLabel: def.name + ' ' + def.effortLabel + ' 추이 그래프',
        height: 340
      });
      if (sc1 && !sc1.fromZero) note.textContent = '변화가 잘 보이도록 0부터 시작하지 않아요';
    }

    caption.textContent = fmtDay(start, true) + ' ~ ' + fmtDay(end, true) + ' · 일주일 넘게 쉬면 선이 끊겨요';

    // 가장 최근 기록 (기간과 상관없이)
    var lastDay = null;
    allPoints.forEach(function (p) { if (!lastDay || p.day > lastDay) lastDay = p.day; });
    var parts = [];
    series.forEach(function (s) {
      var p = null;
      s.points.forEach(function (q) { if (q.day === lastDay) p = q; });
      if (p) parts.push((s.name ? s.name + ' ' : '') + fmtSig(p.value));
    });
    recent.textContent = '가장 최근 기록 ' + fmtDay(lastDay, false, true) + ' · ' + parts.join(' · ');
  }

  // ============================================================
  //  좌우 균형 카드 (스테이지 7 왼쪽·오른쪽)
  // ============================================================
  function renderBalance(m) {
    var def = STAGE_BY_ID[7];
    var st = m.stage[7];
    var box = $('balance-chart');
    var left = dailyPoints(m, 7, 'left');
    var right = dailyPoints(m, 7, 'right');
    var series = [
      { name: '왼쪽', color: COLOR.coral, points: left },
      { name: '오른쪽', color: COLOR.tealLine, points: right }
    ];
    renderLegend($('balance-legend'), series);

    if (!st || (left.length === 0 && right.length === 0)) {
      $('balance-left').textContent = '-';
      $('balance-right').textContent = '-';
      $('balance-date').textContent = '아직 기록이 없어요';
      showChartEmpty(box, '아직 기록이 없어요');
      $('balance-caption').textContent = '';
      $('balance-note').textContent = '';
      return;
    }

    // 최근 값 = 가장 최근 판의 왼쪽·오른쪽
    var lastPlay = null;
    for (var i = st.plays.length - 1; i >= 0; i--) {
      if (st.plays[i].rows.some(function (r) { return r.hasEffort; })) { lastPlay = st.plays[i]; break; }
    }
    var L = null, R = null;
    if (lastPlay) {
      lastPlay.rows.forEach(function (r) {
        if (!r.hasEffort) return;
        if (r.side === 'left') L = r;
        if (r.side === 'right') R = r;
      });
    }
    $('balance-left').textContent = L ? fmtSig(L.maxEffort) : '-';
    $('balance-right').textContent = R ? fmtSig(R.maxEffort) : '-';
    $('balance-date').textContent = lastPlay
      ? fmtDay(lastPlay.day, true, true) + ' ' + fmtTime(lastPlay.playedAt) + ' 기록이에요'
      : '';

    // 기간: 오늘까지 30일. 그 안에 기록이 없으면 마지막 기록일까지 30일.
    var end = m.today;
    var start = addDays(end, -29);
    var all = left.concat(right);
    var inWindow = all.some(function (p) { return p.day >= start && p.day <= end; });
    if (!inWindow) {
      end = all.reduce(function (acc, p) { return p.day > acc ? p.day : acc; }, all[0].day);
      start = addDays(end, -29);
    }

    var sc = drawLineChart({
      container: box,
      series: series,
      startDay: start,
      endDay: end,
      ariaLabel: def.name + ' 왼쪽·오른쪽 ' + def.effortLabel + ' 그래프',
      height: 300
    });
    $('balance-caption').textContent = fmtDay(start, true) + ' ~ ' + fmtDay(end, true) + ' · 날마다 가장 높은 값';
    $('balance-note').textContent = (sc && !sc.fromZero) ? '변화가 잘 보이도록 0부터 시작하지 않아요' : '';
  }

  // ============================================================
  //  활동 캘린더 카드
  // ============================================================
  var calState = { ym: null, selected: null };

  function ymOf(day) { return day.slice(0, 7); }

  function latestDayIn(m, ym) {
    var found = null;
    m.days.forEach(function (d) { if (ymOf(d) === ym) found = d; });
    return found;
  }

  // 볼 수 있는 달의 범위: 첫 기록 달 ~ 오늘 달 (기록이 오늘보다 뒤면 그 달까지)
  function calRange(m) {
    var minYm = m.firstDay ? ymOf(m.firstDay) : ymOf(m.today);
    var maxYm = ymOf(m.today);
    if (m.lastDay && ymOf(m.lastDay) > maxYm) maxYm = ymOf(m.lastDay);
    if (minYm > maxYm) minYm = maxYm;
    return { min: minYm, max: maxYm };
  }

  // 'yyyy-MM' 을 delta 달만큼 옮긴다 (연도 넘김 포함)
  function shiftYm(ym, delta) {
    var dt = new Date(Date.UTC(+ym.slice(0, 4), +ym.slice(5, 7) - 1 + delta, 1));
    return dt.getUTCFullYear() + '-' + pad2(dt.getUTCMonth() + 1);
  }

  // 이전·다음 달 버튼 — 처음 한 번만 묶는다
  function bindCalendarNav(m) {
    function go(delta) {
      if (!calState.ym) return;
      var r = calRange(m);
      var ym = shiftYm(calState.ym, delta);
      if (ym < r.min || ym > r.max) return;
      calState.ym = ym;
      calState.selected = latestDayIn(m, ym);
      renderCalendar(m);
      // 끝에 닿아 버튼이 꺼졌으면 포커스를 달 제목으로 옮긴다
      var btn = $(delta < 0 ? 'cal-prev' : 'cal-next');
      if (btn.disabled) $('cal-month').focus();
    }
    $('cal-prev').addEventListener('click', function () { go(-1); });
    $('cal-next').addEventListener('click', function () { go(1); });
  }

  function renderCalendar(m) {
    if (!calState.ym) {
      calState.ym = ymOf(m.today);
      calState.selected = latestDayIn(m, calState.ym);
    }

    var y = +calState.ym.slice(0, 4);
    var mo = +calState.ym.slice(5, 7);
    $('cal-month').textContent = y + '년 ' + mo + '월';

    var range = calRange(m);
    $('cal-prev').disabled = calState.ym <= range.min;
    $('cal-next').disabled = calState.ym >= range.max;

    var grid = $('cal-grid');
    clear(grid);
    DOW.forEach(function (d) { grid.appendChild(el('div', { 'class': 'cal-dow' }, d)); });

    var firstDay = y + '-' + pad2(mo) + '-01';
    var lead = weekdayOf(firstDay);
    var daysInMonth = new Date(Date.UTC(y, mo, 0)).getUTCDate();

    for (var i = 0; i < lead; i++) grid.appendChild(el('div', { 'class': 'cal-cell is-blank' }));

    for (var d = 1; d <= daysInMonth; d++) {
      var day = y + '-' + pad2(mo) + '-' + pad2(d);
      var plays = m.playsByDay[day] || [];
      var count = plays.length;
      var isToday = day === m.today;

      if (count > 0) {
        var level = Math.min(count, 4);
        var btn = el('button', {
          type: 'button',
          'class': 'cal-cell cal-day level-' + level + (isToday ? ' is-today' : ''),
          'data-day': day,
          'aria-pressed': String(day === calState.selected),
          'aria-label': fmtDay(day, false, true) + ' ' + count + '판' + (isToday ? ', 오늘' : '')
        }, [
          el('span', { 'class': 'cal-num' }, String(d)),
          el('span', { 'class': 'cal-count' }, count + '판')
        ]);
        btn.addEventListener('click', makeDayClick(m, day));
        grid.appendChild(btn);
      } else {
        grid.appendChild(el('div', {
          'class': 'cal-cell' + (isToday ? ' is-today' : ''),
          'aria-label': isToday ? fmtDay(day, false, true) + ', 오늘' : null
        }, el('span', { 'class': 'cal-num' }, String(d))));
      }
    }

    renderCalendarDetail(m);
  }

  function makeDayClick(m, day) {
    return function () {
      calState.selected = day;
      renderCalendar(m);
      // 그리드를 다시 그렸으므로 방금 누른 날짜에 포커스를 되돌린다
      var b = $('cal-grid').querySelector('[data-day="' + day + '"]');
      if (b) b.focus();
    };
  }

  function renderCalendarDetail(m) {
    var detail = $('cal-detail');
    clear(detail);

    var day = calState.selected;
    var plays = day ? (m.playsByDay[day] || []) : [];

    if (!day || plays.length === 0) {
      var hasAnyThisMonth = m.days.some(function (d) { return ymOf(d) === calState.ym; });
      detail.appendChild(el('p', { 'class': 'cal-detail-hint' },
        hasAnyThisMonth ? '날짜를 누르면 그날 한 게임이 여기에 나와요' : '이 달에는 기록이 없어요'));
      return;
    }

    detail.appendChild(el('h4', { 'class': 'cal-detail-title' },
      fmtDay(day, false, true) + ' · ' + plays.length + '판 했어요'));

    var list = el('ul', { 'class': 'cal-list' });
    plays.forEach(function (p) {
      var def = STAGE_BY_ID[p.stage];
      var lines = describePlay(def, p);
      var item = el('li', { 'class': 'cal-item' }, [
        el('div', { 'class': 'cal-item-head' }, [
          el('span', { 'class': 'cal-item-name' }, def.name),
          el('span', { 'class': 'cal-item-time' }, fmtTime(p.playedAt))
        ])
      ]);
      lines.forEach(function (line) { item.appendChild(el('div', { 'class': 'cal-item-line' }, line)); });
      list.appendChild(item);
    });
    detail.appendChild(list);
  }

  // 한 판을 쉬운 말 한두 줄로
  function describePlay(def, play) {
    var rows = play.rows;
    var lines = [];

    if (def.sides) {
      var byKey = {};
      rows.forEach(function (r) { byKey[r.side] = r; });

      if (def.id === 7) {
        // 젤리 분류: 옮긴 젤리(좌·우 각각) + 뻗은 크기(좌·우)
        var reps = def.sides.map(function (s) {
          var r = byKey[s.key];
          return r ? s.label + ' ' + r.reps + def.unit : null;
        }).filter(Boolean);
        if (reps.length) lines.push(def.repsLabel + ' ' + reps.join(' · '));
      } else {
        var any = rows[0];
        if (any) lines.push(def.repsLabel + ' ' + any.reps + def.unit);
      }

      // 7번: "뻗은 크기 왼쪽 0.163 · 오른쪽 0.171" / 4번: "볼 크기 0.0159 · 입술 크기 0.312"
      var effs = def.sides.map(function (s) {
        var r = byKey[s.key];
        if (!r || !r.hasEffort) return null;
        return (def.id === 7 ? s.label : s.effortLabel) + ' ' + fmtSig(r.maxEffort);
      }).filter(Boolean);
      if (effs.length) lines.push((def.id === 7 ? '뻗은 크기 ' : '') + effs.join(' · '));
    } else {
      var r0 = rows[0];
      if (r0) {
        lines.push(def.repsLabel + ' ' + r0.reps + def.unit);
        if (r0.hasEffort) lines.push(def.effortLabel + ' ' + fmtSig(r0.maxEffort));
      }
    }
    return lines;
  }

  // ============================================================
  //  게임별 판 수 카드 (가로 막대)
  // ============================================================
  function renderBars(m) {
    var bars = $('bars');
    clear(bars);

    var counts = STAGES.map(function (s) { return m.stage[s.id] ? m.stage[s.id].plays.length : 0; });
    var max = Math.max.apply(null, counts.concat([1]));

    STAGES.forEach(function (s, i) {
      var n = counts[i];
      var row = el('div', { 'class': 'bar-row' + (n === 0 ? ' is-empty' : '') }, [
        el('span', { 'class': 'bar-name' }, s.name),
        el('div', { 'class': 'bar-track', role: 'img', 'aria-label': s.name + ' ' + n + '판' }),
        el('span', { 'class': 'bar-count' }, n + '판')
      ]);
      if (n > 0) {
        var fill = el('div', { 'class': 'bar-fill' });
        fill.style.width = Math.round((n / max) * 100) + '%';
        row.querySelector('.bar-track').appendChild(fill);
      }
      bars.appendChild(row);
    });
  }

  // ============================================================
  //  성장 카드 식물 그림 (인라인 SVG, 1~6단계)
  //  - 화분·흙은 모든 단계에서 같은 자리, 식물만 자란다
  //  - 통통한 캐주얼 게임 화풍. 화분 얼굴(눈·입·볼)은 plant-pot 안의 5개 요소
  //  - id·defs·그라디언트 없음(같은 페이지에 여러 개 있어도 충돌 없음)
  // ============================================================
  function plantSvg(stage) {
    var st = Number(stage);
    if (!isFinite(st)) st = 1;
    st = Math.round(st);
    if (st < 1) st = 1;
    if (st > 6) st = 6;

    // 팔레트 (+ 밝은/어두운 변형)
    var LEAF = '#7FB069', LEAF_D = '#5E8C4F', LEAF_L = '#9DC98B', LEAF_M = '#6FA05C';
    var FRUIT = '#E86A5E', FRUIT_D = '#C9574D';
    var SOIL = '#A8836B', SOIL_D = '#85644F', SOIL_L = '#C4A48C';
    var POT = '#E8A98F', POT_D = '#CF8C71';
    var PINK = '#F2C4CE', INK = '#4A3B33', CREAM_D = '#F0D9C8';
    var TRUNK = SOIL_D, TRUNK_L = SOIL;

    function hl(cx, cy, rx, ry, rot, op) {
      return '<ellipse cx="' + cx + '" cy="' + cy + '" rx="' + rx + '" ry="' + ry + '"' +
        (rot ? ' transform="rotate(' + rot + ' ' + cx + ' ' + cy + ')"' : '') +
        ' fill="#FFFFFF" fill-opacity="' + (op || 0.35) + '"/>';
    }
    function circ(cx, cy, r, fill) {
      return '<circle cx="' + cx + '" cy="' + cy + '" r="' + r + '" fill="' + fill + '"/>';
    }
    function stroke(d, color, w) {
      return '<path d="' + d + '" fill="none" stroke="' + color + '" stroke-width="' + w +
        '" stroke-linecap="round" stroke-linejoin="round"/>';
    }
    // 둥근 덩어리 잎: 밑동이 (0,0), 끝이 (0,-50). asp<1이면 날씬하게
    function leaf(x, y, ang, sc, asp, fill) {
      var sx = +(sc * (asp || 1)).toFixed(3);
      return '<g transform="translate(' + x + ' ' + y + ') rotate(' + ang + ') scale(' + sx + ' ' + sc + ')">' +
        '<path d="M0 0 C-22 -6 -30 -30 -14 -44 C-6 -52 6 -52 14 -44 C30 -30 22 -6 0 0 Z" fill="' + (fill || LEAF) + '"/>' +
        '<path d="M0 -5 C-2 -16 -2 -28 0 -40" fill="none" stroke="' + LEAF_D + '" stroke-width="2.5" stroke-linecap="round" stroke-opacity="0.55"/>' +
        hl(-8, -31, 4, 6, 20) +
        '</g>';
    }
    // 수관 덩어리(어두운 밑층 + 밝은 윗층)
    function blob(cx, cy, r, dark) {
      return circ(cx, cy + (dark ? 10 : 0), r, dark ? LEAF_D : LEAF);
    }
    function fruit(cx, cy) {
      return circ(cx, cy + 1.5, 11, FRUIT_D) + circ(cx, cy - 1, 10.5, FRUIT) + hl(cx - 4, cy - 5, 3.5, 2.5, -30, 0.55);
    }

    // ── 화분 · 흙 (모든 단계 동일) ─────────────────────────────
    var pot = '<g class="plant-pot">' +
      '<ellipse cx="160" cy="303" rx="76" ry="9" fill="' + INK + '" fill-opacity="0.12"/>' +
      '<path d="M96 230 C96 268 106 300 128 300 L192 300 C214 300 224 268 224 230 Z" fill="' + POT + '"/>' +
      '<path d="M224 230 C224 268 214 300 192 300 L176 300 C198 296 206 268 206 230 Z" fill="' + POT_D + '"/>' +
      '<rect x="106" y="244" width="12" height="30" rx="6" fill="#FFFFFF" fill-opacity="0.35"/>' +
      '<ellipse cx="128" cy="267" rx="8" ry="4.5" fill="' + PINK + '" fill-opacity="0.85"/>' +
      '<ellipse cx="192" cy="267" rx="8" ry="4.5" fill="' + PINK + '" fill-opacity="0.85"/>' +
      circ(146, 258, 4.5, INK) + circ(174, 258, 4.5, INK) +
      hl(147.5, 256.5, 1.5, 1.5, 0, 0.8) + hl(175.5, 256.5, 1.5, 1.5, 0, 0.8) +
      stroke('M152 268 Q160 276 168 268', INK, 3) +
      '<rect x="84" y="206" width="152" height="32" rx="16" fill="' + POT_D + '"/>' +
      '<rect x="85" y="206" width="150" height="26" rx="13" fill="' + POT + '"/>' +
      '<rect x="96" y="222" width="34" height="8" rx="4" fill="#FFFFFF" fill-opacity="0.35"/>' +
      '<ellipse cx="160" cy="208" rx="70" ry="12" fill="' + POT_D + '"/>' +
      '<ellipse cx="160" cy="209" rx="62" ry="9" fill="' + SOIL + '"/>' +
      circ(126, 210, 3, SOIL_D) + circ(182, 213, 2.5, SOIL_D) + circ(200, 207, 2.2, SOIL_D) +
      circ(138, 204, 2, SOIL_L) + circ(190, 204, 1.8, SOIL_L) +
      '</g>';

    // ── 식물 (단계별) ────────────────────────────────────────
    var plant = '';
    var label = '';

    if (st === 1) {
      label = '1단계: 흙에 심은 씨앗';
      plant =
        '<ellipse cx="160" cy="207" rx="22" ry="7.5" fill="' + SOIL_D + '"/>' +
        '<ellipse cx="160" cy="202" rx="16" ry="11" transform="rotate(-18 160 202)" fill="' + CREAM_D + '" stroke="' + SOIL_D + '" stroke-width="1.5" stroke-opacity="0.45"/>' +
        '<path d="M147 204 Q160 209 173 199" fill="none" stroke="' + INK + '" stroke-width="1.8" stroke-linecap="round" stroke-opacity="0.3"/>' +
        hl(153, 197, 4.5, 2.6, -18, 0.65) +
        '<path d="M138 207 A22 7.5 0 0 0 182 207 Z" fill="' + SOIL + '"/>' +
        circ(148, 214, 1.8, SOIL_L) + circ(173, 213, 1.5, SOIL_L);
    } else if (st === 2) {
      label = '2단계: 떡잎이 난 새싹';
      plant =
        stroke('M160 212 C161 200 158 186 160 172', LEAF_D, 7) +
        '<ellipse cx="140" cy="169" rx="22" ry="14" transform="rotate(30 140 169)" fill="' + LEAF + '"/>' +
        '<ellipse cx="180" cy="169" rx="22" ry="14" transform="rotate(-30 180 169)" fill="' + LEAF + '"/>' +
        hl(130, 162, 6.5, 3.5, 30) + hl(190, 162, 6.5, 3.5, -30) +
        circ(160, 172, 6, LEAF_L) + hl(158, 170, 2, 2, 0, 0.5);
    } else if (st === 3) {
      label = '3단계: 잎이 여럿 난 풀';
      // 흙에서 부채꼴로 퍼지는 잎 7장 (뒷줄 3장 중간톤, 앞줄 4장 밝은톤)
      plant =
        leaf(160, 207, -62, 1.4, 0.62, LEAF_M) +
        leaf(160, 207, 62, 1.4, 0.62, LEAF_M) +
        leaf(160, 207, 0, 2.1, 0.62, LEAF_M) +
        leaf(160, 207, -76, 1.1, 0.62) +
        leaf(160, 207, 76, 1.1, 0.62) +
        leaf(160, 207, -34, 1.65, 0.62) +
        leaf(160, 207, 34, 1.65, 0.62);
    } else if (st === 4) {
      label = '4단계: 작은 나무';
      plant =
        '<path d="M150 213 C155 206 154 170 155 128 L165 128 C166 170 165 206 170 213 Z" fill="' + TRUNK + '"/>' +
        stroke('M157.5 200 L157.5 150', TRUNK_L, 3) +
        blob(160, 100, 44, true) + blob(124, 122, 30, true) + blob(196, 122, 30, true) +
        blob(124, 122, 30) + blob(196, 122, 30) + blob(160, 100, 44) +
        hl(142, 78, 12, 7, -35) + hl(126, 96, 4, 4) + hl(184, 106, 5, 3, -35);
    } else {
      label = st === 5 ? '5단계: 큰 나무' : '6단계: 열매가 달린 나무';
      plant =
        '<path d="M142 213 C152 204 149 160 150 100 L170 100 C171 160 168 204 178 213 Z" fill="' + TRUNK + '"/>' +
        stroke('M157 178 C146 170 134 160 118 146', TRUNK, 9) +
        stroke('M163 184 C176 176 190 166 206 150', TRUNK, 9) +
        stroke('M155 200 L154.5 150', TRUNK_L, 4) +
        blob(160, 96, 56, true) + blob(116, 84, 34, true) + blob(204, 84, 34, true) +
        blob(98, 124, 38, true) + blob(222, 124, 38, true) +
        blob(98, 124, 38) + blob(222, 124, 38) +
        blob(116, 84, 34) + blob(204, 84, 34) + blob(160, 96, 56) +
        hl(138, 66, 15, 9, -35) + hl(104, 72, 6, 4, -35) + hl(84, 112, 6, 4, -35) +
        hl(190, 70, 5, 3, -35) + hl(122, 52, 3.5, 3.5) + hl(208, 112, 4, 4);
      if (st === 6) {
        plant +=
          fruit(122, 92) + fruit(178, 60) + fruit(210, 102) +
          fruit(148, 126) + fruit(92, 136) + fruit(230, 138) + fruit(176, 142);
      }
    }

    return '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 320 320" role="img" aria-label="' + label + '">' +
      pot +
      '<g class="plant-sway">' + plant + '</g>' +
      '</svg>';
  }

  // ============================================================
  //  시작
  // ============================================================
  function safe(name, fn) {
    try { fn(); }
    catch (e) {
      if (window.console && console.error) console.error('[대시보드] ' + name + ' 그리기 실패:', e);
    }
  }

  function init() {
    if (window.DASHBOARD_USING_SAMPLE) {
      var banner = $('sample-banner');
      if (banner) banner.hidden = false;
    }
    if (window.DASHBOARD_DATA_BROKEN) {
      var broken = $('broken-banner');
      if (broken) broken.hidden = false;
    }

    var model = buildModel(window.DASHBOARD_DATA);

    safe('헤더', function () { renderHeader(model); });
    safe('요약', function () { renderSummary(model); });
    safe('성장', function () { renderGrowth(model); });
    safe('추이', function () { renderTrendControls(model); renderTrend(model); });
    safe('좌우 균형', function () { renderBalance(model); });
    safe('캘린더 이동', function () { bindCalendarNav(model); });
    safe('캘린더', function () { renderCalendar(model); });
    safe('판 수', function () { renderBars(model); });

    window.addEventListener('resize', debounce(function () {
      safe('추이', function () { renderTrend(model); });
      safe('좌우 균형', function () { renderBalance(model); });
    }, 150));
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
  else init();
})();
