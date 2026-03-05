<?php
/**
 * Create Withdrawal API
 * Cria um novo saque para um guest.
 * Requer autenticação via X-Device-Fingerprint.
 *
 * Usa SELECT ... FOR UPDATE para evitar race conditions.
 */

require_once __DIR__ . '/config.php';

bootstrapEndpoint();

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    jsonResponse(false, 'Método não permitido', [], 405);
}

try {
    $conn = getDBConnection();

    $input = json_decode(file_get_contents('php://input'), true);

    if (!$input) {
        jsonResponse(false, 'Dados inválidos', [], 400);
    }

    $guestId         = isset($input['guest_id'])         ? (int) $input['guest_id'] : 0;
    $pixKey          = sanitizeInput($input['pix_key'] ?? '');
    $pixKeyType      = sanitizeInput($input['pix_key_type'] ?? 'RANDOM');
    $beneficiaryName = sanitizeInput($input['beneficiary_name'] ?? '');
    $email           = isset($input['email']) ? sanitizeInput($input['email']) : null;

    if ($guestId <= 0) {
        jsonResponse(false, 'guest_id inválido', [], 400);
    }
    if (empty($pixKey)) {
        jsonResponse(false, 'Chave PIX não fornecida', [], 400);
    }
    if (empty($beneficiaryName)) {
        jsonResponse(false, 'Nome do beneficiário não fornecido', [], 400);
    }

    // Autenticação
    requireGuestAuth($guestId, $input['device_fingerprint'] ?? null);

    // Rate-limit: max 3 saques por hora por IP
    rateLimit('create_withdrawal', 3, 3600);

    // ── Iniciar transação com lock de linha ──────────────────────────
    $conn->beginTransaction();

    try {
        // SELECT ... FOR UPDATE bloqueia a linha até o COMMIT,
        // impedindo que outra request leia o mesmo saldo.
        $scoreStmt = $conn->prepare("
            SELECT points, level
            FROM " . DB_PREFIX . "guest_scores
            WHERE guest_id = ?
            FOR UPDATE
        ");
        $scoreStmt->execute([$guestId]);
        $score = $scoreStmt->fetch();

        if (!$score) {
            $conn->rollBack();
            jsonResponse(false, 'Guest não encontrado', [], 404);
        }

        $currentPoints = (int) $score['points'];
        $currentLevel  = (int) $score['level'];

        // Buscar configuração do nível atual
        $levelStmt = $conn->prepare("
            SELECT required_points, reward_value
            FROM " . DB_PREFIX . "level_configs
            WHERE level = ? AND is_active = 1 LIMIT 1
        ");
        $levelStmt->execute([$currentLevel]);
        $levelConfig = $levelStmt->fetch();

        if (!$levelConfig) {
            $conn->rollBack();
            jsonResponse(false, 'Nível não encontrado ou inativo', [], 404);
        }

        $requiredPoints = (int)   $levelConfig['required_points'];
        $rewardValue    = (float) $levelConfig['reward_value'];

        if ($currentPoints < $requiredPoints) {
            $conn->rollBack();
            jsonResponse(false, 'Pontos insuficientes para o saque', [
                'current_points'  => $currentPoints,
                'required_points' => $requiredPoints,
            ], 400);
        }

        $requestId   = uniqid('WD-', true);
        $pointsAfter = $currentPoints - $requiredPoints;

        // Criar registro de saque
        $conn->prepare("
            INSERT INTO " . DB_PREFIX . "guest_withdrawals
            (guest_id, request_id, level, points_used, points_before, points_after,
             amount, pix_key, pix_key_type, beneficiary_name, email, status)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'PENDING')
        ")->execute([
            $guestId, $requestId, $currentLevel, $requiredPoints,
            $currentPoints, $pointsAfter, $rewardValue,
            $pixKey, $pixKeyType, $beneficiaryName, $email,
        ]);

        // Subtrair pontos e incrementar nível
        $conn->prepare("
            UPDATE " . DB_PREFIX . "guest_scores
            SET points = points - ?, level = level + 1
            WHERE guest_id = ?
        ")->execute([$requiredPoints, $guestId]);

        // Registrar transação
        $conn->prepare("
            INSERT INTO " . DB_PREFIX . "guest_transactions
            (guest_id, type, amount, points_before, points_after, description, source, status)
            VALUES (?, 'WITHDRAW', ?, ?, ?, ?, 'withdrawal', 'COMPLETED')
        ")->execute([
            $guestId, -$requiredPoints, $currentPoints, $pointsAfter,
            "Saque Nível {$currentLevel} (R$ " . number_format($rewardValue, 2, ',', '.') . ")",
        ]);

        // Atualizar dados do guest
        $conn->prepare("
            UPDATE " . DB_PREFIX . "guest_users
            SET guest_name    = COALESCE(NULLIF(?, ''), guest_name),
                email         = COALESCE(NULLIF(?, ''), email),
                chavepix      = COALESCE(NULLIF(?, ''), chavepix),
                pix_key_type  = COALESCE(NULLIF(?, ''), pix_key_type)
            WHERE guest_id = ?
        ")->execute([$beneficiaryName, $email ?? '', $pixKey, $pixKeyType, $guestId]);

        $conn->commit();

        // Buscar novo nível
        $newScoreStmt = $conn->prepare("SELECT level, points FROM " . DB_PREFIX . "guest_scores WHERE guest_id = ?");
        $newScoreStmt->execute([$guestId]);
        $newScore = $newScoreStmt->fetch();

        jsonResponse(true, 'Saque criado com sucesso', [
            'status'           => 'success',
            'request_id'       => $requestId,
            'level'            => $currentLevel,
            'points_used'      => $requiredPoints,
            'amount'           => $rewardValue,
            'new_level'        => (int) ($newScore['level'] ?? $currentLevel + 1),
            'remaining_points' => (int) ($newScore['points'] ?? 0),
        ]);

    } catch (Exception $e) {
        $conn->rollBack();
        throw $e;
    }
} catch (PDOException $e) {
    error_log('create_withdrawal.php: ' . $e->getMessage());
    jsonResponse(false, 'Erro interno ao criar saque', [], 500);
} catch (Exception $e) {
    error_log('create_withdrawal.php: ' . $e->getMessage());
    jsonResponse(false, 'Erro ao criar saque', [], 500);
}
