<?php
/**
 * Get User Profile API
 * Retorna o perfil completo de um guest.
 * Requer autenticação via X-Device-Fingerprint.
 */

require_once __DIR__ . '/config.php';

bootstrapEndpoint();

try {
    $conn = getDBConnection();

    $guestId = isset($_GET['guest_id']) ? (int) $_GET['guest_id'] : 0;

    if ($guestId <= 0) {
        jsonResponse(false, 'guest_id inválido', [], 400);
    }

    // Autenticação: verificar que device_fingerprint pertence a este guest
    requireGuestAuth($guestId);

    $stmt = $conn->prepare("
        SELECT
            g.guest_id, g.guest_public_id, g.guest_name, g.email,
            g.chavepix, g.pix_key_type, g.status, g.created_at, g.last_access,
            s.points, s.lifetime_points, s.level
        FROM " . DB_PREFIX . "guest_users g
        LEFT JOIN " . DB_PREFIX . "guest_scores s ON g.guest_id = s.guest_id
        WHERE g.guest_id = ? AND g.status = 'active'
        LIMIT 1
    ");
    $stmt->execute([$guestId]);
    $user = $stmt->fetch();

    if (!$user) {
        jsonResponse(false, 'Guest não encontrado', [], 404);
    }

    jsonResponse(true, 'Perfil carregado com sucesso', [
        'user' => [
            'guest_id'        => (int) $user['guest_id'],
            'guest_public_id' => $user['guest_public_id'],
            'display_name'    => $user['guest_name'],
            'name'            => $user['guest_name'],
            'email'           => $user['email'],
            'pix_key'         => $user['chavepix'],
            'pix_key_type'    => $user['pix_key_type'],
            'points'          => (int) ($user['points'] ?? 0),
            'lifetime_points' => (int) ($user['lifetime_points'] ?? 0),
            'level'           => (int) ($user['level'] ?? 1),
            'status'          => $user['status'],
            'created_at'      => $user['created_at'],
            'last_access'     => $user['last_access'],
        ],
    ]);
} catch (PDOException $e) {
    error_log('get_user_profile.php: ' . $e->getMessage());
    jsonResponse(false, 'Erro interno ao buscar perfil', [], 500);
} catch (Exception $e) {
    error_log('get_user_profile.php: ' . $e->getMessage());
    jsonResponse(false, 'Erro ao buscar perfil', [], 500);
}
