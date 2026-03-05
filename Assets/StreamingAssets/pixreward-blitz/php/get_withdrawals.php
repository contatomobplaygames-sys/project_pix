<?php
/**
 * Get Withdrawals API
 * Retorna o histórico de saques de um guest.
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

    // Autenticação
    requireGuestAuth($guestId);

    $stmt = $conn->prepare("
        SELECT withdrawal_id, request_id, level, points_used, points_before, points_after,
               amount, pix_key, pix_key_type, beneficiary_name, email,
               status, processed_at, rejection_reason, created_at, updated_at
        FROM " . DB_PREFIX . "guest_withdrawals
        WHERE guest_id = ?
        ORDER BY created_at DESC
    ");
    $stmt->execute([$guestId]);
    $withdrawals = $stmt->fetchAll(PDO::FETCH_ASSOC);

    $formattedWithdrawals = array_map(function ($w) {
        $status = 'PENDING';
        if (in_array($w['status'], ['COMPLETED', 'APPROVED'], true)) {
            $status = 'COMPLETED';
        }

        $amountFormatted = 'R$ ' . number_format((float) $w['amount'], 2, ',', '.');
        $details = "Saque Nível {$w['level']} ({$amountFormatted})";

        return [
            'id'               => $w['request_id'],
            'type'             => 'WITHDRAW',
            'amount'           => (int) $w['points_used'],
            'date'             => $w['created_at'],
            'details'          => $details,
            'status'           => $status,
            'raw_status'       => $w['status'],
            'amount_currency'  => $amountFormatted,
            'pix_key'          => $w['pix_key'],
            'pix_key_type'     => $w['pix_key_type'],
            'beneficiary_name' => $w['beneficiary_name'],
            'processed_at'     => $w['processed_at'],
            'rejection_reason' => $w['rejection_reason'],
        ];
    }, $withdrawals);

    jsonResponse(true, 'Saques carregados com sucesso', ['withdrawals' => $formattedWithdrawals]);
} catch (PDOException $e) {
    error_log('get_withdrawals.php: ' . $e->getMessage());
    jsonResponse(false, 'Erro interno ao buscar saques', [], 500);
} catch (Exception $e) {
    error_log('get_withdrawals.php: ' . $e->getMessage());
    jsonResponse(false, 'Erro ao buscar saques', [], 500);
}
