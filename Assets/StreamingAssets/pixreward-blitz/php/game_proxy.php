<?php
/**
 * Game Proxy v8 — PixReward Blitz
 *
 * Fetches remote HTML5 game content, injects multi-layer anti-frame-busting
 * protection, and serves clean content from our domain.
 *
 * Protection layers:
 *   Layer 0 — Fullscreen API blocking
 *   Layer 1 — eval() / new Function() intercept (sanitizes frame-busting code)
 *   Layer 2 — top/parent/self location override
 *   Layer 3 — Navigation API interception (window.open, popups)
 *   Layer 4 — Global error handler (suppresses SecurityError)
 *   Layer 5 — MutationObserver (removes injected redirect scripts)
 *
 * Usage:
 *   game_proxy.php?url=https://games.cdn.famobi.com/html5games/...
 */

declare(strict_types=1);

// ═══════════════════════════════════════════════════════════════════════════
// Configuration
// ═══════════════════════════════════════════════════════════════════════════

$allowedDomains = [
    'cdn-factory.marketjs.com',
    'games.cdn.famobi.com',
    'play.famobi.com',
    'html5.gamedistribution.com',
    'html5.gamemonetize.com',
    'cdn.wanted5games.com',
    'storage.y8.com',
    'y8.com',
    'www.y8.com',
    'playcutegames.com',
    'www.construct.net',
    'preview.construct.net',
    'html-classic.itch.zone',
];

$cacheTtlSeconds = 900;
$cacheDir        = __DIR__ . '/game_cache';
$cacheVersion    = 'v8';

// ═══════════════════════════════════════════════════════════════════════════
// Helpers
// ═══════════════════════════════════════════════════════════════════════════

function fail(int $code, string $msg): void
{
    http_response_code($code);
    header('Content-Type: text/plain; charset=utf-8');
    echo $msg;
    exit;
}

function isDomainAllowed(string $host, array $list): bool
{
    $host = strtolower($host);
    foreach ($list as $d) {
        $d = strtolower($d);
        if ($host === $d || str_ends_with($host, '.' . $d)) {
            return true;
        }
    }
    return false;
}

// ═══════════════════════════════════════════════════════════════════════════
// Input Validation
// ═══════════════════════════════════════════════════════════════════════════

$gameUrl = trim((string) ($_GET['url'] ?? ''));
if ($gameUrl === '')                            fail(400, 'Parâmetro "url" é obrigatório.');
if (!filter_var($gameUrl, FILTER_VALIDATE_URL)) fail(400, 'URL inválida.');

$parts = parse_url($gameUrl);
if (!$parts || empty($parts['scheme']) || empty($parts['host'])) fail(400, 'URL malformada.');

$scheme = strtolower((string) $parts['scheme']);
if (!in_array($scheme, ['http', 'https'], true)) fail(400, 'Apenas http/https permitidos.');

$host = (string) $parts['host'];
if (!isDomainAllowed($host, $allowedDomains)) fail(403, 'Domínio não permitido: ' . $host);

// ═══════════════════════════════════════════════════════════════════════════
// Cache
// ═══════════════════════════════════════════════════════════════════════════

$useCache  = !isset($_GET['nocache']);
$cacheKey  = md5($cacheVersion . '|' . $gameUrl);
$cacheFile = $cacheDir . '/' . $cacheKey . '.html';

if ($useCache && is_file($cacheFile) && (time() - (int) filemtime($cacheFile) < $cacheTtlSeconds)) {
    header('Content-Type: text/html; charset=utf-8');
    header('X-Game-Proxy: cached-v8');
    header('Cache-Control: no-store');
    readfile($cacheFile);
    exit;
}

// ═══════════════════════════════════════════════════════════════════════════
// Remote Fetch
// ═══════════════════════════════════════════════════════════════════════════

$ctx = stream_context_create([
    'http' => [
        'method'          => 'GET',
        'timeout'         => 25,
        'follow_location' => true,
        'max_redirects'   => 8,
        'header'          => implode("\r\n", [
            'User-Agent: Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Mobile Safari/537.36',
            'Accept: text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8',
            'Accept-Language: pt-BR,pt;q=0.9,en-US;q=0.8,en;q=0.7',
            'Accept-Encoding: identity',
        ]),
    ],
    'ssl' => ['verify_peer' => true, 'verify_peer_name' => true],
]);

$html = @file_get_contents($gameUrl, false, $ctx);
if ($html === false || trim($html) === '') {
    fail(502, 'Falha ao carregar jogo remoto.');
}

// ═══════════════════════════════════════════════════════════════════════════
// Build base href for relative resources
// ═══════════════════════════════════════════════════════════════════════════

$origin  = $scheme . '://' . $host . (isset($parts['port']) ? ':' . $parts['port'] : '');
$dirPath = rtrim(str_replace('\\', '/', dirname((string) ($parts['path'] ?? '/'))), '/');
$baseHref = $origin . ($dirPath !== '' ? $dirPath . '/' : '/');

