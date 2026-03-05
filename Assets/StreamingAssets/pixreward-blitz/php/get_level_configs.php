<?php
/**
 * Get Level Configs API
 * Retorna todas as configurações de níveis ativas.
 * Endpoint público (não requer autenticação) — dados não sensíveis.
 */

require_once __DIR__ . '/config.php';

bootstrapEndpoint();

try {
    $conn = getDBConnection();

    $stmt = $conn->prepare("
        SELECT level, required_points as requiredPoints, reward_value as rewardValue, is_active
        FROM " . DB_PREFIX . "level_configs
        WHERE is_active = 1
        ORDER BY level ASC
    ");
    $stmt->execute();
    $levels = $stmt->fetchAll();

    $formattedLevels = array_map(function ($l) {
        return [
            'level'          => (int)   $l['level'],
            'requiredPoints' => (int)   $l['requiredPoints'],
            'rewardValue'    => (float) $l['rewardValue'],
        ];
    }, $levels);

    jsonResponse(true, 'Níveis carregados com sucesso', ['levels' => $formattedLevels]);
} catch (PDOException $e) {
    error_log('get_level_configs.php: ' . $e->getMessage());
    jsonResponse(false, 'Erro interno ao buscar níveis', [], 500);
} catch (Exception $e) {
    error_log('get_level_configs.php: ' . $e->getMessage());
    jsonResponse(false, 'Erro ao buscar níveis', [], 500);
}
