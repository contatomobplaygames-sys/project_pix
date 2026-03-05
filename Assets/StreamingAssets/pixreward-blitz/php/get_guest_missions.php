<?php
/**
 * Get Guest Missions API
 * Retorna todas as missões do guest com progresso atual.
 * Requer autenticação via X-Device-Fingerprint.
 */

require_once __DIR__ . '/config.php';

bootstrapEndpoint();

try {
    $guestId = isset($_GET['guest_id']) ? (int) $_GET['guest_id'] : 0;

    if (!$guestId) {
        jsonResponse(false, 'guest_id é obrigatório', [], 400);
    }

    // Autenticação
    requireGuestAuth($guestId);

    $conn = getDBConnection();

    // Verificar se guest existe
    $stmt = $conn->prepare("SELECT guest_id FROM " . DB_PREFIX . "guest_users WHERE guest_id = ?");
    $stmt->execute([$guestId]);
    if (!$stmt->fetch()) {
        jsonResponse(false, 'Guest não encontrado', [], 404);
    }

    // Buscar missões
    $stmt = $conn->prepare("
        SELECT mission_id, title, current_clicks, required_clicks, reward,
               cooldown_seconds, is_locked, last_click_timestamp, completed_count
        FROM " . DB_PREFIX . "guest_missions
        WHERE guest_id = ?
        ORDER BY
            CASE mission_id
                WHEN 'mission_1' THEN 1
                WHEN 'mission_2' THEN 2
                WHEN 'mission_3' THEN 3
                ELSE 4
            END
    ");
    $stmt->execute([$guestId]);
    $missions = $stmt->fetchAll();

    // Criar missões padrão se vazio
    if (empty($missions)) {
        $defaultMissions = [
            ['mission_1', 'Tarefa Iniciante', 10, 20, 60, 0],
            ['mission_2', 'Tarefa Rápida',    5,  10, 60, 1],
            ['mission_3', 'Tarefa Elite',     30,  60, 60, 1],
        ];

        $insertStmt = $conn->prepare("
            INSERT INTO " . DB_PREFIX . "guest_missions
            (guest_id, mission_id, title, required_clicks, reward, cooldown_seconds, is_locked, current_clicks)
            VALUES (?, ?, ?, ?, ?, ?, ?, 0)
        ");

        foreach ($defaultMissions as $m) {
            $insertStmt->execute([$guestId, $m[0], $m[1], $m[2], $m[3], $m[4], $m[5]]);
        }

        $stmt->execute([$guestId]);
        $missions = $stmt->fetchAll();
    }

    $formattedMissions = array_map(function ($m) {
        return [
            'id'                 => $m['mission_id'],
            'title'              => $m['title'],
            'requiredClicks'     => (int) $m['required_clicks'],
            'currentClicks'      => (int) $m['current_clicks'],
            'reward'             => (int) $m['reward'],
            'cooldownSeconds'    => (int) $m['cooldown_seconds'],
            'lastClickTimestamp' => $m['last_click_timestamp'] ? (int) $m['last_click_timestamp'] : null,
            'isLocked'           => (bool) $m['is_locked'],
            'completedCount'     => (int) $m['completed_count'],
        ];
    }, $missions);

    jsonResponse(true, 'Missões carregadas com sucesso', ['missions' => $formattedMissions]);
} catch (PDOException $e) {
    error_log('get_guest_missions.php: ' . $e->getMessage());
    jsonResponse(false, 'Erro interno ao buscar missões', [], 500);
} catch (Exception $e) {
    error_log('get_guest_missions.php: ' . $e->getMessage());
    jsonResponse(false, 'Erro ao buscar missões', [], 500);
}
