<?php
/**
 * Configuração Central — PixReward Blitz
 *
 * Conexão com banco, helpers de resposta, CORS, autenticação de guest e
 * rate-limiting.  Todas as funções utilitárias partilhadas ficam aqui.
 *
 * NUNCA commite este arquivo no Git se contiver credenciais reais.
 */

// ═══════════════════════════════════════════════════════════════════════
// Banco de Dados
// ═══════════════════════════════════════════════════════════════════════

define('DB_HOST',    'localhost');
define('DB_NAME',    'xperia22_apppixreact');
define('DB_USER',    'xperia22_pixapprect');
define('DB_PASS',    'd15n02M03$%');
define('DB_CHARSET', 'utf8mb4');
define('DB_PREFIX',  'pixreward_');

// ═══════════════════════════════════════════════════════════════════════
// Timezone
// ═══════════════════════════════════════════════════════════════════════

date_default_timezone_set('America/Sao_Paulo');

// ═══════════════════════════════════════════════════════════════════════
// Origens permitidas (CORS)
// ═══════════════════════════════════════════════════════════════════════

define('ALLOWED_ORIGINS', [
    'https://app.mobplaygames.com.br',
    'http://localhost:3000',          // dev local
    'http://127.0.0.1:3000',
]);

/**
 * Emite os headers CORS restritos (substitui o antigo Allow-Origin: *).
 * Deve ser chamado no topo de cada endpoint.
 */
function applyCorsHeaders(): void {
    $origin = $_SERVER['HTTP_ORIGIN'] ?? '';

    if (in_array($origin, ALLOWED_ORIGINS, true)) {
        header("Access-Control-Allow-Origin: {$origin}");
    } else {
        // Para requests sem Origin (e.g. Unity WebView), permitir
        // somente se vier da mesma máquina (server-side) ou de app nativo.
        // Não emitir header — o browser bloqueará requests cross-origin.
    }

    header('Access-Control-Allow-Methods: GET, POST, OPTIONS');
    header('Access-Control-Allow-Headers: Content-Type, Accept, X-Device-Fingerprint');
    header('Access-Control-Max-Age: 3600');
    header('Vary: Origin');
}

// ═══════════════════════════════════════════════════════════════════════
// Conexão PDO
// ═══════════════════════════════════════════════════════════════════════

function getDBConnection(): PDO {
    static $conn = null;

    if ($conn === null) {
        try {
            $dsn = 'mysql:host=' . DB_HOST . ';dbname=' . DB_NAME . ';charset=' . DB_CHARSET;
            $options = [
                PDO::ATTR_ERRMODE            => PDO::ERRMODE_EXCEPTION,
                PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
                PDO::ATTR_EMULATE_PREPARES   => false,
                PDO::ATTR_PERSISTENT         => false,
            ];
            $conn = new PDO($dsn, DB_USER, DB_PASS, $options);
        } catch (PDOException $e) {
            error_log('DB connection error: ' . $e->getMessage());
            throw new Exception('Erro ao conectar com o banco de dados');
        }
    }

    return $conn;
}

// ═══════════════════════════════════════════════════════════════════════
// JSON Response
// ═══════════════════════════════════════════════════════════════════════

/**
 * Envia resposta JSON padronizada e encerra o script.
 */
function jsonResponse(bool $success, string $message, array $data = [], int $statusCode = 200): void {
    http_response_code($statusCode);
    header('Content-Type: application/json; charset=utf-8');

    echo json_encode([
        'success'   => $success,
        'message'   => $message,
        'data'      => $data,
        'timestamp' => date('Y-m-d H:i:s'),
    ], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);

    exit;
}

// ═══════════════════════════════════════════════════════════════════════
// Input helpers
// ═══════════════════════════════════════════════════════════════════════

/**
 * Sanitiza entrada textual (trim + remove tags).
 * NÃO aplica htmlspecialchars — isso é feito na saída/display, não na entrada.
 */
function sanitizeInput(string $input): string {
    return trim(strip_tags($input));
}

// ═══════════════════════════════════════════════════════════════════════
// Guest Public ID
// ═══════════════════════════════════════════════════════════════════════

function generateGuestPublicId(): string {
    return 'GUEST-'
         . strtoupper(substr(md5(uniqid((string)random_int(0, PHP_INT_MAX), true)), 0, 8))
         . '-'
         . strtoupper(substr(md5((string)time()), 0, 4));
}

// ═══════════════════════════════════════════════════════════════════════
// Autenticação de Guest  (device_fingerprint ↔ guest_id)
// ═══════════════════════════════════════════════════════════════════════