// ═══════════════════════════════════════════════════════════════════════════
// Ensure <head> exists
// ═══════════════════════════════════════════════════════════════════════════

if (stripos($html, '<head') === false) {
    $html = '<html><head></head><body>' . $html . '</body></html>';
}

// ═══════════════════════════════════════════════════════════════════════════
// Remove existing X-Frame-Options / CSP frame-ancestors from meta tags
// ═══════════════════════════════════════════════════════════════════════════

$html = preg_replace(
    '/<meta[^>]*(?:X-Frame-Options|frame-ancestors)[^>]*>/i',
    '',
    $html
) ?? $html;

// ═══════════════════════════════════════════════════════════════════════════
// Build injection payload
// ═══════════════════════════════════════════════════════════════════════════

$safeOriginJs = json_encode($origin, JSON_UNESCAPED_SLASHES);

$script = <<<'JSBLOCK'
<script>
(function() {
  'use strict';

  // ── LAYER 0: Fullscreen API Blocking ──────────────────────────────────
  try {
    var noFs = function() {
      return Promise.reject(new DOMException('Fullscreen blocked', 'NotAllowedError'));
    };
    var fsTargets = ['requestFullscreen','webkitRequestFullscreen','webkitRequestFullScreen','mozRequestFullScreen','msRequestFullscreen'];
    fsTargets.forEach(function(m) {
      try { if (Element.prototype[m]) Element.prototype[m] = noFs; } catch(e) {}
      try { if (HTMLElement.prototype[m]) HTMLElement.prototype[m] = noFs; } catch(e) {}
    });
    ['fullscreenEnabled','webkitFullscreenEnabled','mozFullScreenEnabled','msFullscreenEnabled'].forEach(function(p) {
      try { Object.defineProperty(document, p, { value: false, writable: false, configurable: true }); } catch(e) {}
    });
  } catch(e) {}

  // ── LAYER 1: eval() & new Function() Intercept ────────────────────────
  // Games using frame-busting via eval/Function get their navigation code
  // neutralized before execution.

  var NAV_PATTERNS = [
    /top\s*\.\s*location\s*\.\s*replace\s*\([^)]*\)/g,
    /top\s*\.\s*location\s*\.\s*assign\s*\([^)]*\)/g,
    /top\s*\.\s*location\s*\.\s*href\s*=[^;,}\)\]]+/g,
    /top\s*\.\s*location\s*=[^;,}\)\]]+/g,
    /parent\s*\.\s*location\s*\.\s*replace\s*\([^)]*\)/g,
    /parent\s*\.\s*location\s*\.\s*assign\s*\([^)]*\)/g,
    /parent\s*\.\s*location\s*\.\s*href\s*=[^;,}\)\]]+/g,
    /parent\s*\.\s*location\s*=[^;,}\)\]]+/g,
    /window\s*\.\s*top\s*\.\s*location\s*\.\s*replace\s*\([^)]*\)/g,
    /window\s*\.\s*top\s*\.\s*location\s*\.\s*assign\s*\([^)]*\)/g,
    /window\s*\.\s*top\s*\.\s*location\s*\.\s*href\s*=[^;,}\)\]]+/g,
    /window\s*\.\s*top\s*\.\s*location\s*=[^;,}\)\]]+/g,
    /self\s*\.\s*location\s*\.\s*replace\s*\([^)]*\)/g,
    /self\s*\.\s*location\s*\.\s*assign\s*\([^)]*\)/g
  ];

  function sanitize(code) {
    if (typeof code !== 'string') return code;
    for (var i = 0; i < NAV_PATTERNS.length; i++) {
      code = code.replace(NAV_PATTERNS[i], '(void 0)');
    }
    return code;
  }

  try {
    var _eval = window.eval;
    window.eval = function(code) {
      return _eval.call(window, sanitize(code));
    };
  } catch(e) {}

  try {
    var _Function = window.Function;
    window.Function = function() {
      var args = Array.prototype.slice.call(arguments);
      if (args.length > 0) {
        var body = args[args.length - 1];
        if (typeof body === 'string' &&
            (body.indexOf('top.location') !== -1 ||
             body.indexOf('parent.location') !== -1 ||
             body.indexOf('self.location') !== -1)) {
          args[args.length - 1] = sanitize(body);
        }
      }
      return _Function.apply(this, args);
    };
    window.Function.prototype = _Function.prototype;
  } catch(e) {}

  // ── LAYER 2: Location Override ────────────────────────────────────────
  // Override navigation methods on top/parent/self to prevent redirects

  function noop() {}
  var GAME_ORIGIN = %GAME_ORIGIN%;

  // top & parent
  ['top', 'parent'].forEach(function(target) {
    try {
      var ref = window[target];
      if (ref && ref !== window.self) {
        try { ref.location.replace = noop; } catch(e) {}
        try { ref.location.assign  = noop; } catch(e) {}
      }
    } catch(e) {}
  });

  // self — allow same-origin navigation, block external
  try {
    var _selfReplace = window.location.replace.bind(window.location);
    var _selfAssign  = window.location.assign.bind(window.location);

    window.location.replace = function(url) {
      try {
        var p = new URL(url, window.location.href);
        if (p.origin === window.location.origin || p.origin === GAME_ORIGIN) return _selfReplace(url);
      } catch(e) {}
    };
    window.location.assign = function(url) {
      try {
        var p = new URL(url, window.location.href);
        if (p.origin === window.location.origin || p.origin === GAME_ORIGIN) return _selfAssign(url);
      } catch(e) {}
    };
  } catch(e) {}

  // ── LAYER 3: Navigation API Intercept ─────────────────────────────────
  // Block window.open to external URLs, suppress alert/confirm/prompt

  try {
    var _open = window.open;
    window.open = function(url) {
      if (url && typeof url === 'string') {
        try {
          var p = new URL(url, window.location.href);
          if (p.origin === window.location.origin || p.origin === GAME_ORIGIN) {
            return _open.apply(window, arguments);
          }
        } catch(e) {}
      }
      return null;
    };
  } catch(e) {}

  window.alert   = function() {};
  window.confirm = function() { return true; };
  window.prompt  = function() { return null; };

  // ── LAYER 4: Global Error Handler ─────────────────────────────────────
  // Suppress security-related errors that would crash the game runtime

  window.addEventListener('error', function(ev) {
    var m = String(ev && ev.message || '');
    if (m.indexOf('SecurityError') !== -1 ||
        m.indexOf('cross-origin') !== -1 ||
        m.indexOf('Location') !== -1 ||
        m.indexOf('Blocked') !== -1 ||
        m.indexOf('frame') !== -1 ||
        m.indexOf('permission') !== -1 ||
        m.indexOf('sandboxed') !== -1) {
      ev.preventDefault();
      ev.stopImmediatePropagation();
      return true;
    }
  }, true);

  window.addEventListener('unhandledrejection', function(ev) {
    var m = String(ev && ev.reason || '');
    if (m.indexOf('SecurityError') !== -1 ||
        m.indexOf('Location') !== -1 ||
        m.indexOf('Fullscreen') !== -1 ||
        m.indexOf('NotAllowedError') !== -1) {
      ev.preventDefault();
    }
  });

  // ── LAYER 5: MutationObserver ─────────────────────────────────────────
  // Watch for dynamically injected scripts that try frame-busting and
  // neutralize them before they execute

  try {
    var observer = new MutationObserver(function(mutations) {
      mutations.forEach(function(mut) {
        mut.addedNodes.forEach(function(node) {
          if (node.nodeName === 'SCRIPT' && node.textContent) {
            var src = node.textContent;
            if (src.indexOf('top.location') !== -1 ||
                src.indexOf('parent.location') !== -1 ||
                src.indexOf('window.top.location') !== -1) {
              node.textContent = sanitize(src);
            }
          }
        });
      });
    });
    observer.observe(document.documentElement, { childList: true, subtree: true });
  } catch(e) {}

  // ── Performance: Optimize Canvas Rendering ────────────────────────────
  // Request high-performance GPU for WebGL games

  try {
    var _getContext = HTMLCanvasElement.prototype.getContext;
    HTMLCanvasElement.prototype.getContext = function(type, attrs) {
      if (type === 'webgl' || type === 'webgl2' || type === 'experimental-webgl') {
        attrs = attrs || {};
        attrs.powerPreference = attrs.powerPreference || 'high-performance';
        attrs.antialias = attrs.antialias !== undefined ? attrs.antialias : false;
        attrs.preserveDrawingBuffer = attrs.preserveDrawingBuffer || false;
        attrs.desynchronized = true;
      }
      return _getContext.call(this, type, attrs);
    };
  } catch(e) {}

  // ── Performance: Overscroll Prevention ─────────────────────────────────

  try {
    document.documentElement.style.overscrollBehavior = 'none';
    document.body.style.overscrollBehavior = 'none';
    document.body.style.overflow = 'hidden';
    document.documentElement.style.overflow = 'hidden';
  } catch(e) {}

})();
</script>
JSBLOCK;

// Replace placeholder with actual game origin
$script = str_replace('%GAME_ORIGIN%', $safeOriginJs, $script);

$baseTag    = '<base href="' . htmlspecialchars($baseHref, ENT_QUOTES, 'UTF-8') . '">';
$injections = $baseTag . $script;
$html = preg_replace('/<head([^>]*)>/i', '<head$1>' . $injections, $html, 1) ?? $html;

// ═══════════════════════════════════════════════════════════════════════════
// Write Cache
// ═══════════════════════════════════════════════════════════════════════════

if (!is_dir($cacheDir)) {
    @mkdir($cacheDir, 0755, true);
}
@file_put_contents($cacheFile, $html);

// ═══════════════════════════════════════════════════════════════════════════
// Response
// ═══════════════════════════════════════════════════════════════════════════

header('Content-Type: text/html; charset=utf-8');
header('X-Game-Proxy: live-v8');
header('Cache-Control: no-store');
header_remove('X-Frame-Options');
echo $html;
