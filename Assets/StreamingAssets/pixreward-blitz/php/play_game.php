<?php
/**
 * play_game.php — PixReward Blitz · Game Engine v3
 *
 * Professional HTML5 game player with advanced JavaScript engine.
 * Handles ALL types of online game URLs with browser-native fluidity.
 *
 * Features:
 *   - Intelligent loading strategy (direct / proxy / auto-detect)
 *   - Anti-frame-busting protection (5-layer defense)
 *   - Fullscreen API blocking
 *   - Touch/pointer optimization for mobile WebViews
 *   - Audio context auto-resume on visibility change
 *   - Memory pressure monitoring & cleanup
 *   - Resource hints (dns-prefetch, preconnect)
 *   - Navigation interception (prevents game redirects)
 *   - Graceful error recovery with retry
 *   - postMessage protocol for parent communication
 *
 * GET Parameters:
 *   url   (required) — Original game URL
 *   mode  (optional) — auto | direct | proxy  (default: auto)
 *   title (optional) — Game display name
 *
 * postMessage Protocol (sends to window.parent):
 *   { type: 'pixreward:game_ready',  url }
 *   { type: 'pixreward:game_error',  url, error }
 *   { type: 'pixreward:game_closed', url }
 *
 * Listens from window.parent:
 *   { type: 'pixreward:close_game' }
 */

declare(strict_types=1);

// ═══════════════════════════════════════════════════════════════════════════
// Domain Configuration
// ═══════════════════════════════════════════════════════════════════════════

$directDomains = [
    'play.famobi.com',
    'html5.gamedistribution.com',
    'html5.gamemonetize.com',
    'www.crazygames.com',
    'itch.io',
    'itch.zone',
    'html-classic.itch.zone',
    'playcutegames.com',
    'www.construct.net',
    'preview.construct.net',
];

$proxyDomains = [
    'cdn-factory.marketjs.com',
    'cdn.wanted5games.com',
    'storage.y8.com',
    'games.cdn.famobi.com',
    'y8.com',
];

// ═══════════════════════════════════════════════════════════════════════════
// Helpers
// ═══════════════════════════════════════════════════════════════════════════

