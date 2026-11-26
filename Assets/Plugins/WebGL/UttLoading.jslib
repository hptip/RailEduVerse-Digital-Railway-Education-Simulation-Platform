// Màn hình chờ chuyển scene của bản Web UTTVR (bộ UI Liquid Glass).
// Vẽ bằng HTML/CSS phía trên canvas Unity: thanh tiến độ và hiệu ứng chạy bằng transform/opacity
// nên trình duyệt vẫn cho chuyển động mượt cả khi Unity đang bận khởi tạo scene.
// Ảnh nền và mẹo đọc từ StreamingAssets/Loading/loading.json (thay ảnh không cần build lại).
mergeInto(LibraryManager.library, {
  // Khung game giãn kín cửa sổ ở mọi scene. Không tự bật toàn màn hình.
  // Nếu dùng toàn màn hình (F11 hoặc nút của Unity), màn hình chờ vẫn hiện.
  UttWebFullscreenInit: function () {
    if (window.__uttFs) return;
    window.__uttFs = true;
    var canvas = Module.canvas, container = canvas ? canvas.parentElement : null;
    if (!container) return;

    // Giãn khung game phủ kín cửa sổ trình duyệt ở mọi scene (giống scene Quản lý vòng đời),
    // ẩn thanh logo phía dưới. Không phải toàn màn hình thật nên không cần người dùng bấm trước.
    Object.assign(container.style, { position: 'fixed', left: '0', top: '0', right: '0', bottom: '0',
      width: '100%', height: '100%', transform: 'none', margin: '0', overflow: 'hidden' });
    Object.assign(canvas.style, { width: '100%', height: '100%', display: 'block' });
    var footer = document.getElementById('unity-footer');
    if (footer) footer.style.display = 'none';
    document.documentElement.style.background = '#1E1E4E';
    document.body.style.margin = '0';
    document.body.style.overflow = 'hidden';
    // Vẽ theo đúng kích thước cửa sổ, không nhân thêm hệ số phóng đại của Windows (125%/150%):
    // nhẹ hơn 1,5–2,3 lần ở laptop, hình vẫn đúng tỉ lệ. Độ phân giải còn chỉnh được trong menu E → Chất lượng.
    Module.devicePixelRatio = 1;

    var css = document.createElement('style');
    css.textContent =
      '#' + (container.id || 'unity-container') + ':fullscreen{position:fixed!important;left:0!important;top:0!important;width:100%!important;height:100%!important;transform:none!important;background:#000}' +
      '#' + (container.id || 'unity-container') + ':-webkit-full-screen{position:fixed!important;left:0!important;top:0!important;width:100%!important;height:100%!important;transform:none!important;background:#000}' +
      '#' + (container.id || 'unity-container') + ':fullscreen canvas{width:100%!important;height:100%!important}' +
      '#' + (container.id || 'unity-container') + ':-webkit-full-screen canvas{width:100%!important;height:100%!important}';
    document.head.appendChild(css);

    function fsElement() { return document.fullscreenElement || document.webkitFullscreenElement || null; }
    function enter() {
      if (fsElement()) return;
      var req = container.requestFullscreen || container.webkitRequestFullscreen;
      if (!req) return;
      try { var p = req.call(container); if (p && p.catch) p.catch(function () {}); } catch (e) {}
    }
    var unityBtn = document.getElementById('unity-fullscreen-button');
    if (unityBtn) unityBtn.onclick = enter;

    // Đưa màn hình chờ vào đúng chỗ mỗi khi bật/tắt toàn màn hình
    function rehome() {
      var L = window.__uttLoading;
      if (!L || !L.root) return;
      var fs = fsElement();
      var host = fs && fs.tagName !== 'CANVAS' ? fs : document.body;
      if (L.root.parentNode !== host) host.appendChild(L.root);
    }
    window.__uttRehome = rehome;
    document.addEventListener('fullscreenchange', rehome);
    document.addEventListener('webkitfullscreenchange', rehome);
  },

  UttLoadingShow: function (keyPtr, titlePtr, basePtr) {
    var key = UTF8ToString(keyPtr), title = UTF8ToString(titlePtr), base = UTF8ToString(basePtr);
    var L = window.__uttLoading || (window.__uttLoading = {});
    if (!L.root) {
      var css = document.createElement('style');
      css.textContent =
        '#utt-ld{position:fixed;inset:0;z-index:2147483000;background:#1E1E4E;font-family:"Be Vietnam Pro",system-ui,sans-serif;color:#fff;opacity:0;transition:opacity .25s ease;pointer-events:auto;display:none}' +
        '#utt-ld.on{opacity:1}#utt-ld.out{opacity:0;transition:opacity .45s ease}' +
        '#utt-ld .bg{position:absolute;left:0;right:0;top:0;bottom:0;background:linear-gradient(180deg,#3E6FB8 0%,#24497E 55%,#0B1630 100%);background-size:cover;background-position:center;transform:scale(1.04);transition:transform 12s ease-out}' +
        '#utt-ld.on .bg{transform:scale(1)}' +
        '#utt-ld .shade{position:absolute;inset:0;background:linear-gradient(180deg,rgba(26,26,72,.12) 0%,rgba(26,26,72,.32) 55%,rgba(26,26,72,.85) 100%)}' +
        '#utt-ld .card{position:absolute;left:50%;bottom:4vh;transform:translateX(-50%);width:min(640px,86vw);box-sizing:border-box;padding:26px 30px;border-radius:28px;' +
          'background:linear-gradient(160deg,rgba(38,38,96,.68),rgba(30,30,78,.54));backdrop-filter:blur(22px) saturate(160%);-webkit-backdrop-filter:blur(22px) saturate(160%);' +
          'border:1px solid rgba(255,255,255,.26);box-shadow:inset 0 1px 0 rgba(255,255,255,.4),0 30px 60px rgba(6,14,32,.45)}' +
        '#utt-ld .kick{font-size:13px;font-weight:600;letter-spacing:.12em;text-transform:uppercase;color:rgba(255,255,255,.72)}' +
        '#utt-ld .title{margin-top:6px;font-size:30px;font-weight:700;line-height:1.2}' +
        '#utt-ld .bar{position:relative;margin-top:18px;height:10px;border-radius:999px;background:rgba(255,255,255,.14);overflow:hidden}' +
        '#utt-ld .fill{position:absolute;inset:0;border-radius:999px;background:#F7931E;transform-origin:0 50%;transform:scaleX(0);will-change:transform}' +
        '#utt-ld .shine{position:absolute;top:0;bottom:0;width:35%;background:linear-gradient(90deg,rgba(255,255,255,0),rgba(255,255,255,.45),rgba(255,255,255,0));animation:utt-ld-shine 1.4s linear infinite;will-change:transform}' +
        '@keyframes utt-ld-shine{from{transform:translateX(-120%)}to{transform:translateX(320%)}}' +
        '#utt-ld .row{margin-top:12px;display:flex;align-items:center;gap:10px;font-size:15px;color:rgba(255,255,255,.88)}' +
        '#utt-ld .spin{width:16px;height:16px;border-radius:50%;border:2px solid rgba(255,255,255,.25);border-top-color:#fff;animation:utt-ld-spin .8s linear infinite;will-change:transform}' +
        '@keyframes utt-ld-spin{to{transform:rotate(360deg)}}' +
        '#utt-ld .tip{margin-top:14px;font-size:14px;color:rgba(255,255,255,.75)}#utt-ld .tip b{color:#FFD77A}';
      document.head.appendChild(css);
      var font = document.createElement('link');
      font.rel = 'stylesheet';
      font.href = 'https://fonts.googleapis.com/css2?family=Be+Vietnam+Pro:wght@400;600;700&display=swap';
      document.head.appendChild(font);

      var root = document.createElement('div');
      root.id = 'utt-ld';
      root.innerHTML = '<div class="bg"></div><div class="shade"></div><div class="card">' +
        '<div class="kick">Đang chuyển tới</div><div class="title"></div>' +
        '<div class="bar"><div class="fill"></div><div class="shine"></div></div>' +
        '<div class="row"><div class="spin"></div><span class="status"></span></div><div class="tip"></div></div>';
      document.body.appendChild(root);
      L.root = root;
      L.bg = root.querySelector('.bg');
      L.title = root.querySelector('.title');
      L.fill = root.querySelector('.fill');
      L.status = root.querySelector('.status');
      L.tip = root.querySelector('.tip');
      L.value = 0;
      L.set = function (v, seconds, easing) {
        L.fill.style.transition = 'transform ' + seconds + 's ' + (easing || 'ease-out');
        L.fill.style.transform = 'scaleX(' + v + ')';
      };
      L.apply = function () {
        var cfg = L.config || {}, item = cfg[L.key] || {};
        if (item.title) L.title.textContent = item.title;
        var tips = item.tips || cfg.tips || [];
        if (tips.length) L.tip.innerHTML = 'Mẹo: ' + tips[Math.floor(Math.random() * tips.length)];
        if (item.color) L.bg.style.backgroundImage = item.color;
        var shift = (typeof item.shift === 'number') ? item.shift : (typeof cfg.shift === 'number' ? cfg.shift : 0);
        // Khung ảnh kéo dài lên trên (không dịch cả khung) → đáy ảnh luôn chạm đáy màn hình, không hở
        L.bg.style.top = (-shift) + 'vh';
        L.bg.style.bottom = '0';
        L.bg.style.backgroundPosition = shift > 0 ? 'center bottom' : 'center';
        if (item.image) {
          var url = L.base + '/Loading/' + item.image, img = new Image();
          img.onload = function () { if (L.key === L.loadingKey) L.bg.style.backgroundImage = 'url("' + url + '")'; };
          L.loadingKey = L.key;
          img.src = url;
        }
      };
    }
    L.base = base;
    L.key = key;
    L.title.textContent = title;
    L.tip.textContent = '';
    L.bg.style.backgroundImage = '';
    L.status.textContent = 'Đang tải…';
    L.value = 0;
    L.set(0, 0);
    clearTimeout(L.hideTimer);
    if (window.__uttRehome) window.__uttRehome();
    L.root.classList.remove('out');
    L.root.style.display = 'block';
    void L.root.offsetWidth;
    L.root.classList.add('on');
    // Nhích chậm trong lúc chờ % thật, để người dùng thấy đang chạy
    requestAnimationFrame(function () { L.set(0.3, 6, 'cubic-bezier(.1,.6,.3,1)'); });

    if (L.config) L.apply();
    else if (!L.fetching) {
      L.fetching = true;
      fetch(base + '/Loading/loading.json', { cache: 'no-cache' })
        .then(function (r) { return r.ok ? r.json() : {}; })
        .then(function (j) { L.config = j || {}; L.apply(); })
        .catch(function () { L.config = {}; });
    }
  },

  UttLoadingProgress: function (p) {
    var L = window.__uttLoading;
    if (!L || !L.root) return;
    var v = Math.max(0, Math.min(1, p)) * 0.7;
    if (v <= L.value) return;
    L.value = v;
    L.set(v, 0.35);
    L.status.textContent = 'Đang tải… ' + Math.round(v * 100) + '%';
  },

  UttLoadingPhase2: function () {
    var L = window.__uttLoading;
    if (!L || !L.root) return;
    L.value = Math.max(L.value, 0.7);
    L.status.textContent = 'Đang chuẩn bị dữ liệu…';
    // Bò chậm dần tới 95% (do trình duyệt tự chạy, không cần Unity rảnh)
    // Đặt ngay (không dùng setTimeout: Unity có thể chiếm luồng chính ngay sau lệnh này)
    L.set(0.95, 30, 'cubic-bezier(.05,.75,.2,1)');
  },

  UttLoadingReady: function () {
    var L = window.__uttLoading;
    if (!L || !L.root) return;
    L.value = 1;
    L.set(1, 0.3);
    L.status.textContent = 'Sẵn sàng';
    clearTimeout(L.hideTimer);
    L.hideTimer = setTimeout(function () {
      L.root.classList.add('out');
      L.root.classList.remove('on');
      L.hideTimer = setTimeout(function () { L.root.style.display = 'none'; L.root.classList.remove('out'); }, 480);
    }, 380);
  }
});
