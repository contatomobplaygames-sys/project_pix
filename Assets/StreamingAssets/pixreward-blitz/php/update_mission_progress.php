<?php
/**
 * Update Mission Progress API
 * Atualiza o progresso de uma missão do guest.
 * Requer autenticação via X-Device-Fingerprint.
 */

require_once __DIR__ . '/config.php';

bootstrapEndpoint();

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    jsonResponse(false, 'Método não permitido', [], 405);
}

try {
    $input = file_get_contents('php://input');
    $data  = json_decode($input, true);

    if (!$data) {
        jsonResponse(false, 'Dados inválidos', [], 400);
    }

    $guestId            = isset($data['guest_id'])            ? (int) $data['guest_id'] : 0;
    $missionId          = isset($data['mission_id'])          ? sanitizeInput($data['mission_id']) : '';
    $currentClicks      = isset($data['current_clicks'])      ? (int) $data['current_clicks'] : 0;
    $lastClickTimestamp = isset($data['last_click_timestamp']) ? (int) $data['last_click_timestamp'] : null;
    $isLocked           = isset($data['is_locked'])           ? (int) $data['is_locked'] : 0;
    $isCompleted        = isset($data['is_completed'])        ? (bool) $data['is_completed'] : false;

    if (!$guestId || !$missionId) {
        jsonResponse(false, 'guest_id e mission_id são obrigatórios', [], 400);
    }

    // Autenticação
    requireGuestAuth($guestId, $data['device_fingerprint'] ?? null);

    $conn = getDBConnection();

    // Verificar se a missão existe
    $checkStmt = $conn->prepare("SELECT id FROM " . DB_PREFIX . "guest_missions WHERE guest_id = ? AND mission_id = ?");
    $checkStmt->execute([$guestId, $missionId]);
    $exists = $checkStmt->fetch();

    if (!$exists) {
        // Valores padrão alinhados com frontend (reward = requiredClicks * 2)
        $defaultMissions = [
            'mission_1' => ['Tarefa Iniciante', 10, 20, 60, 0],
            'mission_2' => ['Tarefa Rápida',     5, 10, 60, 1],
            'mission_3' => ['Tarefa Elite',      30, 60, 60, 1],
        ];

        $default = $defaultMissions[$missionId] ?? ['Tarefa', 10, 20, 60, 1];

        $conn->prepare("
            INSERT INTO " . DB_PREFIX . "guest_missions
            (guest_id, mission_id, title, required_clicks, reward, cooldown_seconds, is_locked, current_clicks, last_click_timestamp)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
        ")->execute([
            $guestId, $missionId,
            $default[0], $default[1], $default[2], $default[3],
            $isLocked, $currentClicks, $lastClickTimestamp,
        ]);
    } else {
        $updateFields = [];
        $updateValues = [];

        $updateFields[] = 'current_clicks = ?';
        $updateValues[] = $currentClicks;

        if ($lastClickTimestamp !== null) {
            $updateFields[] = 'last_click_timestamp = ?';
            $updateValues[] = $lastClickTimestamp;
        }

        $updateFields[] = 'is_locked = ?';
        $updateValues[] = $isLocked;

        if ($isCompleted) {
            $updateFields[] = 'completed_count = completed_count + 1';
            $updateFields[] = 'last_completed_at = NOW()';
            $updateFields[] = 'current_clicks = 0';
            $updateValues[0] = 0; // override
        }

        $updateValues[] = $guestId;
        $updateValues[] = $missionId;

        $conn->prepare(
            "UPDATE " . DB_PREFIX . "guest_missions SET " . implode(', ', $updateFields) . " WHERE guest_id = ? AND mission_id = ?"
        )->execute($updateValues);
    }

    // Buscar missão atualizada
    $selectStmt = $conn->prepare("
        SELECT mission_id, title, current_clicks, required_clicks, reward,
               cooldown_seconds, is_locked, last_click_timestamp, completed_count
        FROM " . DB_PREFIX . "guest_missions WHERE guest_id = ? AND mission_id = ?
    ");
    $selectStmt->execute([$guestId, $missionId]);
    $mission = $selectStmt->fetch();

    if (!$mission) {
        jsonResponse(false, 'Erro ao atualizar missão', [], 500);
    }

    jsonResponse(true, 'Progresso da missão atualizado', [
        'mission' => [
            'id'                 => $mission['mission_id'],
            'title'              => $mission['title'],
            'requiredClicks'     => (int) $mission['required_clicks'],
            'currentClicks'      => (int) $mission['current_clicks'],
            'reward'             => (int) $mission['reward'],
            'cooldownSeconds'    => (int) $mission['cooldown_seconds'],
            'lastClickTimestamp' => $mission['last_click_timestamp'] ? (int) $mission['last_click_timestamp'] : null,
            'isLocked'           => (bool) $mission['is_locked'],
            'completedCount'     => (int) $mission['completed_count'],
        ],
    ]);
} catch (PDOException $e) {
    error_log('update_mission_progress.php: ' . $e->getMessage());
    jsonResponse(false, 'Erro interno ao atualizar missão', [], 500);
} catch (Exception $e) {
    error_log('update_mission_progress.php: ' . $e->getMessage());
    jsonResponse(false, 'Erro ao atualizar missão', [], 500);
}