function halt(string $msg): never
{
    http_response_code(400);
    header('Content-Type: text/html; charset=utf-8');
    $safe = htmlspecialchars($msg, ENT_QUOTES, 'UTF-8');
    echo <<<HTML
<!DOCTYPE html>
<html lang="pt-BR"><head><meta charset="UTF-8">
<meta name="viewport" content="width=device-width,initial-scale=1,maximum-scale=1,user-scalable=no">
<style>
*{margin:0;padding:0;box-sizing:border-box}
html,body{width:100%;height:100%;background:#0f172a;color:#f87171;
font-family:system-ui,-apple-system,sans-serif;display:flex;align-items:center;
justify-content:center;text-align:center;padding:24px}
.c{max-width:320px}
h2{font-size:16px;font-weight:700;margin-bottom:8px}
p{font-size:13px;line-height:1.5;color:#94a3b8;margin-bottom:20px}
.b{display:inline-block;padding:12px 28px;background:#3b82f6;color:#fff;border:none;
border-radius:12px;font:600 13px/1 system-ui,sans-serif;cursor:pointer;transition:all .15s}
.b:active{transform:scale(.95);opacity:.8}
</style></head>
<body><div class="c">
<h2>{$safe}</h2>
<p>Não foi possível iniciar o jogo. Verifique sua conexão e tente novamente.</p>
<button class="b" onclick="location.reload()">Tentar novamente</button>
</div></body></html>
HTML;
    exit;
}

function domainMatch(string $host, array $list): bool
{
    $host = strtolower($host);
    foreach ($list as $d) {
        if ($host === strtolower($d) || str_ends_with($host, '.' . strtolower($d))) {
            return true;
        }
    }
    return false;
}

// ═══════════════════════════════════════════════════════════════════════════
// Input Validation
// ═══════════════════════════════════════════════════════════════════════════

$gameUrl   = trim((string) ($_GET['url']   ?? ''));
$mode      = strtolower(trim((string) ($_GET['mode']  ?? 'auto')));
$gameTitle = trim((string) ($_GET['title'] ?? 'Jogo'));

if ($gameUrl === '' || !filter_var($gameUrl, FILTER_VALIDATE_URL)) {
    halt('URL do jogo inválida ou ausente');
}

$parts  = parse_url($gameUrl);
$host   = strtolower((string) ($parts['host']   ?? ''));
$scheme = strtolower((string) ($parts['scheme'] ?? 'https'));

if (!in_array($scheme, ['http', 'https'], true)) {
    halt('Apenas URLs http/https são permitidas');
}

// ═══════════════════════════════════════════════════════════════════════════
// Loading Strategy
// ═══════════════════════════════════════════════════════════════════════════

$useProxy = match ($mode) {
    'proxy'  => true,
    'direct' => false,
    default  => domainMatch($host, $proxyDomains),
};

$embedUrl = $useProxy
    ? 'game_proxy.php?url=' . urlencode($gameUrl) . '&v=8'
    : $gameUrl;

// ═══════════════════════════════════════════════════════════════════════════
// HTTP Headers
// ═══════════════════════════════════════════════════════════════════════════

header('Content-Type: text/html; charset=utf-8');
header('Cache-Control: no-store');
header_remove('X-Frame-Options');

// ═══════════════════════════════════════════════════════════════════════════
// PHP → JS Variables
// ═══════════════════════════════════════════════════════════════════════════

$jsEmbedUrl = json_encode($embedUrl, JSON_UNESCAPED_SLASHES);
$jsRawUrl   = json_encode($gameUrl,  JSON_UNESCAPED_SLASHES);
$jsTitle    = json_encode($gameTitle, JSON_UNESCAPED_SLASHES);
$jsHost     = json_encode($host,     JSON_UNESCAPED_SLASHES);
$jsProxied  = $useProxy ? 'true' : 'false';
$safeTitle  = htmlspecialchars($gameTitle, ENT_QUOTES, 'UTF-8');

$preconnectOrigin = $scheme . '://' . $host;
?>
<!DOCTYPE html>
<html lang="pt-BR">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width,initial-scale=1,maximum-scale=1,minimum-scale=1,user-scalable=no,viewport-fit=cover">
<meta name="theme-color" content="#000000">
<title>PixReward — <?= $safeTitle ?></title>

<!-- Resource Hints -->
<link rel="dns-prefetch" href="<?= htmlspecialchars($preconnectOrigin) ?>">
<link rel="preconnect"   href="<?= htmlspecialchars($preconnectOrigin) ?>" crossorigin>

<style>
/* ═══════════════════════════════════════════════════════════════════════
   Reset & Viewport
   ═══════════════════════════════════════════════════════════════════════ */
*,*::before,*::after{margin:0;padding:0;box-sizing:border-box}

html{
  width:100%;height:100%;overflow:hidden;
  position:relative;
  -webkit-text-size-adjust:100%;
}

body{
  position:relative;
  width:100%;height:100%;overflow:hidden;
  background:#000;color:#fff;
  font-family:system-ui,-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;
  touch-action:none;
  -webkit-user-select:none;user-select:none;
  overscroll-behavior:none;
  -webkit-tap-highlight-color:transparent;
  -webkit-overflow-scrolling:auto;
}

/* ═══════════════════════════════════════════════════════════════════════
   Game Iframe — full viewport, optimized rendering
   ═══════════════════════════════════════════════════════════════════════ */
#game-frame{
  position:absolute;top:0;left:0;
  width:100%;height:100%;
  border:none;display:block;
  background:#000;
  touch-action:auto;
  z-index:1;
  opacity:0;
  transition:opacity .4s ease;
}
#game-frame.visible{opacity:1}

