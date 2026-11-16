// The playground: a little desktop with live cards that behave like the app's.
// Drag them, line them up, drop one on another to join, right-click for the card menu,
// and open the tray menu from the taskbar. Pitzki lives on it too.
(() => {
  const desk = document.getElementById('desk');
  if (!desk) return;
  const sizer = document.getElementById('desk-sizer');
  const ART = document.getElementById('mascot-art').innerHTML;
  const W = 1200, H = 700, BAR = 40, GAP = 10, SNAP = 12;
  const $ = (html) => { const t = document.createElement('template'); t.innerHTML = html.trim(); return t.content.firstChild; };
  const mascot = (cls = '') => `<svg class="mascot ${cls}" viewBox="0 -6 120 102">${ART}</svg>`;
  const store = {
    get(k, d) { try { const v = localStorage.getItem('pw.' + k); return v == null ? d : JSON.parse(v); } catch { return d; } },
    set(k, v) { try { localStorage.setItem('pw.' + k, JSON.stringify(v)); } catch {} },
  };

  // ----- fitting the 1200x700 desk into the page -----
  let zoom = 1;
  function fit() {
    zoom = Math.min(1, sizer.clientWidth / W);
    desk.style.transform = `scale(${zoom})`;
    sizer.style.height = (H * zoom) + 'px';
  }
  addEventListener('resize', fit);

  // ----- settings, as in the tray menu -----
  const set = { lineUp: true, join: true, lockAll: false, glass: true, profile: 'all', autoGame: true, ...store.get('set', {}) };
  const save = () => store.set('set', set);

  // ----- text that rolls when it changes -----
  function roll(el, text, down = false) {
    text = String(text);
    if (el.dataset.v === text) return;
    const first = el.dataset.v == null;
    el.dataset.v = text;
    if (first) { el.textContent = text; return; }
    const span = document.createElement('span');
    span.className = 'roll' + (down ? ' down' : '');
    span.textContent = text;
    el.replaceChildren(span);
  }

  // ----- Pitzki helpers -----
  function act(svg, cls, ms) {
    if (!svg) return;
    svg.classList.remove(cls); void svg.getBoundingClientRect(); svg.classList.add(cls);
    if (ms) setTimeout(() => svg.classList.remove(cls), ms);
  }
  function say(text, near) {
    const box = (near || desk).getBoundingClientRect(), d = desk.getBoundingClientRect();
    const b = $(`<div class="bubble">${text}</div>`);
    b.style.left = ((box.left + box.width / 2 - d.left) / zoom) + 'px';
    b.style.top = ((box.top - d.top) / zoom - 24) + 'px';
    desk.appendChild(b);
    setTimeout(() => b.remove(), 2300);
  }
  const pitzkiSvgs = () => [...desk.querySelectorAll('.wcard:not(.gone) .c-pitzki svg.mascot, .wcard:not(.gone) .c-weather svg.mascot, .wcard:not(.gone) .c-focus svg.mascot')];
  function react(text) {
    const main = desk.querySelector('.wcard:not(.gone) .c-pitzki svg.mascot');
    if (!main) return;
    act(main, 'jump', 600);
    say(text, main);
  }

  // the app draws its icons with Windows' own icon font; use it too when this computer has it
  (() => {
    try {
      const c = document.createElement('canvas').getContext('2d'), g = String.fromCharCode(0xE72E);
      c.font = '20px monospace'; const plain = c.measureText(g).width;
      c.font = '20px "Segoe Fluent Icons", "Segoe MDL2 Assets", monospace';
      if (c.measureText(g).width !== plain) document.documentElement.classList.add('fluent');
    } catch {}
  })();

  // ----- the cards -----
  const KINDS = {
    clock: { name: 'Clock', ico: '<i class="fi-g">&#xE121;</i><i class="fi-s">🕒</i>', grp: 'Everyday', tip: "The time and today's date" },
    pitzki: { name: 'Pitzki', ico: '<i class="fi-g">&#xE76E;</i><i class="fi-s">☺</i>', grp: 'Everyday', tip: 'He sits on the desktop and keeps you company' },
    weather: { name: 'Weather', ico: '<i class="fi-g">&#xE706;</i><i class="fi-s">☀</i>', grp: 'Everyday', tip: 'The weather, and Pitzki out in it' },
    tasks: { name: 'Tasks', ico: '<i class="fi-g">&#xE762;</i><i class="fi-s">✓</i>', grp: 'Everyday', tip: 'A short list you tick off' },
    focus: { name: 'Focus rounds', ico: '<i class="fi-g">&#xE823;</i><i class="fi-s">◔</i>', grp: 'Everyday', tip: '25 minutes of work, 5 of rest' },
    stocks: { name: 'Stocks', ico: '<i class="fi-g">&#xE9D2;</i><i class="fi-s">📈</i>', grp: 'Money', tip: 'Prices that move, with a line of the day' },
    pc: { name: 'This PC', ico: '<i class="fi-g">&#xE7F4;</i><i class="fi-s">▣</i>', grp: 'Your PC', tip: 'Processor, memory, network and a graph' },
    music: { name: 'Music', ico: '<i class="fi-g">&#xE8D6;</i><i class="fi-s">♫</i>', grp: 'Music and games', tip: "Whatever is playing, and Pitzki dances" },
  };

  const BUILD = {
    clock(card) {
      const el = $(`<div class="inner c-clock"><div class="title"><span class="part"></span></div><div class="bign"><span class="hm"></span></div><div class="sub2 date"></div></div>`);
      const tick = () => {
        const n = new Date(), h = n.getHours();
        roll(el.querySelector('.part'), h < 5 ? 'Night' : h < 12 ? 'Morning' : h < 17 ? 'Afternoon' : h < 21 ? 'Evening' : 'Night');
        roll(el.querySelector('.hm'), n.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', hour12: false }));
        roll(el.querySelector('.date'), n.toLocaleDateString('en-GB', { weekday: 'long', day: 'numeric', month: 'long' }));
      };
      tick(); card.timers.push(setInterval(tick, 1000));
      return el;
    },

    pitzki(card) {
      const el = $(`<div class="inner c-pitzki"><div class="stage">${mascot()}</div><div class="mood">click me</div></div>`);
      const svg = el.querySelector('svg'), stage = el.querySelector('.stage'), mood = el.querySelector('.mood');
      const lines = ['hi!', 'nice day', 'hehe', '♪', 'need a break?', 'look, stocks!', 'wheee'];
      svg.addEventListener('click', (e) => { e.stopPropagation(); if (card.moved) return; act(svg, 'wave', 1500); act(svg, 'jump', 600); say(lines[Math.floor(Math.random() * lines.length)], svg); });
      svg.addEventListener('dblclick', (e) => { e.stopPropagation(); explore(card); });
      // about every 25 seconds he does one of his scenes, from the real app
      const scenes = (window.SCENES || []).filter(s => !['hello', 'boot', 'birthday', 'alien'].includes(s.k));
      let busy = false;
      const scene = () => {
        if (busy || !scenes.length || document.hidden || card.el.classList.contains('gone') || desk.classList.contains('playing')) return;
        busy = true;
        const s = scenes[Math.floor(Math.random() * scenes.length)];
        const img = window.Clips ? Clips.make(`scenes/${s.k}`, s.n) : $(`<img src="scenes/${s.k}.gif" alt="${s.n}">`);
        svg.style.visibility = 'hidden';
        stage.appendChild(img);
        roll(mood, s.n);
        setTimeout(() => { img.remove(); svg.style.visibility = ''; roll(mood, 'click me', true); busy = false; }, 6500);
      };
      card.timers.push(setInterval(scene, 25000));
      setTimeout(scene, 5000);
      card.mood = mood;
      return el;
    },

    weather(card) {
      const el = $(`<div class="inner c-weather"><div class="title">Weather</div>
        <div class="sky2"><div class="sun"></div><div class="drops"></div><div class="flakes"></div>
          <svg class="umbrella" viewBox="0 0 70 40"><path d="M3 30a32 26 0 0 1 64 0q-8-6-16 0q-8-6-16 0q-8-6-16 0q-8-6-16 0z" fill="#ef6f8f"/><path d="M35 4v34" stroke="#ddd" stroke-width="2"/></svg>
          ${mascot()}</div>
        <div class="foot"><span class="temp"></span><span class="sub2 cond"></span></div></div>`);
      const svg = el.querySelector('svg.mascot'), sun = el.querySelector('.sun'), drops = el.querySelector('.drops'), flakes = el.querySelector('.flakes'), umb = el.querySelector('.umbrella');
      for (let i = 0; i < 14; i++) drops.appendChild($(`<i style="left:${6 + i * 7}%;animation-delay:${(i * 0.37) % 1}s"></i>`));
      for (let i = 0; i < 10; i++) flakes.appendChild($(`<i style="left:${4 + i * 10}%;animation-delay:${(i * 0.61) % 3}s"></i>`));
      const skies = [['sun', 26, 'Sunny'], ['rain', 14, 'Rain'], ['snow', -1, 'Snow'], ['cloud', 18, 'Cloudy']];
      let i = 0;
      const show = () => {
        const [k, t, c] = skies[i % skies.length];
        sun.style.opacity = k === 'sun' ? 1 : 0;
        drops.style.display = k === 'rain' ? '' : 'none';
        flakes.style.display = k === 'snow' ? '' : 'none';
        umb.style.opacity = k === 'rain' ? 1 : 0; umb.style.transform = k === 'rain' ? 'none' : 'translateY(-14px)';
        svg.dataset.hat = k === 'snow' ? 'beanie' : '';
        if (k === 'sun') act(svg, 'wave', 1500);
        roll(el.querySelector('.temp'), t + '°', t < (el.querySelector('.temp').dataset.v ? parseInt(el.querySelector('.temp').dataset.v) : 99));
        roll(el.querySelector('.cond'), c);
      };
      show();
      el.addEventListener('click', () => { if (!card.moved) { i++; show(); } });
      card.timers.push(setInterval(() => { i++; show(); }, 9000));
      return el;
    },

    tasks(card) {
      const el = $(`<div class="inner c-tasks"><div class="title"><span class="n"></span></div><div class="list"></div><input placeholder="Add a task, press Enter" maxlength="40"><span class="clear">Clear done</span></div>`);
      let items = store.get('tasks', [[true, 'Send the invoice'], [false, 'Call the dentist'], [false, 'Water the plants']]);
      const list = el.querySelector('.list');
      const draw = (fresh) => {
        list.replaceChildren(...items.map(([d, t], k) => {
          const row = $(`<div class="t${d ? ' done' : ''}"><i></i><span></span></div>`);
          row.querySelector('span').textContent = t;
          if (k !== fresh) row.style.animation = 'none';
          row.addEventListener('click', () => {
            if (card.moved) return;
            items[k][0] = !items[k][0]; store.set('tasks', items); draw();
            if (items[k][0]) react(['nice!', 'done ✓', 'go you!'][Math.floor(Math.random() * 3)]);
          });
          return row;
        }));
        roll(el.querySelector('.n'), `Tasks · ${items.filter(x => !x[0]).length} left`);
      };
      const box = el.querySelector('input');
      box.addEventListener('pointerdown', e => e.stopPropagation());
      box.addEventListener('keydown', e => {
        e.stopPropagation();
        if (e.key !== 'Enter' || !box.value.trim() || items.length >= 7) return;
        items.push([false, box.value.trim()]); box.value = ''; store.set('tasks', items); draw(items.length - 1);
      });
      el.querySelector('.clear').addEventListener('click', () => { items = items.filter(x => !x[0]); store.set('tasks', items); draw(); });
      draw();
      return el;
    },

    focus(card) {
      const el = $(`<div class="inner c-focus"><div class="title">Focus</div><div class="row2"><span class="bign">25:00</span>${mascot()}</div>
        <div class="sub2 what">25 min focus, then a break</div><div class="bar"><b></b></div><div class="btns"><button class="main go">Start</button><button class="skip">Skip</button></div></div>`);
      const big = el.querySelector(".bign"), bar = el.querySelector('.bar b'), go = el.querySelector('.go'), svg = el.querySelector('svg');
      let left = 25 * 60, total = left, running = false, rest = false;
      const show = () => { roll(big, `${String(Math.floor(left / 60)).padStart(2, '0')}:${String(left % 60).padStart(2, '0')}`, true); bar.style.width = (100 * (1 - left / total)) + '%'; };
      const next = () => {
        rest = !rest; total = left = (rest ? 5 : 25) * 60;
        roll(el.querySelector('.what'), rest ? '5 min break, stretch!' : '25 min focus, then a break');
        act(svg, 'jump', 600); show();
      };
      go.addEventListener('click', () => { running = !running; go.textContent = running ? 'Stop' : 'Start'; go.classList.toggle('main', !running); if (running) act(svg, 'wave', 1500); });
      el.querySelector('.skip').addEventListener('click', next);
      card.timers.push(setInterval(() => { if (!running) return; left = Math.max(0, left - 1); if (left === 0) { next(); react(rest ? 'break time!' : 'back to it!'); } show(); }, 1000));
      show();
      return el;
    },

    stocks(card) {
      const el = $(`<div class="inner c-stocks"><div class="title"><span class="grow">Stocks</span><span class="sub2">live demo</span></div><div class="rows"></div></div>`);
      const list = [['ACME', 'Acme Corp', 182.4], ['NOVA', 'Nova Labs', 96.1], ['BYTE', 'Byte Inc.', 341.7], ['SKY', 'Sky Air', 54.3]].map(([s, n, p]) => {
        const day = Array.from({ length: 30 }, (_, i) => p * (1 + Math.sin(i / 4 + p) * 0.006 + (Math.random() - .5) * 0.006));
        const row = $(`<div class="s"><div><b>${s}</b><small>${n}</small></div><svg viewBox="0 0 100 26" preserveAspectRatio="none"><path fill="none" stroke-width="1.6"/></svg><div class="p"><span class="v"></span><span class="m"></span></div></div>`);
        el.querySelector('.rows').appendChild(row);
        return { s, open: day[0], day, row };
      });
      const draw = (st, prev) => {
        const now = st.day[st.day.length - 1], pct = (now / st.open - 1) * 100, up = pct >= 0;
        const lo = Math.min(...st.day), hi = Math.max(...st.day);
        st.row.querySelector('path').setAttribute('d', st.day.map((v, i) => `${i ? 'L' : 'M'}${(i / (st.day.length - 1) * 100).toFixed(1)} ${(24 - (v - lo) / (hi - lo || 1) * 22).toFixed(1)}`).join(''));
        st.row.querySelector('path').setAttribute('stroke', up ? '#5ad18a' : '#f07c8e');
        roll(st.row.querySelector('.v'), '$' + now.toFixed(2), prev != null && now < prev);
        const m = st.row.querySelector('.m'); m.className = 'm ' + (up ? 'up' : 'dn');
        roll(m, `${up ? '▲' : '▼'} ${Math.abs(pct).toFixed(2)}%`, !up);
      };
      list.forEach(st => draw(st));
      card.timers.push(setInterval(() => {
        const st = list[Math.floor(Math.random() * list.length)];
        const prev = st.day[st.day.length - 1];
        const big = Math.random() < 0.08;
        const next = prev * (1 + (Math.random() - .48) * (big ? 0.05 : 0.008));
        st.day.push(next); st.day.shift(); draw(st, prev);
        if (big) react(next > prev ? `${st.s} jumped!` : `ouch, ${st.s}`);
      }, 1600));
      return el;
    },

    pc(card) {
      const el = $(`<div class="inner c-pc"><div class="title">This PC</div>
        ${['CPU', 'Graphics', 'Memory'].map(n => `<div class="r" data-n="${n}">${n.toUpperCase()}<div><b></b></div><span></span></div>`).join('')}
        <div class="r">NETWORK<i></i><span class="net"></span></div>
        <svg viewBox="0 0 100 40" preserveAspectRatio="none"><path class="a" fill="none" stroke="#afa9ec" stroke-width="1.5"/><path class="b" fill="none" stroke="#5ad18a" stroke-width="1.2"/></svg></div>`);
      const cpu = [], net = [];
      const tick = () => {
        const c = 8 + Math.random() * 40 + (Math.random() < .1 ? 40 : 0), g = 5 + Math.random() * 30, m = 52 + Math.random() * 6, n = Math.random() * 900;
        cpu.push(c); net.push(n / 10); if (cpu.length > 30) { cpu.shift(); net.shift(); }
        const set = (name, pct, text) => { const r = el.querySelector(`[data-n="${name}"]`); r.querySelector('b').style.width = pct + '%'; roll(r.querySelector('span'), text); };
        set('CPU', c, Math.round(c) + '%'); set('Graphics', g, Math.round(g) + '%'); set('Memory', m, (m * 0.16).toFixed(1) + ' / 16 GB');
        roll(el.querySelector('.net'), `↓ ${Math.round(n)} KB/s`);
        const path = (arr, max) => arr.map((v, i) => `${i ? 'L' : 'M'}${(i / 29 * 100).toFixed(1)} ${(38 - Math.min(v, max) / max * 34).toFixed(1)}`).join('');
        el.querySelector('.a').setAttribute('d', path(cpu, 100)); el.querySelector('.b').setAttribute('d', path(net, 100));
        if (c > 80) { const p = desk.querySelector('.c-pitzki svg.mascot'); if (p && Math.random() < .5) say('phew, hot!', p); }
      };
      tick(); card.timers.push(setInterval(tick, 1500));
      return el;
    },

    music(card) {
      const songs = [['Midnight Drive', 'The Pixel Cats', 214, 'linear-gradient(135deg,#ff6b9a,#6a4cff)'], ['Neon Rain', 'Lofi Robots', 187, 'linear-gradient(135deg,#3fd0c9,#2a4cff)'], ['Sunday Pancakes', 'Mellow Moon', 201, 'linear-gradient(135deg,#ffc35c,#ff6b6b)']];
      const el = $(`<div class="inner c-music"><div class="title">Now playing</div><div class="top"><div class="cover"></div><div class="song"><b></b><span class="sub2 who"></span></div><button class="play">▶</button></div>
        <div class="track"><div><b></b></div></div><div class="times"><span class="at"></span><span class="end"></span></div><div class="keys"><button class="prev">⏮</button><button class="next">⏭</button></div></div>`);
      let n = 0, at = 61, playing = false;
      const clock = s => `${Math.floor(s / 60)}:${String(Math.floor(s % 60)).padStart(2, '0')}`;
      const show = (fresh) => {
        const [t, a, len, col] = songs[n];
        roll(el.querySelector('.song b'), t); roll(el.querySelector('.who'), a);
        el.querySelector('.cover').style.background = col;
        el.querySelector('.track b').style.width = (at / len * 100) + '%';
        el.querySelector('.at').textContent = clock(at); el.querySelector('.end').textContent = '-' + clock(len - at);
        el.querySelector('.play').textContent = playing ? '⏸' : '▶';
        desk.classList.toggle('music', playing);
        pitzkiSvgs().forEach(s => s.classList.toggle('dance', playing));
        if (fresh && playing) react('♪ ♫');
      };
      el.querySelector('.play').addEventListener('click', () => { playing = !playing; show(true); });
      el.querySelector('.next').addEventListener('click', () => { n = (n + 1) % songs.length; at = 0; show(); });
      el.querySelector('.prev').addEventListener('click', () => { n = (n + songs.length - 1) % songs.length; at = 0; show(); });
      const track = el.querySelector('.track');
      track.addEventListener('pointerdown', e => { e.stopPropagation(); const r = track.getBoundingClientRect(); at = Math.max(0, Math.min(1, (e.clientX - r.left) / r.width)) * songs[n][2]; show(); });
      card.timers.push(setInterval(() => { if (!playing) return; at += 1; if (at >= songs[n][2]) { n = (n + 1) % songs.length; at = 0; } show(); }, 1000));
      card.stop = () => { playing = false; show(); };
      show();
      return el;
    },
  };

  // ----- placing, dragging, lining up, joining -----
  const cards = [];
  const layout = store.get('layout', null);
  const START = [
    ["clock", 20, 20], ["weather", 20, 168], ["pitzki", 292, 20], ["pc", 292, 228],
    ["stocks", 590, 20], ["music", 590, 370], ["tasks", 925, 20], ["focus", 925, 290],
  ];

  function rect(c) { const r = c.el.getBoundingClientRect(); return { x: c.x, y: c.y, w: r.width / zoom, h: r.height / zoom }; }
  function group(c) { return c.group ? cards.filter(o => o.group === c.group && !o.removed) : [c]; }
  function place(c) { c.el.style.left = c.x + 'px'; c.el.style.top = c.y + 'px'; }
  function persist() { store.set('layout', cards.filter(c => !c.removed).map(c => ({ k: c.kind, x: c.x, y: c.y, s: c.scale, l: c.locked, g: c.glass, gl: c.glassLevel, only: c.only, peek: c.peek }))); }

  function make(kind, x, y, opts = {}) {
    const c = { kind, x, y, scale: opts.s || 1, locked: !!opts.l, glass: opts.g ?? true, glassLevel: opts.gl || 'Normal', only: opts.only || 'all', peek: !!opts.peek, timers: [] };
    c.el = $(`<div class="wcard pop"><div class="skin"></div></div>`);
    c.el.querySelector('.skin').appendChild(BUILD[kind](c));
    desk.insertBefore(c.el, desk.querySelector('.taskbar'));
    c.el.addEventListener('animationend', e => { if (e.animationName === 'cardPop') c.el.classList.remove('pop'); });
    cards.push(c);
    style(c); place(c); hookDrag(c);
    return c;
  }

  function style(c) {
    c.el.style.setProperty('--s', c.scale);
    c.el.classList.toggle('glass', set.glass && c.glass);
    c.el.style.setProperty('--glass', { Clearer: .25, Normal: .45, Darker: .7 }[c.glassLevel]);
    const hidden = c.removed || (set.profile !== 'all' && c.only !== 'all' && c.only !== set.profile);
    c.el.classList.toggle('gone', hidden || desk.classList.contains('quit'));
  }
  const restyle = () => cards.forEach(style);

  function remove(c) {
    c.removed = true; style(c); c.timers.forEach(clearInterval); c.stop?.();
    if (c.group) split(c);
    setTimeout(() => c.el.remove(), 400);
    persist(); refreshPicker();
  }

  function snapTo(c, nx, ny, alt) {
    const me = group(c), box = bounds(me.map(o => o === c ? { ...rect(o), x: nx, y: ny } : { ...rect(o), x: o.x + nx - c.x, y: o.y + ny - c.y }));
    let dx = 0, dy = 0;
    if (set.lineUp && !alt) {
      const xs = [[0, box.x], [W - box.w, box.x]], ys = [[0, box.y], [H - BAR - box.h, box.y]];
      for (const o of cards) {
        if (o.removed || me.includes(o) || o.el.classList.contains('gone')) continue;
        const r = rect(o);
        xs.push([r.x + r.w + GAP, box.x], [r.x - box.w - GAP, box.x], [r.x, box.x], [r.x + r.w - box.w, box.x]);
        ys.push([r.y + r.h + GAP, box.y], [r.y - box.h - GAP, box.y], [r.y, box.y], [r.y + r.h - box.h, box.y]);
      }
      const best = (list) => list.reduce((b, [t, v]) => Math.abs(t - v) < SNAP && Math.abs(t - v) < Math.abs(b) ? t - v : b, Infinity);
      const bx = best(xs), by = best(ys);
      if (bx !== Infinity) dx = bx; if (by !== Infinity) dy = by;
    }
    // stay on the desk
    const fx = Math.max(-box.x, Math.min(W - box.w - box.x, dx)), fy = Math.max(-box.y, Math.min(H - BAR - box.h - box.y, dy));
    return [nx + fx, ny + fy];
  }
  function bounds(rs) { const x = Math.min(...rs.map(r => r.x)), y = Math.min(...rs.map(r => r.y)); return { x, y, w: Math.max(...rs.map(r => r.x + r.w)) - x, h: Math.max(...rs.map(r => r.y + r.h)) - y }; }

  function overlapTarget(c) {
    const a = rect(c), me = group(c);
    let best = null, most = 0;
    for (const o of cards) {
      if (o.removed || me.includes(o) || o.el.classList.contains('gone')) continue;
      const b = rect(o);
      const w = Math.min(a.x + a.w, b.x + b.w) - Math.max(a.x, b.x), h = Math.min(a.y + a.h, b.y + b.h) - Math.max(a.y, b.y);
      const part = w > 0 && h > 0 ? (w * h) / Math.min(a.w * a.h, b.w * b.h) : 0;
      if (part > most) { most = part; best = o; }
    }
    return most > 0.35 ? best : null;
  }

  let groups = 0;
  function joinCards(c, into) {
    const g = into.group || ('g' + (++groups));
    const parts = [...group(into), ...group(c).filter(o => o !== into)];
    parts.forEach(o => o.group = g);
    // side by side, in the order they stand, top-aligned with the first
    parts.sort((p, q) => p.x - q.x);
    let x = Math.min(...parts.map(p => p.x)), y = into.y;
    parts.forEach((p, i) => { p.x = x; p.y = y; x += rect(p).w + 2; place(p); p.el.classList.toggle('joined-left', i < parts.length - 1); p.el.classList.toggle('joined-right', i > 0); });
    const box = bounds(parts.map(rect));
    if (box.x + box.w > W) { const shift = box.x + box.w - W + 6; parts.forEach(p => { p.x -= shift; place(p); }); }
    react('together!');
    persist();
  }
  function split(c) {
    const rest = group(c).filter(o => o !== c);
    c.group = null; c.el.classList.remove('joined-left', 'joined-right');
    c.y = Math.min(H - BAR - rect(c).h, c.y + rect(c).h * 0.4 + 20); place(c);
    if (rest.length === 1) { rest[0].group = null; rest[0].el.classList.remove('joined-left', 'joined-right'); }
    else rest.forEach((p, i) => { p.el.classList.toggle('joined-left', i < rest.length - 1); p.el.classList.toggle('joined-right', i > 0); });
  }

  function hookDrag(c) {
    let start = null, longPress = null;
    c.el.addEventListener('pointerdown', e => {
      if (e.button === 2 || e.target.closest('button, input, .track')) return;
      closeMenus();
      c.moved = false;
      const p = point(e);
      start = { px: p.x, py: p.y, x: c.x, y: c.y, at: group(c).map(o => [o, o.x - c.x, o.y - c.y]) };
      c.el.setPointerCapture(e.pointerId);
      if (e.pointerType !== 'mouse') longPress = setTimeout(() => { start = null; openCardMenu(c, p.x, p.y); }, 550);
    });
    c.el.addEventListener('pointermove', e => {
      const p = point(e);
      // the glow under the pointer, for Liquid Glass
      const r = c.el.getBoundingClientRect();
      c.el.querySelector('.skin').style.setProperty('--mx', (e.clientX - r.left) / zoom / c.scale + 'px');
      c.el.querySelector('.skin').style.setProperty('--my', (e.clientY - r.top) / zoom / c.scale + 'px');
      if (!start) return;
      const dx = p.x - start.px, dy = p.y - start.py;
      if (!c.moved && Math.hypot(dx, dy) < 5) return;
      clearTimeout(longPress);
      if (c.locked || set.lockAll) { start = null; c.el.classList.remove('shake'); void c.el.offsetWidth; c.el.classList.add('shake'); say('locked 🔒', c.el); return; }
      c.moved = true;
      if (c.peek) untuck(c);
      start.at.forEach(([o]) => o.el.classList.add('dragging'));
      const [nx, ny] = snapTo(c, start.x + dx, start.y + dy, e.altKey);
      start.at.forEach(([o, ox, oy]) => { o.x = nx + ox; o.y = ny + oy; place(o); });
      const t = set.join ? overlapTarget(c) : null;
      cards.forEach(o => o.el.classList.toggle('target', o === t));
      // whizzing a card around makes Pitzki dizzy
      if (Math.hypot(e.movementX, e.movementY) > 60) { const pz = desk.querySelector('.c-pitzki svg.mascot'); if (pz && !pz.classList.contains('dizzy')) { act(pz, 'dizzy', 1600); say('woah...', pz); } }
    });
    const end = () => {
      clearTimeout(longPress);
      if (!start) return;
      cards.forEach(o => { o.el.classList.remove('dragging'); o.el.classList.remove('target'); });
      if (c.moved && set.join) { const t = overlapTarget(c); if (t) joinCards(c, t); }
      if (c.moved && c.peek) setTimeout(() => tuck(c), 900);
      start = null; persist();
      setTimeout(() => c.moved = false, 0);
    };
    c.el.addEventListener('pointerup', end);
    c.el.addEventListener('pointercancel', end);
    c.el.addEventListener('contextmenu', e => { e.preventDefault(); const p = point(e); openCardMenu(c, p.x, p.y); });
    c.el.addEventListener('pointerenter', () => { if (c.peek && c.tucked) untuck(c); });
    c.el.addEventListener('pointerleave', () => { if (c.peek && !start) setTimeout(() => { if (!c.el.matches(':hover')) tuck(c); }, 700); });
  }
  const point = e => { const d = desk.getBoundingClientRect(); return { x: (e.clientX - d.left) / zoom, y: (e.clientY - d.top) / zoom }; };

  // hide at the screen edge: tucked behind the nearest side, slides out at a touch
  function tuck(c) {
    if (!c.peek || c.tucked) return;
    const r = rect(c);
    c.home = c.x;
    c.x = (c.x + r.w / 2 < W / 2) ? -r.w + 16 : W - 16;
    c.tucked = true; c.el.classList.add('tucked'); place(c);
  }
  function untuck(c) {
    if (!c.tucked) return;
    const r = rect(c);
    c.x = c.x < 0 ? 8 : W - r.w - 8;
    c.tucked = false; c.el.classList.remove('tucked'); place(c);
  }

  // ----- menus -----
  function closeMenus() { desk.querySelectorAll('.menu').forEach(m => m.remove()); desk.querySelector('.tray-icon')?.classList.remove('on'); }
  desk.addEventListener('pointerdown', e => { if (!e.target.closest('.menu, .picker, .tray-icon')) closeMenus(); });

  function menuAt(el, x, y) {
    desk.appendChild(el);
    const w = el.offsetWidth, h = el.offsetHeight;
    el.style.left = Math.min(x, W - w - 6) + 'px';
    el.style.top = Math.min(y, H - BAR - h - 6) + 'px';
  }

  function openCardMenu(c, x, y) {
    closeMenus();
    const tick = on => `<span class="tick">${on ? '✓' : ''}</span>`;
    const sizes = [['Small', .85], ['Normal', 1], ['Big', 1.25], ['Huge', 1.6]];
    const m = $(`<div class="menu">
      ${sizes.map(([n, s]) => `<button data-size="${s}">${tick(c.scale === s)}${n}</button>`).join('')}<hr>
      <button data-a="lock">${tick(c.locked)}Lock in place</button>
      <button data-a="line">${tick(set.lineUp)}Line up with other cards</button>
      <button data-a="glass">${tick(c.glass)}Liquid glass</button>
      <button data-a="level">${tick(false)}Glass: ${c.glassLevel}</button>
      <button data-a="peek">${tick(c.peek)}Hide at the screen edge</button>
      <button data-a="only">${tick(false)}Shows in: ${c.only === 'all' ? 'every profile' : c.only === 'work' ? 'Work' : 'Game'}</button>
      ${c.group ? '<button data-a="split">' + tick(false) + 'Split off</button>' : ''}<hr>
      <button data-a="picker">${tick(false)}Widgets…</button>
      <button data-a="off">${tick(false)}Take this off</button></div>`);
    m.addEventListener('click', e => {
      const b = e.target.closest('button'); if (!b) return;
      if (b.dataset.size) { c.scale = +b.dataset.size; style(c); setTimeout(() => { const [nx, ny] = snapTo(c, c.x, c.y, true); c.x = nx; c.y = ny; place(c); }, 380); }
      const a = b.dataset.a;
      if (a === 'lock') c.locked = !c.locked;
      if (a === 'line') { set.lineUp = !set.lineUp; save(); }
      if (a === 'glass') { c.glass = !c.glass; style(c); }
      if (a === 'level') { c.glassLevel = { Clearer: 'Normal', Normal: 'Darker', Darker: 'Clearer' }[c.glassLevel]; c.glass = true; style(c); }
      if (a === 'peek') { c.peek = !c.peek; c.peek ? setTimeout(() => tuck(c), 300) : untuck(c); }
      if (a === 'only') { c.only = { all: 'work', work: 'game', game: 'all' }[c.only]; style(c); }
      if (a === 'split') split(c);
      if (a === 'picker') openPicker();
      if (a === 'off') remove(c);
      persist(); closeMenus();
    });
    menuAt(m, x, y);
  }

  function openTray() {
    const was = desk.querySelector('.menu.tray'); closeMenus(); if (was) return;
    desk.querySelector('.tray-icon').classList.add('on');
    const sw = on => `<span class="switch${on ? ' on' : ''}"></span>`;
    const prof = { all: 'All cards', work: 'Work', game: 'Game' }[set.profile];
    const m = $(`<div class="menu tray">
      <div class="head">${mascot()}<div><b>Pitzki Widgets</b><span>Little cards on your desktop</span></div></div><hr>
      <div class="row" data-a="picker"><span class="ico"><i class="fi-g">&#xE8A9;</i><svg viewBox="0 0 24 24"><rect x="4" y="4" width="16" height="16" rx="2"/><path d="M12 4v16M4 12h16"/></svg></span><span class="label">Widgets…</span></div><hr>
      <div class="row" data-a="lockAll"><span class="ico"><i class="fi-g">&#xE72E;</i><svg viewBox="0 0 24 24"><rect x="5" y="10" width="14" height="10" rx="2"/><path d="M8 10V7a4 4 0 0 1 8 0v3"/></svg></span><span class="label">Lock every card in place</span>${sw(set.lockAll)}</div>
      <div class="row" data-a="lineUp"><span class="ico"><i class="fi-g">&#xE8FD;</i><svg viewBox="0 0 24 24"><path d="M5 6h14M5 10h14M5 14h14M5 18h14"/></svg></span><span class="label">Line cards up with each other</span>${sw(set.lineUp)}</div>
      <div class="row" data-a="join"><span class="ico"><i class="fi-g">&#xE71B;</i><svg viewBox="0 0 24 24"><rect x="4" y="4" width="10" height="10" rx="1.5"/><rect x="10" y="10" width="10" height="10" rx="1.5"/></svg></span><span class="label">Join cards dropped together</span>${sw(set.join)}</div>
      <div class="row" data-a="glass"><span class="ico"><i class="fi-g">&#xE790;</i><svg viewBox="0 0 24 24"><rect x="4" y="4" width="16" height="16" rx="3"/><path d="M8 15l7-7M8 11l3-3"/></svg></span><span class="label">Liquid glass</span>${sw(set.glass)}</div><hr>
      <div class="row" data-a="profile"><span class="ico"><i class="fi-g">&#xE77B;</i><svg viewBox="0 0 24 24"><circle cx="12" cy="8" r="4"/><path d="M4.5 20a7.5 7.5 0 0 1 15 0"/></svg></span><span class="label">Profile</span><span class="pillsel"><b>${prof}</b><i class="fi-g chev">&#xE76C;</i><i class="fi-s chev">›</i></span></div>
      <div class="row" data-a="autoGame"><span class="ico"><i class="fi-g">&#xE7FC;</i><svg viewBox="0 0 24 24"><rect x="2.5" y="7" width="19" height="11" rx="5.5"/><path d="M7 10.5v4M5 12.5h4"/><circle cx="16" cy="11.5" r=".9" fill="currentColor"/><circle cx="17.8" cy="13.8" r=".9" fill="currentColor"/></svg></span><span class="label">Switch to Game while playing</span>${sw(set.autoGame)}</div><hr>
      <div class="row" data-a="refresh"><span class="ico"><i class="fi-g">&#xE72C;</i><svg viewBox="0 0 24 24"><path d="M19 12a7 7 0 1 1-2.05-4.95"/><path d="M19 4.5V9h-4.5"/></svg></span><span class="label">Refresh</span></div>
      <div class="row quit" data-a="quit"><span class="ico"><i class="fi-g">&#xE7E8;</i><svg viewBox="0 0 24 24"><path d="M12 3.5v8"/><path d="M6.6 6.8a8 8 0 1 0 10.8 0"/></svg></span><span class="label">Quit Pitzki Widgets</span></div></div>`);
    m.addEventListener('click', e => {
      const r = e.target.closest('.row'); if (!r) return;
      const a = r.dataset.a;
      if (['lockAll', 'lineUp', 'join', 'glass', 'autoGame'].includes(a)) {
        set[a] = !set[a]; save(); r.querySelector('.switch').classList.toggle('on', set[a]); restyle();
        if (a === 'glass' && set.glass) react('shiny ✨');
        return;
      }
      if (a === 'profile') { set.profile = { all: 'work', work: 'game', game: 'all' }[set.profile]; save(); r.querySelector('.pillsel b').textContent = { all: 'All cards', work: 'Work', game: 'Game' }[set.profile]; restyle(); return; }
      closeMenus();
      if (a === 'picker') openPicker();
      if (a === 'refresh') { persist(); location.hash = '#try'; location.reload(); }
      if (a === 'reset') { store.set('layout', null); store.set('tasks', null); location.hash = '#try'; location.reload(); }
      if (a === 'quit') quit();
    });
    desk.appendChild(m);
    m.style.right = '8px'; m.style.bottom = (BAR + 6) + 'px';
  }

  function quit() {
    desk.classList.add('quit'); restyle();
    const off = $(`<div class="desk-off"><button>Start Pitzki Widgets</button></div>`);
    off.querySelector('button').addEventListener('click', () => { desk.classList.remove('quit'); off.remove(); restyle(); react('I\'m back!'); });
    desk.insertBefore(off, desk.querySelector('.taskbar'));
  }

  // the "Widgets…" window: a switch for every card
  function openPicker() {
    desk.querySelector('.picker')?.remove();
    const p = $(`<div class="picker"><header>${mascot()}<div><b>Widgets</b><span class="count"></span></div><button class="x"><i class="fi-g">&#xE711;</i><i class="fi-s">✕</i></button></header><div class="list"></div></div>`);
    p.querySelector('.x').addEventListener('click', () => p.remove());
    p.addEventListener('pointerdown', e => e.stopPropagation());
    desk.appendChild(p);
    refreshPicker();
  }
  function refreshPicker() {
    const p = desk.querySelector('.picker'); if (!p) return;
    const list = p.querySelector('.list'), on = k => cards.some(c => c.kind === k && !c.removed);
    let html = '', grp = '';
    for (const [k, v] of Object.entries(KINDS)) {
      if (v.grp !== grp) { grp = v.grp; html += `<div class="grp">${grp}</div>`; }
      html += `<div class="item${on(k) ? ' on' : ''}" data-k="${k}"><span class="badge">${v.ico}</span><span class="words">${v.name}<span>${v.tip}</span></span><span class="switch${on(k) ? ' on' : ''}"></span></div>`;
    }
    list.innerHTML = html + `<p class="sub2" style="margin:10px 4px 0">The app has 43 cards. This little desk has ${Object.keys(KINDS).length} of them.</p>`;
    p.querySelector('.count').textContent = `${cards.filter(c => !c.removed).length} cards on your desktop`;
    list.querySelectorAll('.item').forEach(it => it.addEventListener('click', () => {
      const k = it.dataset.k, here = cards.find(c => c.kind === k && !c.removed);
      if (here) remove(here);
      else { const spot = START.find(s => s[0] === k); const c = make(k, spot[1], spot[2]); const [nx, ny] = snapTo(c, c.x, c.y, true); c.x = nx; c.y = ny; place(c); persist(); }
      refreshPicker();
    }));
  }

  // ----- Pitzki goes exploring: out of his card, along the taskbar, up onto a card, and home when clicked -----
  function explore(card) {
    if (desk.querySelector('.walker')) return;
    const home = card.el.querySelector('.c-pitzki svg.mascot');
    const r = card.el.getBoundingClientRect(), d = desk.getBoundingClientRect();
    const w = $(`<div class="walker">${mascot()}</div>`);
    w.style.left = ((r.left - d.left) / zoom + 50) + 'px'; w.style.top = ((r.top - d.top) / zoom + 30) + 'px';
    desk.appendChild(w);
    home.style.visibility = 'hidden'; roll(card.mood, 'out exploring...');
    const svg = w.querySelector('svg');
    const step = (x, y, then) => { w.classList.add('walking'); w.style.left = x + 'px'; w.style.top = y + 'px'; setTimeout(() => { w.classList.remove('walking'); then?.(); }, 3200); };
    requestAnimationFrame(() => {
      w.style.top = (H - BAR - 56) + 'px';
      setTimeout(() => step(Math.random() < .5 ? 40 : W - 120, H - BAR - 56, () => {
        const others = cards.filter(o => !o.removed && o !== card && !o.el.classList.contains('gone'));
        const t = others[Math.floor(Math.random() * others.length)];
        if (!t) return;
        const tr = rect(t);
        step(tr.x + tr.w / 2 - 35, H - BAR - 56, () => { w.style.top = (tr.y - 46) + 'px'; act(svg, 'wave', 1500); setTimeout(() => say('nice view!', w), 500); });
      }), 500);
    });
    w.addEventListener('click', () => { w.remove(); home.style.visibility = ''; roll(card.mood, 'home!', true); act(home, 'jump', 600); });
  }

  // ----- a pretend game, for the Game profile -----
  const gameBtn = desk.querySelector('.game-btn');
  gameBtn?.addEventListener('click', () => {
    const on = !desk.classList.contains('playing');
    desk.classList.toggle('playing', on);
    gameBtn.classList.toggle('on', on);
    gameBtn.textContent = on ? '🎮 Quit the game' : '🎮 Start a game';
    if (set.autoGame) { set.profile = on ? 'game' : 'all'; save(); restyle(); }
    pitzkiSvgs().forEach(s => { s.dataset.hat = on ? 'crown' : ''; if (on) act(s, 'cheer', 1700); });
    react(on ? 'go go go!' : 'gg!');
  });

  // the taskbar clock and the tray icon
  const tclock = desk.querySelector('.taskbar .clock');
  const ttick = () => { const n = new Date(); tclock.innerHTML = `${n.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', hour12: false })}<br>${n.toLocaleDateString('en-GB')}`; };
  ttick(); setInterval(ttick, 10000);
  desk.querySelector('.tray-icon').addEventListener('click', e => { e.stopPropagation(); openTray(); });

  // ----- start: the saved desk, or the first one -----
  fit();
  const ONLY = { tasks: 'work', focus: 'work', pc: 'game' };   // so the profiles have something to show
  const first = layout || START.map(([k, x, y]) => ({ k, x, y, only: ONLY[k] }));
  first.forEach((o, i) => setTimeout(() => make(o.k, o.x, o.y, o), 120 * i));
  setTimeout(() => { cards.filter(c => c.peek).forEach(tuck); }, 120 * first.length + 600);
  setTimeout(() => desk.querySelector('.tray-icon').classList.add('pulse'), 2500);

  // eyes follow the pointer, like the app
  addEventListener('pointermove', e => {
    for (const svg of desk.querySelectorAll('svg.mascot')) {
      const b = svg.getBoundingClientRect(); if (!b.width) continue;
      const dx = Math.max(-1, Math.min(1, (e.clientX - (b.left + b.width / 2)) / 260)), dy = Math.max(-1, Math.min(1, (e.clientY - (b.top + b.height / 2)) / 200));
      const f = svg.querySelector('.face'); if (f) f.style.transform = `translate(${dx * 3.2}px, ${dy * 2.4}px)`;
    }
  }, { passive: true });
})();

// ===== Pitzki's corner: every scene from the app, one click away =====
(() => {
  const grid = document.getElementById('pz-grid');
  if (!grid) return;
  const view = document.getElementById('pz-view'), name = document.getElementById('pz-name');
  const big = view.querySelector('svg.mascot');
  let timer = null;
  for (const s of window.SCENES || []) {
    const b = document.createElement('button');
    b.innerHTML = `<img src="scenes/${s.k}.png" alt="" loading="lazy">${s.n}${s.when ? `<small>${s.when}</small>` : ''}`;
    // the little picture comes alive (60 fps) while the pointer is on it
    b.addEventListener('pointerenter', e => {
      if (e.pointerType !== 'mouse' || b.querySelector('video') || !window.Clips) return;
      const v = Clips.make(`scenes/${s.k}`, s.n); v.className = 'tile-clip';
      v.addEventListener('loadeddata', () => { if (b.matches(':hover')) v.classList.add('on'); else v.remove(); }, { once: true });
      b.querySelector('img').after(v);
    });
    b.addEventListener('pointerleave', () => b.querySelector('video.tile-clip')?.remove());
    b.addEventListener('click', () => {
      grid.querySelectorAll('button').forEach(x => x.classList.toggle('on', x === b));
      view.querySelector('img, video')?.remove();
      const img = Clips.make(`scenes/${s.k}`, s.n);     // a new clip starts from the beginning
      big.style.visibility = 'hidden';
      view.appendChild(img);
      name.textContent = s.n;
      clearTimeout(timer);
      timer = setTimeout(() => { img.remove(); big.style.visibility = ''; name.textContent = 'Pick a scene'; b.classList.remove('on'); }, s.k === 'case' ? 12500 : 7500);
    });
    grid.appendChild(b);
  }
  // his looks: the glow colour and a hat, like Piztki's own page
  document.getElementById('pz-colors')?.addEventListener('click', e => {
    const b = e.target.closest('button'); if (!b) return;
    e.currentTarget.querySelectorAll('button').forEach(x => x.classList.toggle('on', x === b));
    document.documentElement.style.setProperty('--accent', b.dataset.color);
  });
  document.getElementById('pz-hats')?.addEventListener('click', e => {
    const b = e.target.closest('button'); if (!b) return;
    e.currentTarget.querySelectorAll('button').forEach(x => x.classList.toggle('on', x === b));
    document.querySelectorAll('svg.mascot').forEach(s => s.dataset.hat = b.dataset.hat);
  });
  document.getElementById('pz-moves')?.addEventListener('click', e => {
    const b = e.target.closest('button'); if (!b) return;
    const cls = b.dataset.move;
    big.classList.remove('dance', 'wave', 'dizzy', 'sleep', 'cheer', 'jump');
    void big.getBoundingClientRect();
    big.classList.add(cls);
    if (cls !== 'dance' && cls !== 'sleep') setTimeout(() => big.classList.remove(cls), 1800);
    e.currentTarget.querySelectorAll('button').forEach(x => x.classList.toggle('on', x === b && (cls === 'dance' || cls === 'sleep')));
  });
})();
