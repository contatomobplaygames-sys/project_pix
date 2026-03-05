<?php
/**
 * Update Guest Profile API
 * Atualiza nome e email de um guest.
 * Requer autenticação via X-Device-Fingerprint.
 */

require_once __DIR__ . '/config.php';

bootstrapEndpoint();

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    jsonResponse(false, 'Método não permitido. Use POST.', [], 405);
}

try {
    $conn = getDBConnection();

    $input = json_decode(file_get_contents('php://input'), true);
    $guestId = isset($input['guest_id']) ? (int) $input['guest_id'] : 0;

    if ($guestId <= 0) {
        jsonResponse(false, 'guest_id inválido', [], 400);
    }

    // Autenticação
    requireGuestAuth($guestId, $input['device_fingerprint'] ?? null);

    $displayName = isset($input['display_name']) ? sanitizeInput($input['display_name']) : null;
    $name        = isset($input['name'])         ? sanitizeInput($input['name'])         : null;
    $email       = isset($input['email'])        ? sanitizeInput($input['email'])        : null;
    $finalName   = $displayName ?? $name;

    if ($finalName === null && $email === null) {
        jsonResponse(false, 'Nenhum campo para atualizar fornecido', [], 400);
    }

    // Verificar se guest existe
    $checkStmt = $conn->prepare("SELECT guest_id FROM " . DB_PREFIX . "guest_users WHERE guest_id = ? AND status = 'active' LIMIT 1");
    $checkStmt->execute([$guestId]);
    if (!$checkStmt->fetch()) {
        jsonResponse(false, 'Guest não encontrado ou inativo', [], 404);
    }

    $updateFields = [];
    $updateValues = [];

    if ($finalName !== null && $finalName !== '') {
        $updateFields[] = 'guest_name = ?';
        $updateValues[] = $finalName;
    }

    if ($email !== null && $email !== '') {
        if (!filter_var($email, FILTER_VALIDATE_EMAIL)) {
            jsonResponse(false, 'Email inválido', [], 400);
        }
        $updateFields[] = 'email = ?';
        $updateValues[] = $email;
    }

    if (empty($updateFields)) {
        jsonResponse(false, 'Nenhum campo válido para atualizar', [], 400);
    }

    $updateFields[] = 'last_access = NOW()';
    $updateValues[] = $guestId;

    $conn->prepare(
        "UPDATE " . DB_PREFIX . "guest_users SET " . implode(', ', $updateFields) . " WHERE guest_id = ? AND status = 'active'"
    )->execute($updateValues);

    // Buscar dados atualizados
    $fetchStmt = $conn->prepare("
        SELECT g.guest_id, g.guest_public_id, g.guest_name, g.email, g.chavepix, g.pix_key_type,
               s.points, s.lifetime_points, s.level
        FROM " . DB_PREFIX . "guest_users g
        LEFT JOIN " . DB_PREFIX . "guest_scores s ON g.guest_id = s.guest_id
        WHERE g.guest_id = ? AND g.status = 'active' LIMIT 1
    ");
    $fetchStmt->execute([$guestId]);
    $updated = $fetchStmt->fetch();

    if (!$updated) {
        jsonResponse(false, 'Erro ao buscar dados atualizados', [], 500);
    }

    jsonResponse(true, 'Perfil atualizado com sucesso', [
        'user' => [
            'guest_id'        => (int) $updated['guest_id'],
            'guest_public_id' => $updated['guest_public_id'],
            'display_name'    => $updated['guest_name'],
            'name'            => $updated['guest_name'],
            'email'           => $updated['email'],
            'pix_key'         => $updated['chavepix'],
            'pix_key_type'    => $updated['pix_key_type'],
            'points'          => (int) ($updated['points'] ?? 0),
            'lifetime_points' => (int) ($updated['lifetime_points'] ?? 0),
            'level'           => (int) ($updated['level'] ?? 1),
        ],
    ]);
} catch (PDOException $e) {
    error_log('update_guest_profile.php: ' . $e->getMessage());
    jsonResponse(false, 'Erro interno ao atualizar perfil', [], 500);
} catch (Exception $e) {
    error_log('update_guest_profile.php: ' . $e->getMessage());
    jsonResponse(false, 'Erro ao atualizar perfil', [], 500);
}
