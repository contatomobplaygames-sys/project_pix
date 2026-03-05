<?php
/**
 * Guest Authentication API
 * Endpoint para criar ou recuperar guest baseado em device fingerprint.
 *
 * Este é o ÚNICO endpoint que NÃO exige autenticação prévia (é o
 * próprio fluxo de login).
 */

require_once __DIR__ . '/config.php';

bootstrapEndpoint();

// Apenas aceitar POST
if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    jsonResponse(false, 'Método não permitido', [], 405);
}

try {
    $conn = getDBConnection();

    $action = $_POST['action'] ?? '';

    if ($action === 'create_or_get') {
        if (empty($_POST['device_fingerprint'])) {
            jsonResponse(false, 'Device fingerprint não fornecido', [], 400);
        }

        $deviceFingerprint = sanitizeInput($_POST['device_fingerprint']);
        $userAgent = sanitizeInput($_POST['user_agent'] ?? $_SERVER['HTTP_USER_AGENT'] ?? '');
        $ipAddress = $_SERVER['REMOTE_ADDR'] ?? '';

        if (strlen($deviceFingerprint) < 16 || strlen($deviceFingerprint) > 128) {
            jsonResponse(false, 'Fingerprint inválido', [], 400);
        }

        // Rate-limit: max 10 auth requests por minuto por IP
        rateLimit('guest_auth', 10, 60);

        // Buscar guest existente
        $stmt = $conn->prepare("
            SELECT guest_id, guest_public_id, guest_name, email, chavepix,
                   status, created_at, last_access
            FROM " . DB_PREFIX . "guest_users
            WHERE device_fingerprint = ? AND status = 'active'
            LIMIT 1
        ");
        $stmt->execute([$deviceFingerprint]);
        $guest = $stmt->fetch();

        if ($guest) {
            // Atualizar último acesso
            $conn->prepare("UPDATE " . DB_PREFIX . "guest_users SET last_access = NOW() WHERE guest_id = ?")
                 ->execute([$guest['guest_id']]);

            // Buscar pontos
            $scoreStmt = $conn->prepare("SELECT points, lifetime_points, level FROM " . DB_PREFIX . "guest_scores WHERE guest_id = ?");
            $scoreStmt->execute([$guest['guest_id']]);
            $score = $scoreStmt->fetch();

            jsonResponse(true, 'Guest recuperado', [
                'guest_id'        => (int) $guest['guest_id'],
                'guest_public_id' => $guest['guest_public_id'],
                'guest_name'      => $guest['guest_name'],
                'email'           => $guest['email'],
                'chavepix'        => $guest['chavepix'],
                'user_score'      => (int) ($score['points'] ?? 0),
                'points'          => (int) ($score['points'] ?? 0),
                'lifetime_points' => (int) ($score['lifetime_points'] ?? 0),
                'level'           => (int) ($score['level'] ?? 1),
                'is_existing'     => true,
                'created_at'      => $guest['created_at'],
            ]);
        } else {
            // Criar novo guest
            $guestPublicId = generateGuestPublicId();

            $conn->prepare("
                INSERT INTO " . DB_PREFIX . "guest_users
                (device_fingerprint, guest_public_id, guest_name, user_agent, ip_address, status)
                VALUES (?, ?, 'Visitante', ?, ?, 'active')
            ")->execute([$deviceFingerprint, $guestPublicId, $userAgent, $ipAddress]);

            $newGuestId = $conn->lastInsertId();

            $conn->prepare("
                INSERT INTO " . DB_PREFIX . "guest_scores (guest_id, points, lifetime_points, level)
                VALUES (?, 0, 0, 1)
                ON DUPLICATE KEY UPDATE guest_id = guest_id
            ")->execute([$newGuestId]);

            jsonResponse(true, 'Guest criado com sucesso', [
                'guest_id'        => (int) $newGuestId,
                'guest_public_id' => $guestPublicId,
                'guest_name'      => 'Visitante',
                'email'           => null,
                'chavepix'        => null,
                'user_score'      => 0,
                'points'          => 0,
                'lifetime_points' => 0,
                'level'           => 1,
                'is_existing'     => false,
                'created_at'      => date('Y-m-d H:i:s'),
            ]);
        }
    } else {
        jsonResponse(false, 'Ação não reconhecida', [], 400);
    }
} catch (PDOException $e) {
    error_log('guest_auth.php: ' . $e->getMessage());
    jsonResponse(false, 'Erro interno ao processar requisição', [], 500);
} catch (Exception $e) {
    error_log('guest_auth.php: ' . $e->getMessage());
    jsonResponse(false, 'Erro ao processar requisição', [], 500);
}
