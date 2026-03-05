<?php
/**
 * Add Super Bonus Points Endpoint
 * Adiciona pontos do Super Bônus.
 * Requer autenticação via X-Device-Fingerprint.
 * Rate-limited: max 5 por minuto.
 */

require_once __DIR__ . '/config.php';

bootstrapEndpoint();

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    jsonResponse(false, 'Método não permitido. Use POST.', [], 405);
}

try {
    $rawInput = file_get_contents('php://input');
    $input    = json_decode($rawInput, true);

    if (!$input) {
        jsonResponse(false, 'JSON inválido', [], 400);
    }

    $guestId = isset($input['guest_id']) ? (int) $input['guest_id'] : 0;
    $points  = isset($input['points'])   ? (int) $input['points']   : 1;

    if ($guestId <= 0) {
        jsonResponse(false, 'guest_id inválido', [], 400);
    }
    if ($points <= 0 || $points > 5) {
        // Cap máximo de pontos por request para evitar abuso
        jsonResponse(false, 'Quantidade de pontos inválida (1-5)', [], 400);
    }

    // Autenticação
    requireGuestAuth($guestId, $input['device_fingerprint'] ?? null);

    // Rate-limit: max 5 super bonus por minuto por IP
    rateLimit('super_bonus', 5, 60);

    $conn = getDBConnection();
    $conn->beginTransaction();

    try {
        // Verificar guest e travar linha
        $stmt = $conn->prepare("
            SELECT g.guest_id, g.status,
                   COALESCE(s.points, 0) as points,
                   COALESCE(s.lifetime_points, 0) as lifetime_points
            FROM " . DB_PREFIX . "guest_users g
            LEFT JOIN " . DB_PREFIX . "guest_scores s ON g.guest_id = s.guest_id
            WHERE g.guest_id = ?
            FOR UPDATE
        ");
        $stmt->execute([$guestId]);
        $guest = $stmt->fetch();

        if (!$guest) {
            throw new Exception('Guest não encontrado');
        }
        if ($guest['status'] !== 'active') {
            throw new Exception('Conta de guest inativa');
        }

        $currentPoints = (int) $guest['points'];
        $newTotal      = $currentPoints + $points;
        $newLifetime   = (int) $guest['lifetime_points'] + $points;

        // Verificar se scores existe
        $scoreCheck = $conn->prepare("SELECT row_id FROM " . DB_PREFIX . "guest_scores WHERE guest_id = ?");
        $scoreCheck->execute([$guestId]);

        if ($scoreCheck->fetch()) {
            $conn->prepare("
                UPDATE " . DB_PREFIX . "guest_scores
                SET points = ?, lifetime_points = ?, updated_at = NOW()
                WHERE guest_id = ?
            ")->execute([$newTotal, $newLifetime, $guestId]);
        } else {
            $conn->prepare("
                INSERT INTO " . DB_PREFIX . "guest_scores (guest_id, points, lifetime_points, level, updated_at)
                VALUES (?, ?, ?, 1, NOW())
            ")->execute([$guestId, $newTotal, $newLifetime]);
        }

        // Atualizar last_access
        $conn->prepare("UPDATE " . DB_PREFIX . "guest_users SET last_access = NOW() WHERE guest_id = ?")->execute([$guestId]);

        // Criar transação
        $description = isset($input['description']) ? trim($input['description']) : 'Super Bônus';
        $source      = isset($input['source'])      ? trim($input['source'])      : 'react';

        $conn->prepare("
            INSERT INTO " . DB_PREFIX . "guest_transactions
            (guest_id, type, amount, points_before, points_after, description, source, status, created_at)
            VALUES (?, 'EARN', ?, ?, ?, ?, ?, 'COMPLETED', NOW())
        ")->execute([$guestId, $points, $currentPoints, $newTotal, $description, $source]);

        $transactionId = $conn->lastInsertId();

        $conn->commit();

        // Resposta — manter formato que o frontend espera (status/message)
        http_response_code(200);
        header('Content-Type: application/json; charset=utf-8');
        echo json_encode([
            'status'         => 'success',
            'message'        => 'Pontos adicionados',
            'transaction_id' => $transactionId,
            'points_added'   => $points,
            'new_total'      => $newTotal,
            'total_points'   => $newTotal,
            'guest_id'       => $guestId,
        ], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
        exit;

    } catch (Exception $e) {
        $conn->rollBack();
        throw $e;
    }
} catch (PDOException $e) {
    error_log('add_super_bonus_points.php: ' . $e->getMessage());
    http_response_code(500);
    echo json_encode(['status' => 'error', 'message' => 'Erro interno'], JSON_UNESCAPED_UNICODE);
    exit;
} catch (Exception $e) {
    error_log('add_super_bonus_points.php: ' . $e->getMessage());
    http_response_code(400);
    echo json_encode(['status' => 'error', 'message' => $e->getMessage()], JSON_UNESCAPED_UNICODE);
    exit;
}