/**
 * Valida que o guest_id pertence ao device_fingerprint informado.
 *
 * COMO FUNCIONA:
 *   O frontend envia `guest_id` no body/query E o `device_fingerprint`
 *   no header `X-Device-Fingerprint` (ou no body como fallback).
 *   Aqui verificamos se o par (guest_id, device_fingerprint) existe e
 *   está ativo.  Isso impede que alguém altere apenas o guest_id para
 *   manipular outra conta.
 *
 * @param int         $guestId            O guest_id informado pelo client.
 * @param string|null $deviceFingerprint  Fingerprint enviado pelo client.
 *                                        Se null, tentará obter do header.
 * @return bool true se válido.
 */
function authenticateGuest(int $guestId, ?string $deviceFingerprint = null): bool {
    // Obter fingerprint do header (preferência) ou fallback
    if ($deviceFingerprint === null) {
        $deviceFingerprint = $_SERVER['HTTP_X_DEVICE_FINGERPRINT']
                          ?? $_POST['device_fingerprint']
                          ?? null;

        // Tentar body JSON
        if ($deviceFingerprint === null) {
            $body = json_decode(file_get_contents('php://input'), true);
            $deviceFingerprint = $body['device_fingerprint'] ?? null;
        }
    }

    if (empty($deviceFingerprint) || $guestId <= 0) {
        return false;
    }

    $conn = getDBConnection();
    $stmt = $conn->prepare(
        'SELECT 1 FROM ' . DB_PREFIX . "guest_users
         WHERE guest_id = ? AND device_fingerprint = ? AND status = 'active'
         LIMIT 1"
    );
    $stmt->execute([$guestId, $deviceFingerprint]);

    return (bool) $stmt->fetch();
}

/**
 * Wrapper que rejeita a request com 403 se a autenticação falhar.
 * Chamado no início de endpoints que alteram dados.
 */
function requireGuestAuth(int $guestId, ?string $deviceFingerprint = null): void {
    if (!authenticateGuest($guestId, $deviceFingerprint)) {
        jsonResponse(false, 'Autenticação inválida', [], 403);
    }
}

// ═══════════════════════════════════════════════════════════════════════
// Rate Limiting simples (por IP + endpoint, usando banco)
// ═══════════════════════════════════════════════════════════════════════

/**
 * Limita requisições por IP e endpoint usando tabela de rate-limit.
 *
 * Cria a tabela automaticamente se não existir (somente na primeira vez).
 *
 * @param string $endpoint  Nome lógico do endpoint (ex: 'super_bonus').
 * @param int    $maxHits   Máximo de hits permitidos no período.
 * @param int    $windowSec Janela de tempo em segundos.
 */
function rateLimit(string $endpoint, int $maxHits, int $windowSec): void {
    $ip = $_SERVER['REMOTE_ADDR'] ?? '0.0.0.0';

    try {
        $conn = getDBConnection();

        // Garantir que a tabela existe (DDL idempotente)
        $conn->exec("
            CREATE TABLE IF NOT EXISTS " . DB_PREFIX . "rate_limits (
                id         BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                ip_address VARCHAR(45)  NOT NULL,
                endpoint   VARCHAR(64)  NOT NULL,
                hit_at     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
                INDEX idx_rate (ip_address, endpoint, hit_at)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        ");

        // Limpar entradas antigas (fora da janela)
        $conn->prepare(
            'DELETE FROM ' . DB_PREFIX . 'rate_limits
             WHERE ip_address = ? AND endpoint = ? AND hit_at < DATE_SUB(NOW(), INTERVAL ? SECOND)'
        )->execute([$ip, $endpoint, $windowSec]);

        // Contar hits na janela
        $stmt = $conn->prepare(
            'SELECT COUNT(*) as hits FROM ' . DB_PREFIX . 'rate_limits
             WHERE ip_address = ? AND endpoint = ? AND hit_at >= DATE_SUB(NOW(), INTERVAL ? SECOND)'
        );
        $stmt->execute([$ip, $endpoint, $windowSec]);
        $hits = (int) $stmt->fetchColumn();

        if ($hits >= $maxHits) {
            jsonResponse(false, 'Muitas requisições. Tente novamente mais tarde.', [], 429);
        }

        // Registrar hit
        $conn->prepare(
            'INSERT INTO ' . DB_PREFIX . 'rate_limits (ip_address, endpoint) VALUES (?, ?)'
        )->execute([$ip, $endpoint]);

    } catch (PDOException $e) {
        // Se falhar (tabela não criou, etc.), logar mas não bloquear
        error_log('Rate-limit error: ' . $e->getMessage());
    }
}

// ═══════════════════════════════════════════════════════════════════════
// Helpers para preflight OPTIONS
// ═══════════════════════════════════════════════════════════════════════

/**
 * Responde ao preflight OPTIONS e encerra.
 */
function handlePreflight(): void {
    if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
        http_response_code(204);
        exit;
    }
}

/**
 * Setup padrão chamado no início de TODO endpoint:
 *   1. CORS headers
 *   2. Preflight
 */
function bootstrapEndpoint(): void {
    applyCorsHeaders();
    handlePreflight();
}
