<?php
/**
 * Unified Score Submission Endpoint
 *
 * Persiste pontos de guests no banco de dados.
 * Suporta idempotência via client_tx_id (UUID) para evitar duplicatas em retries.
 *
 * Autenticação: X-Device-Fingerprint header.
 * Rate-limit: max 30 por minuto por IP.
 */

require_once __DIR__ . '/config.php';

bootstrapEndpoint();

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    jsonResponse(false, 'Método não permitido. Use POST.', [], 405);
}

rateLimit('submit_score', 30, 60);

try {
    $rawInput = file_get_contents('php://input');
    $input    = json_decode($rawInput, true);

    if (!$input) {
        jsonResponse(false, 'JSON inválido', [], 400);
    }

    $points     = isset($input['points'])      ? (int) $input['points']     : 0;
    $type       = isset($input['type'])         ? trim($input['type'])       : '';
    $source     = isset($input['source'])       ? trim($input['source'])     : '';
    $guestId    = isset($input['guest_id'])  && $input['guest_id'] > 0  ? (int) $input['guest_id']  : null;
    $deviceId   = isset($input['device_id'])    ? trim($input['device_id'])  : null;
    $clientTxId = isset($input['client_tx_id']) ? trim($input['client_tx_id']) : null;

    if ($points <= 0)   jsonResponse(false, 'Pontos inválidos (> 0)', [], 400);
    if (empty($type))   jsonResponse(false, 'Tipo é obrigatório', [], 400);
    if (empty($source)) jsonResponse(false, 'Source é obrigatório', [], 400);

    // Mapear tipo para valor válido do ENUM
    $validTransTypes = ['EARN', 'WITHDRAW', 'BONUS', 'MISSION', 'PENALTY'];
    $transType       = in_array(strtoupper($type), $validTransTypes) ? strtoupper($type) : 'EARN';

    if ($deviceId !== null && strlen($deviceId) < 10) {
        jsonResponse(false, 'device_id inválido (min 10 chars)', [], 400);
    }

    $conn = getDBConnection();

    // ──────────────────────────────────────────────────────────────────────
    // Idempotência: se client_tx_id já existe, retornar o total atual sem
    // duplicar a transação. Isso protege contra retries do frontend.
    // ──────────────────────────────────────────────────────────────────────
    if ($clientTxId) {
        $dup = $conn->prepare("
            SELECT t.transaction_id, t.amount, t.points_after,
                   COALESCE(s.points, 0) AS current_points
            FROM " . DB_PREFIX . "guest_transactions t
            LEFT JOIN " . DB_PREFIX . "guest_scores s ON t.guest_id = s.guest_id
            WHERE t.client_tx_id = ?
            LIMIT 1
        ");
        $dup->execute([$clientTxId]);
        $existing = $dup->fetch();

        if ($existing) {
            // Transação já processada — retornar sucesso com total atual
            http_response_code(200);
            header('Content-Type: application/json; charset=utf-8');
            echo json_encode([
                'status'         => 'success',
                'message'        => 'Transação já processada (idempotente)',
                'transaction_id' => (int) $existing['transaction_id'],
                'points_added'   => (int) $existing['amount'],
                'new_total'      => (int) $existing['current_points'],
                'total_points'   => (int) $existing['current_points'],
                'user_type'      => 'guest',
                'guest_id'       => $guestId,
                'idempotent'     => true,
            ], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
            exit;
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Processar transação normalmente
    // ──────────────────────────────────────────────────────────────────────
    $conn->beginTransaction();

    try {
        $accountId     = null;
        $currentPoints = 0;
        $newTotal      = 0;

        // ── CASO 1: Guest com ID direto ──
        if ($guestId && $guestId > 0) {
            $fp = $input['device_fingerprint'] ?? $_SERVER['HTTP_X_DEVICE_FINGERPRINT'] ?? $deviceId ?? null;
            if (!authenticateGuest($guestId, $fp)) {
                $conn->rollBack();
                jsonResponse(false, 'Autenticação inválida', [], 403);
            }

            $stmt = $conn->prepare("
                SELECT g.guest_id, g.status, COALESCE(s.points,0) as points, COALESCE(s.lifetime_points,0) as lifetime_points
                FROM " . DB_PREFIX . "guest_users g
                LEFT JOIN " . DB_PREFIX . "guest_scores s ON g.guest_id = s.guest_id
                WHERE g.guest_id = ? FOR UPDATE
            ");
            $stmt->execute([$guestId]);
            $guest = $stmt->fetch();

            if (!$guest)                      throw new Exception('Guest não encontrado');
            if ($guest['status'] !== 'active') throw new Exception('Conta inativa');

            $accountId     = $guestId;
            $currentPoints = (int) $guest['points'];
            $newTotal      = $currentPoints + $points;
            $newLifetime   = (int) $guest['lifetime_points'] + $points;

            $scoreCheck = $conn->prepare("SELECT row_id FROM " . DB_PREFIX . "guest_scores WHERE guest_id = ?");
            $scoreCheck->execute([$guestId]);

            if ($scoreCheck->fetch()) {
                $conn->prepare("UPDATE " . DB_PREFIX . "guest_scores SET points = ?, lifetime_points = ?, updated_at = NOW() WHERE guest_id = ?")
                     ->execute([$newTotal, $newLifetime, $guestId]);
            } else {
                $conn->prepare("INSERT INTO " . DB_PREFIX . "guest_scores (guest_id, points, lifetime_points, level, updated_at) VALUES (?,?,?,1,NOW())")
                     ->execute([$guestId, $newTotal, $newLifetime]);
            }

            $conn->prepare("UPDATE " . DB_PREFIX . "guest_users SET last_access = NOW() WHERE guest_id = ?")->execute([$guestId]);

        // ── CASO 2: Recuperar Guest por device_fingerprint ──
        } elseif ($deviceId && strlen($deviceId) >= 10) {
            $stmt = $conn->prepare("
                SELECT g.guest_id, g.status, COALESCE(s.points,0) as points, COALESCE(s.lifetime_points,0) as lifetime_points
                FROM " . DB_PREFIX . "guest_users g
                LEFT JOIN " . DB_PREFIX . "guest_scores s ON g.guest_id = s.guest_id
                WHERE g.device_fingerprint = ? AND g.status = 'active'
                ORDER BY g.created_at DESC LIMIT 1
                FOR UPDATE
            ");
            $stmt->execute([$deviceId]);
            $existing = $stmt->fetch();

            if (!$existing) {
                $conn->rollBack();
                jsonResponse(false, 'Nenhuma conta encontrada. Abra a tela inicial primeiro.', [], 400);
            }

            $guestId       = (int) $existing['guest_id'];
            $currentPoints = (int) $existing['points'];
            $accountId     = $guestId;
            $newTotal      = $currentPoints + $points;
            $newLifetime   = (int) $existing['lifetime_points'] + $points;

            $scoreCheck = $conn->prepare("SELECT row_id FROM " . DB_PREFIX . "guest_scores WHERE guest_id = ?");
            $scoreCheck->execute([$guestId]);

            if ($scoreCheck->fetch()) {
                $conn->prepare("UPDATE " . DB_PREFIX . "guest_scores SET points = ?, lifetime_points = ?, updated_at = NOW() WHERE guest_id = ?")
                     ->execute([$newTotal, $newLifetime, $guestId]);
            } else {
                $conn->prepare("INSERT INTO " . DB_PREFIX . "guest_scores (guest_id, points, lifetime_points, level, updated_at) VALUES (?,?,?,1,NOW())")
                     ->execute([$guestId, $newTotal, $newLifetime]);
            }

            $conn->prepare("UPDATE " . DB_PREFIX . "guest_users SET last_access = NOW() WHERE guest_id = ?")->execute([$guestId]);

        } else {
            throw new Exception('Deve fornecer guest_id ou device_id válido');
        }

        // ── Registrar transação (com client_tx_id para idempotência) ──
        $description = isset($input['description']) ? trim($input['description']) : "Pontos de {$source}";

        $conn->prepare("
            INSERT INTO " . DB_PREFIX . "guest_transactions
            (guest_id, type, amount, points_before, points_after, description, source, client_tx_id, status, created_at)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, 'COMPLETED', NOW())
        ")->execute([$accountId, $transType, $points, $currentPoints, $newTotal, $description, $source, $clientTxId]);

        $transactionId = $conn->lastInsertId();

        $conn->commit();

        $responseData = [
            'transaction_id' => $transactionId,
            'points_added'   => $points,
            'new_total'      => $newTotal,
            'total_points'   => $newTotal,
            'user_type'      => 'guest',
            'guest_id'       => $guestId,
        ];

        http_response_code(200);
        header('Content-Type: application/json; charset=utf-8');
        echo json_encode(array_merge(['status' => 'success', 'message' => 'Pontos registrados'], $responseData), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
        exit;

    } catch (Exception $e) {
        $conn->rollBack();
        throw $e;
    }
} catch (PDOException $e) {
    error_log('unified_submit_score.php: ' . $e->getMessage());
    http_response_code(500);
    echo json_encode(['status' => 'error', 'message' => 'Erro interno'], JSON_UNESCAPED_UNICODE);
    exit;
} catch (Exception $e) {
    error_log('unified_submit_score.php: ' . $e->getMessage());
    http_response_code(400);
    echo json_encode(['status' => 'error', 'message' => $e->getMessage()], JSON_UNESCAPED_UNICODE);
    exit;
}