/* ═══════════════════════════════════════════════════════════════════════
   Loading Overlay
   ═══════════════════════════════════════════════════════════════════════ */
#loader{
  position:absolute;top:0;left:0;
  width:100%;height:100%;
  display:flex;flex-direction:column;
  align-items:center;justify-content:center;
  background:linear-gradient(135deg,#0f172a 0%,#1e1b4b 100%);
  z-index:10;
  transition:opacity .5s ease,visibility .5s;
}
#loader.hidden{opacity:0;visibility:hidden;pointer-events:none}

/* Spinner */
.ld-spinner{
  position:relative;width:56px;height:56px;margin-bottom:28px;
}
.ld-ring{
  position:absolute;inset:0;
  border:3px solid transparent;border-radius:50%;
}
.ld-ring:nth-child(1){border-top-color:#6366f1;animation:ld-spin .8s linear infinite}
.ld-ring:nth-child(2){inset:7px;border-right-color:#a78bfa;animation:ld-spin 1.2s linear infinite reverse}
.ld-ring:nth-child(3){inset:14px;border-bottom-color:#818cf8;animation:ld-spin 1.6s linear infinite}
.ld-dot{
  position:absolute;top:50%;left:50%;
  width:6px;height:6px;margin:-3px 0 0 -3px;
  background:#6366f1;border-radius:50%;
  animation:ld-pulse 1s ease-in-out infinite;
}

/* Text */
.ld-title{font-size:15px;font-weight:700;color:#e2e8f0;margin-bottom:6px;text-align:center}
.ld-sub{font-size:11px;color:#94a3b8;text-align:center;transition:all .3s}

/* Progress */
.ld-bar-wrap{
  width:200px;height:3px;background:rgba(255,255,255,.08);
  border-radius:2px;margin-top:24px;overflow:hidden;
}
.ld-bar{
  width:0%;height:100%;
  background:linear-gradient(90deg,#6366f1,#a78bfa,#6366f1);
  background-size:200% 100%;
  border-radius:2px;
  transition:width .4s ease;
  animation:ld-shimmer 1.5s linear infinite;
}

/* Steps */
.ld-steps{display:flex;gap:6px;margin-top:16px}
.ld-step{
  width:36px;height:3px;border-radius:2px;
  background:rgba(255,255,255,.06);
  transition:background .4s,box-shadow .4s;
}
.ld-step.active{background:#6366f1;box-shadow:0 0 10px rgba(99,102,241,.5)}
.ld-step.done{background:#22c55e;box-shadow:0 0 8px rgba(34,197,94,.3)}

/* Error State */
.ld-error{
  display:flex;flex-direction:column;align-items:center;
  max-width:280px;text-align:center;
}
.ld-error-icon{
  width:56px;height:56px;border-radius:50%;
  background:rgba(239,68,68,.12);
  display:flex;align-items:center;justify-content:center;
  margin-bottom:16px;
}
.ld-error-icon svg{width:24px;height:24px;color:#ef4444}
.ld-error h3{font-size:15px;font-weight:700;color:#f87171;margin-bottom:8px}
.ld-error p{font-size:12px;color:#94a3b8;line-height:1.5;margin-bottom:20px}
.ld-retry{
  padding:12px 36px;
  background:linear-gradient(135deg,#6366f1,#7c3aed);
  color:#fff;border:none;border-radius:14px;
  font:600 13px/1 system-ui,sans-serif;
  cursor:pointer;transition:all .15s;
  box-shadow:0 4px 20px rgba(99,102,241,.3);
}
.ld-retry:active{transform:scale(.95);opacity:.85}

/* ═══════════════════════════════════════════════════════════════════════
   Animations
   ═══════════════════════════════════════════════════════════════════════ */
@keyframes ld-spin{to{transform:rotate(360deg)}}
@keyframes ld-pulse{0%,100%{opacity:.3;transform:scale(.7)}50%{opacity:1;transform:scale(1.3)}}
@keyframes ld-shimmer{0%{background-position:200% 0}100%{background-position:-200% 0}}
</style>
</head>
<body>

<!-- Loading Overlay -->
<div id="loader">
  <div class="ld-spinner" id="ld-spinner">
    <div class="ld-ring"></div>
    <div class="ld-ring"></div>
    <div class="ld-ring"></div>
    <div class="ld-dot"></div>
  </div>
  <div class="ld-title" id="ld-title">Preparando jogo…</div>
  <div class="ld-sub" id="ld-sub"><?= $safeTitle ?></div>
  <div class="ld-bar-wrap"><div class="ld-bar" id="ld-bar"></div></div>
  <div class="ld-steps" id="ld-steps">
    <div class="ld-step" id="s0"></div>
    <div class="ld-step" id="s1"></div>
    <div class="ld-step" id="s2"></div>
    <div class="ld-step" id="s3"></div>
  </div>
</div>

<!-- Game Iframe -->
<iframe
  id="game-frame"
  src="about:blank"
  allow="autoplay; fullscreen; gyroscope; accelerometer; pointer-lock; gamepad; clipboard-write; clipboard-read; web-share; screen-wake-lock; xr-spatial-tracking"
  allowfullscreen
  referrerpolicy="no-referrer-when-downgrade"
  importance="high"
  loading="eager"
></iframe>

<!-- ═══════════════════════════════════════════════════════════════════════
     JavaScript — PixReward Game Engine v3
     ═══════════════════════════════════════════════════════════════════════ -->
<script>
(function() {
  'use strict';

  // ════════════════════════════════════════════════════════════════════════
  // Configuration (injected by PHP)
  // ════════════════════════════════════════════════════════════════════════

  var CONFIG = {
    embedUrl:    <?= $jsEmbedUrl ?>,
    rawUrl:      <?= $jsRawUrl ?>,
    title:       <?= $jsTitle ?>,
    host:        <?= $jsHost ?>,
    proxied:     <?= $jsProxied ?>,
    timeout:     20000,
    revealDelay: 400,
    retryMax:    2
  };

  // ════════════════════════════════════════════════════════════════════════
  // LAYER 1: Fullscreen API Blocking
  // Prevents games from entering fullscreen which breaks WebView layout
  // ════════════════════════════════════════════════════════════════════════

  (function() {
    var reject = function() {
      return Promise.reject(new DOMException('Fullscreen disabled by host', 'NotAllowedError'));
    };
    var targets = [
      [Element.prototype, 'requestFullscreen'],
      [Element.prototype, 'webkitRequestFullscreen'],
      [Element.prototype, 'webkitRequestFullScreen'],
      [Element.prototype, 'mozRequestFullScreen'],
      [Element.prototype, 'msRequestFullscreen'],
      [HTMLElement.prototype, 'webkitRequestFullScreen']
    ];
    for (var i = 0; i < targets.length; i++) {
      try { if (targets[i][0][targets[i][1]]) targets[i][0][targets[i][1]] = reject; } catch(e) {}
    }
    var props = [
      [document, 'fullscreenEnabled'],
      [document, 'webkitFullscreenEnabled'],
      [document, 'mozFullScreenEnabled'],
      [document, 'msFullscreenEnabled']
    ];
    for (var j = 0; j < props.length; j++) {
      try {
        Object.defineProperty(props[j][0], props[j][1], { value: false, writable: false, configurable: true });
      } catch(e) {}
    }
  })();

  // ════════════════════════════════════════════════════════════════════════
  // LAYER 2: Screen Orientation Lock Intercept
  // ════════════════════════════════════════════════════════════════════════

  try {
    if (screen.orientation && screen.orientation.lock) {
      screen.orientation.lock = function() { return Promise.resolve(); };
    }
  } catch(e) {}

  // ════════════════════════════════════════════════════════════════════════
  // LAYER 3: Navigation Guard
  // Intercepts window.open, alert, confirm, prompt to prevent popups
  // and unwanted navigations from breaking the game container
  // ════════════════════════════════════════════════════════════════════════

  (function() {
    var _open = window.open;
    window.open = function(url) {
      if (url && typeof url === 'string') {
        try {
          var parsed = new URL(url, window.location.href);
          if (parsed.origin === window.location.origin) return _open.apply(window, arguments);
        } catch(e) {}
      }
      return null;
    };

    window.alert   = function() {};
    window.confirm = function() { return true; };
    window.prompt  = function() { return null; };
  })();

  // ════════════════════════════════════════════════════════════════════════
  // LAYER 4: beforeunload Prevention
  // Prevents the game page from navigating away
  // ════════════════════════════════════════════════════════════════════════

  window.addEventListener('beforeunload', function(e) {
    e.preventDefault();
    e.returnValue = '';
  });

  // ════════════════════════════════════════════════════════════════════════
  // DOM References
  // ════════════════════════════════════════════════════════════════════════

  var frame   = document.getElementById('game-frame');
  var loader  = document.getElementById('loader');
  var elTitle = document.getElementById('ld-title');
  var elSub   = document.getElementById('ld-sub');
  var elSpin  = document.getElementById('ld-spinner');
  var elBar   = document.getElementById('ld-bar');
  var steps   = [
    document.getElementById('s0'),
    document.getElementById('s1'),
    document.getElementById('s2'),
    document.getElementById('s3')
  ];

  // ════════════════════════════════════════════════════════════════════════
  // State Machine
  // ════════════════════════════════════════════════════════════════════════

  var STATES = {
    IDLE:         0,
    CONNECTING:   1,
    DOWNLOADING:  2,
    RENDERING:    3,
    READY:        4,
    ERROR:        5
  };

  var state       = STATES.IDLE;
  var retryCount  = 0;
  var loadFired   = false;
  var progressVal = 0;
  var progressTimer = null;

  // ════════════════════════════════════════════════════════════════════════
  // Progress Simulation
  // Smoothly animates the progress bar during load to give visual feedback
  // ════════════════════════════════════════════════════════════════════════

  function startProgress() {
    progressVal = 0;
    updateBar(0);

    var targets = [
      { to: 25, duration: 800 },
      { to: 50, duration: 2000 },
      { to: 70, duration: 4000 },
      { to: 85, duration: 6000 },
      { to: 92, duration: 10000 }
    ];
    var idx = 0;

    function next() {
      if (idx >= targets.length || state === STATES.READY || state === STATES.ERROR) return;
      var t = targets[idx++];
      animateBar(t.to, t.duration);
      progressTimer = setTimeout(next, t.duration);
    }
    next();
  }

  function animateBar(to, duration) {
    elBar.style.transition = 'width ' + duration + 'ms ease';
    elBar.style.width = to + '%';
    progressVal = to;
  }

  function updateBar(val) {
    elBar.style.transition = 'none';
    elBar.style.width = val + '%';
    progressVal = val;
  }

  function finishProgress() {
    if (progressTimer) clearTimeout(progressTimer);
    elBar.style.transition = 'width .3s ease';
    elBar.style.width = '100%';
  }

  // ════════════════════════════════════════════════════════════════════════
  // Step Indicators
  // ════════════════════════════════════════════════════════════════════════

  function setStep(n) {
    for (var i = 0; i < steps.length; i++) {
      steps[i].className = i < n ? 'ld-step done' : (i === n ? 'ld-step active' : 'ld-step');
    }
  }

  // ════════════════════════════════════════════════════════════════════════
  // State Transitions
  // ════════════════════════════════════════════════════════════════════════

  function transition(newState, opts) {
    if (state === STATES.READY || state === STATES.ERROR) return;
    state = newState;
    opts  = opts || {};

    switch (newState) {
      case STATES.CONNECTING:
        setStep(0);
        elTitle.textContent = 'Conectando ao servidor…';
        elSub.textContent   = CONFIG.title;
        startProgress();
        break;

      case STATES.DOWNLOADING:
        setStep(1);
        elTitle.textContent = 'Carregando jogo…';
        elSub.textContent   = 'Baixando recursos do jogo';
        break;

      case STATES.RENDERING:
        setStep(2);
        elTitle.textContent = 'Quase pronto…';
        elSub.textContent   = 'Inicializando engine do jogo';
        break;

      case STATES.READY:
        setStep(3);
        finishProgress();
        revealGame();
        notifyParent('pixreward:game_ready');
        break;

      case STATES.ERROR:
        if (progressTimer) clearTimeout(progressTimer);
        showError(opts.message || 'Não foi possível carregar o jogo.');
        notifyParent('pixreward:game_error', { error: opts.message || 'load_failed' });
        break;
    }
  }

  // ════════════════════════════════════════════════════════════════════════
  // Overlay Control
  // ════════════════════════════════════════════════════════════════════════

  function revealGame() {
    frame.classList.add('visible');
    setTimeout(function() {
      loader.classList.add('hidden');
    }, CONFIG.revealDelay);

    try { frame.focus(); } catch(e) {}
  }

  function showError(msg) {
    elSpin.style.display = 'none';
    document.getElementById('ld-steps').style.display = 'none';
    elTitle.style.display = 'none';
    elSub.style.display   = 'none';
    elBar.parentElement.style.display = 'none';

    var canRetry = retryCount < CONFIG.retryMax;
    var html =
      '<div class="ld-error">' +
        '<div class="ld-error-icon">' +
          '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">' +
            '<circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/>' +
          '</svg>' +
        '</div>' +
        '<h3>Falha no carregamento</h3>' +
        '<p>' + msg + '</p>' +
        (canRetry
          ? '<button class="ld-retry" id="btn-retry">Tentar novamente</button>'
          : '<button class="ld-retry" onclick="location.reload()">Recarregar página</button>') +
      '</div>';

    var el = document.createElement('div');
    el.innerHTML = html;
    loader.appendChild(el.firstChild);

    if (canRetry) {
      var btn = document.getElementById('btn-retry');
      if (btn) btn.addEventListener('click', retryLoad);
    }
  }

  // ════════════════════════════════════════════════════════════════════════
  // Retry Logic
  // ════════════════════════════════════════════════════════════════════════

  function retryLoad() {
    retryCount++;
    state     = STATES.IDLE;
    loadFired = false;

    var errEl = loader.querySelector('.ld-error');
    if (errEl) errEl.remove();

    elSpin.style.display = '';
    document.getElementById('ld-steps').style.display = '';
    elTitle.style.display = '';
    elSub.style.display   = '';
    elBar.parentElement.style.display = '';

    loader.classList.remove('hidden');
    frame.classList.remove('visible');

    beginLoad();
  }

  // ════════════════════════════════════════════════════════════════════════
  // postMessage — Communication with React parent
  // ════════════════════════════════════════════════════════════════════════

  function notifyParent(type, extra) {
    if (window.parent === window) return;
    var msg = {
      type:    type,
      url:     CONFIG.rawUrl,
      title:   CONFIG.title,
      proxied: CONFIG.proxied
    };
    if (extra) {
      for (var k in extra) {
        if (extra.hasOwnProperty(k)) msg[k] = extra[k];
      }
    }
    try { window.parent.postMessage(msg, '*'); } catch(e) {}
  }

  window.addEventListener('message', function(ev) {
    try {
      var d = (typeof ev.data === 'string') ? JSON.parse(ev.data) : ev.data;
      if (d && d.type === 'pixreward:close_game') {
        notifyParent('pixreward:game_closed');
      }
    } catch(e) {}
  });

  // ════════════════════════════════════════════════════════════════════════
  // Audio Context Manager
  // Many games create AudioContext but browsers suspend it until user
  // interaction. This ensures audio resumes after visibility changes.
  // ════════════════════════════════════════════════════════════════════════

  (function() {
    var resumeAudio = function() {
      try {
        var ctx = frame.contentWindow && frame.contentWindow.AudioContext;
        if (!ctx) return;
        var instances = frame.contentWindow._pixAudioContexts || [];
        instances.forEach(function(ac) {
          if (ac.state === 'suspended') ac.resume();
        });
      } catch(e) {}
    };

    document.addEventListener('visibilitychange', function() {
      if (!document.hidden) resumeAudio();
    });

    document.addEventListener('touchstart', resumeAudio, { once: true, passive: true });
    document.addEventListener('click', resumeAudio, { once: true });
  })();

  // ════════════════════════════════════════════════════════════════════════
  // Touch & Pointer Optimization
  // Prevents body scroll while allowing the game iframe to handle
  // all touch interactions natively
  // ════════════════════════════════════════════════════════════════════════

  (function() {
    var passive = { passive: false };

    document.body.addEventListener('touchmove', function(e) {
      if (e.target === frame || frame.contains(e.target)) return;
      e.preventDefault();
    }, passive);

    document.body.addEventListener('gesturestart', function(e) {
      e.preventDefault();
    }, passive);

    document.body.addEventListener('gesturechange', function(e) {
      e.preventDefault();
    }, passive);
  })();

  // ════════════════════════════════════════════════════════════════════════
  // Keyboard Passthrough
  // Ensure keyboard events reach the game iframe for games that need it
  // ════════════════════════════════════════════════════════════════════════

  document.addEventListener('keydown', function(e) {
    try {
      if (document.activeElement !== frame) frame.focus();
    } catch(err) {}
  });

  // ════════════════════════════════════════════════════════════════════════
  // Memory Pressure Monitor
  // Detects low-memory situations and notifies for potential cleanup
  // ════════════════════════════════════════════════════════════════════════

  if (navigator.deviceMemory !== undefined || (performance && performance.memory)) {
    setInterval(function() {
      try {
        if (performance.memory) {
          var used  = performance.memory.usedJSHeapSize;
          var limit = performance.memory.jsHeapSizeLimit;
          if (used / limit > 0.9) {
            console.warn('[GameEngine] High memory usage: ' + Math.round(used / 1048576) + 'MB / ' + Math.round(limit / 1048576) + 'MB');
          }
        }
      } catch(e) {}
    }, 30000);
  }

  // ════════════════════════════════════════════════════════════════════════
  // Iframe Lifecycle & Load Detection
  // ════════════════════════════════════════════════════════════════════════

  frame.addEventListener('load', function() {
    if (loadFired) return;
    loadFired = true;

    transition(STATES.RENDERING);

    injectIframeEnhancements();

    setTimeout(function() {
      transition(STATES.READY);
    }, CONFIG.revealDelay);
  });

  frame.addEventListener('error', function() {
    transition(STATES.ERROR, {
      message: 'O servidor do jogo não respondeu. Verifique sua conexão.'
    });
  });

  // ════════════════════════════════════════════════════════════════════════
  // Iframe Post-Load Enhancements
  // Injected after game loads to optimize rendering inside the game
  // ════════════════════════════════════════════════════════════════════════

  function injectIframeEnhancements() {
    try {
      var win = frame.contentWindow;
      var doc = frame.contentDocument;
      if (!win || !doc) return;

      var style = doc.createElement('style');
      style.textContent =
        'html,body{' +
          'overscroll-behavior:none!important;' +
          '-webkit-overflow-scrolling:touch;' +
          'overflow:hidden!important;' +
          'width:100%!important;height:100%!important;' +
          'margin:0!important;padding:0!important;' +
        '}' +
        'canvas{' +
          'touch-action:none;' +
          'image-rendering:auto;' +
          '-webkit-backface-visibility:hidden;' +
          'backface-visibility:hidden;' +
        '}';
      doc.head.appendChild(style);

      try {
        if (win.Element && win.Element.prototype.requestFullscreen) {
          win.Element.prototype.requestFullscreen = function() {
            return Promise.reject(new DOMException('Fullscreen blocked', 'NotAllowedError'));
          };
        }
        if (win.Element && win.Element.prototype.webkitRequestFullscreen) {
          win.Element.prototype.webkitRequestFullscreen = function() {
            return Promise.reject(new DOMException('Fullscreen blocked', 'NotAllowedError'));
          };
        }
      } catch(e) {}

      try {
        var origOpen = win.open;
        win.open = function(url) {
          try {
            var p = new URL(url, win.location.href);
            if (p.origin === win.location.origin) return origOpen.apply(win, arguments);
          } catch(e) {}
          return null;
        };
      } catch(e) {}

      try {
        var _audioCtx = win.AudioContext || win.webkitAudioContext;
        if (_audioCtx) {
          win._pixAudioContexts = [];
          var _origAudioCtx = _audioCtx;
          var wrappedCtx = function() {
            var ctx = new _origAudioCtx();
            win._pixAudioContexts.push(ctx);
            return ctx;
          };
          wrappedCtx.prototype = _origAudioCtx.prototype;
          try {
            win.AudioContext = wrappedCtx;
            if (win.webkitAudioContext) win.webkitAudioContext = wrappedCtx;
          } catch(e) {}
        }
      } catch(e) {}

    } catch(e) {
      /* cross-origin — enhancements skipped silently */
    }
  }

  // ════════════════════════════════════════════════════════════════════════
  // Safety Timeout
  // If iframe doesn't fire load in N seconds, reveal anyway
  // ════════════════════════════════════════════════════════════════════════

  var safetyTimeout = setTimeout(function() {
    if (state !== STATES.READY && state !== STATES.ERROR) {
      console.warn('[GameEngine] Timeout ' + (CONFIG.timeout / 1000) + 's — forcing reveal');
      loadFired = true;
      transition(STATES.READY);
    }
  }, CONFIG.timeout);

  // ════════════════════════════════════════════════════════════════════════
  // Begin Loading
  // ════════════════════════════════════════════════════════════════════════

  function beginLoad() {
    transition(STATES.CONNECTING);

    setTimeout(function() {
      transition(STATES.DOWNLOADING);
      frame.src = CONFIG.embedUrl;
    }, 120);

    safetyTimeout = setTimeout(function() {
      if (state !== STATES.READY && state !== STATES.ERROR) {
        console.warn('[GameEngine] Timeout ' + (CONFIG.timeout / 1000) + 's — forcing reveal');
        loadFired = true;
        transition(STATES.READY);
      }
    }, CONFIG.timeout);
  }

  // ════════════════════════════════════════════════════════════════════════
  // Visibility Change Handler
  // Pauses/resumes game awareness when tab/app becomes hidden/visible
  // ════════════════════════════════════════════════════════════════════════

  document.addEventListener('visibilitychange', function() {
    if (state !== STATES.READY) return;
    try {
      var win = frame.contentWindow;
      if (!win) return;

      if (document.hidden) {
        if (typeof win._pixOnHide === 'function') win._pixOnHide();
      } else {
        if (typeof win._pixOnShow === 'function') win._pixOnShow();
        frame.focus();
      }
    } catch(e) {}
  });

  // ════════════════════════════════════════════════════════════════════════
  // Cleanup on page unload
  // ════════════════════════════════════════════════════════════════════════

  window.addEventListener('unload', function() {
    if (progressTimer) clearTimeout(progressTimer);
    if (safetyTimeout) clearTimeout(safetyTimeout);
    try { frame.src = 'about:blank'; } catch(e) {}
  });

  // ════════════════════════════════════════════════════════════════════════
  // Start
  // ════════════════════════════════════════════════════════════════════════

  beginLoad();

})();
</script>
</body>
</html>
