-- ============================================================================
-- PixReward Blitz — Schema Completo (Limpo e Otimizado)
--
-- Banco: xperia22_apppixreact
-- Servidor: MySQL 5.7+
-- Charset: utf8mb4
--
-- Tabelas (7):
--   1. pixreward_guest_users        — Contas de guests (identidade via device fingerprint)
--   2. pixreward_guest_scores       — Pontuação atual de cada guest (1 linha por guest)
--   3. pixreward_guest_transactions — Log de auditoria de todas as movimentações de pontos
--   4. pixreward_guest_missions     — Progresso das tarefas/missões de cada guest
--   5. pixreward_guest_withdrawals  — Solicitações de saque (PIX)
--   6. pixreward_level_configs      — Configuração dos níveis de saque (admin)
--   7. pixreward_rate_limits        — Rate-limiting por IP (auto-gerenciada pelo config.php)
--
-- Removido nesta versão (itens sem uso no código):
--   - pixreward_ad_events           (nenhum endpoint lê ou escreve)
--   - pixreward_guest_levels        (100% redundante com guest_withdrawals)
--   - pixreward_system_settings     (valores hardcoded no frontend config.ts)
--   - v_guest_summary               (view não usada por nenhum endpoint)
--   - v_pending_withdrawals         (view não usada por nenhum endpoint)
--   - 6 colunas de referral/conversão em guest_users (sistema não implementado)
--   - 13 índices sem nenhuma query que os utilize
-- ============================================================================

SET SQL_MODE = "NO_AUTO_VALUE_ON_ZERO";
START TRANSACTION;
SET time_zone = "+00:00";

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8mb4 */;

-- ============================================================================
-- 1. pixreward_guest_users
--    Identidade do guest. Cada dispositivo gera um registro via device_fingerprint.
-- ============================================================================

CREATE TABLE `pixreward_guest_users` (
  `guest_id`           INT          NOT NULL AUTO_INCREMENT,
  `device_fingerprint` VARCHAR(128) NOT NULL               COMMENT 'Fingerprint único do dispositivo',
  `guest_public_id`    VARCHAR(50)  NOT NULL               COMMENT 'ID público (ex: GUEST-XXXX-XXXX)',
  `guest_name`         VARCHAR(100) DEFAULT 'Visitante'    COMMENT 'Nome do visitante',
  `email`              VARCHAR(255) DEFAULT NULL            COMMENT 'Email (opcional)',
  `chavepix`           VARCHAR(255) DEFAULT NULL            COMMENT 'Chave PIX cadastrada',
  `pix_key_type`       ENUM('CPF','EMAIL','PHONE','RANDOM') DEFAULT NULL COMMENT 'Tipo da chave PIX',
  `status`             ENUM('active','inactive') DEFAULT 'active' COMMENT 'Status da conta',
  `user_agent`         TEXT                                 COMMENT 'User-agent no momento da criação',
  `ip_address`         VARCHAR(45)  DEFAULT NULL            COMMENT 'IP no momento da criação',
  `created_at`         TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'Data de criação',
  `last_access`        TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT 'Último acesso',

  PRIMARY KEY (`guest_id`),
  UNIQUE KEY `uniq_fingerprint` (`device_fingerprint`),
  UNIQUE KEY `uniq_public_id`  (`guest_public_id`),
  KEY `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Contas de usuários convidados (guest)';

-- ============================================================================
-- 2. pixreward_guest_scores
--    Snapshot de pontuação — exatamente 1 linha por guest.
--    Atualizada atomicamente via FOR UPDATE em toda operação de pontos.
-- ============================================================================

CREATE TABLE `pixreward_guest_scores` (
  `row_id`          INT NOT NULL AUTO_INCREMENT,
  `guest_id`        INT NOT NULL                      COMMENT 'FK → guest_users',
  `points`          INT NOT NULL DEFAULT 0            COMMENT 'Saldo de pontos atual',
  `lifetime_points` INT NOT NULL DEFAULT 0            COMMENT 'Total acumulado histórico (nunca decrementado)',
  `level`           INT NOT NULL DEFAULT 1            COMMENT 'Nível atual (incrementado a cada saque)',
  `updated_at`      TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

  PRIMARY KEY (`row_id`),
  UNIQUE KEY `uniq_guest` (`guest_id`),

  CONSTRAINT `fk_scores_guest`
    FOREIGN KEY (`guest_id`) REFERENCES `pixreward_guest_users` (`guest_id`)
    ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Pontuação dos guests (1 linha por guest)';

-- ============================================================================
-- 3. pixreward_guest_transactions
--    Log de auditoria imutável. Cada movimentação de pontos gera uma linha.
--    Tabela append-only — nenhum endpoint faz SELECT, mas é essencial para
--    auditoria, debug e futuro histórico de atividades.
-- ============================================================================

CREATE TABLE `pixreward_guest_transactions` (
  `transaction_id` INT          NOT NULL AUTO_INCREMENT,
  `guest_id`       INT          NOT NULL          COMMENT 'FK → guest_users',
  `type`           ENUM('EARN','WITHDRAW','BONUS','MISSION','PENALTY') NOT NULL COMMENT 'Tipo de transação',
  `amount`         INT          NOT NULL          COMMENT 'Pontos (+ganho, -perda)',
  `points_before`  INT          DEFAULT NULL      COMMENT 'Saldo antes da transação',
  `points_after`   INT          DEFAULT NULL      COMMENT 'Saldo após a transação',
  `description`    VARCHAR(255) DEFAULT NULL      COMMENT 'Descrição legível',
  `source`         VARCHAR(50)  DEFAULT NULL      COMMENT 'Origem (mission_1, earn_bonus, ad_reward, etc)',
  `status`         ENUM('COMPLETED','PENDING','FAILED') DEFAULT 'COMPLETED',
  `created_at`     TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,

  PRIMARY KEY (`transaction_id`),
  KEY `idx_guest_date` (`guest_id`, `created_at`),

  CONSTRAINT `fk_transactions_guest`
    FOREIGN KEY (`guest_id`) REFERENCES `pixreward_guest_users` (`guest_id`)
    ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Log de auditoria — todas as movimentações de pontos';

-- ============================================================================
-- 4. pixreward_guest_missions
--    Progresso de cada missão por guest.
--    As missões padrão são criadas automaticamente pelo get_guest_missions.php
--    na primeira requisição de cada guest.
-- ============================================================================

CREATE TABLE `pixreward_guest_missions` (
  `id`                   INT         NOT NULL AUTO_INCREMENT,
  `guest_id`             INT         NOT NULL          COMMENT 'FK → guest_users',
  `mission_id`           VARCHAR(50) NOT NULL          COMMENT 'ID da missão (mission_1, mission_2, mission_3)',
  `title`                VARCHAR(100) NOT NULL DEFAULT '' COMMENT 'Título da missão',
  `current_clicks`       INT         NOT NULL DEFAULT 0 COMMENT 'Cliques (vídeos assistidos) atuais',
  `required_clicks`      INT         NOT NULL          COMMENT 'Cliques necessários para completar',
  `reward`               INT         NOT NULL          COMMENT 'Recompensa em pontos ao completar',
  `cooldown_seconds`     INT         NOT NULL DEFAULT 60 COMMENT 'Cooldown entre cliques (segundos)',
  `is_locked`            TINYINT(1)  DEFAULT 1         COMMENT '0=desbloqueada, 1=bloqueada',
  `last_click_timestamp` BIGINT      DEFAULT NULL      COMMENT 'Timestamp do último clique (ms)',
  `completed_count`      INT         DEFAULT 0         COMMENT 'Quantas vezes foi completada',
  `last_completed_at`    TIMESTAMP   NULL DEFAULT NULL  COMMENT 'Última conclusão',
  `created_at`           TIMESTAMP   NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at`           TIMESTAMP   NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

  PRIMARY KEY (`id`),
  UNIQUE KEY `uniq_guest_mission` (`guest_id`, `mission_id`),
  KEY `idx_mission_id` (`mission_id`),

  CONSTRAINT `fk_missions_guest`
    FOREIGN KEY (`guest_id`) REFERENCES `pixreward_guest_users` (`guest_id`)
    ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Progresso das missões dos guests';

-- ============================================================================
-- 5. pixreward_guest_withdrawals
--    Solicitações de saque PIX. Também serve como log de níveis completados
--    (cada saque = 1 nível concluído).
--    Usa SELECT ... FOR UPDATE no create_withdrawal.php para evitar race conditions.
-- ============================================================================

CREATE TABLE `pixreward_guest_withdrawals` (
  `withdrawal_id`    INT            NOT NULL AUTO_INCREMENT,
  `guest_id`         INT            NOT NULL          COMMENT 'FK → guest_users',
  `request_id`       VARCHAR(36)    NOT NULL          COMMENT 'UUID único da requisição',
  `level`            INT            NOT NULL          COMMENT 'Nível do saque',
  `points_used`      INT            NOT NULL          COMMENT 'Pontos debitados',
  `points_before`    INT            NOT NULL DEFAULT 0 COMMENT 'Saldo antes do saque',
  `points_after`     INT            NOT NULL DEFAULT 0 COMMENT 'Saldo após o saque',
  `amount`           DECIMAL(10,2)  NOT NULL          COMMENT 'Valor em R$',
  `pix_key`          VARCHAR(255)   NOT NULL          COMMENT 'Chave PIX utilizada',
  `pix_key_type`     ENUM('CPF','EMAIL','PHONE','RANDOM') NOT NULL COMMENT 'Tipo da chave PIX',
  `beneficiary_name` VARCHAR(255)   NOT NULL          COMMENT 'Nome do beneficiário',
  `email`            VARCHAR(255)   DEFAULT NULL      COMMENT 'Email do beneficiário',
  `status`           ENUM('PENDING','PROCESSING','APPROVED','REJECTED','COMPLETED','FAILED') DEFAULT 'PENDING',
  `processed_at`     TIMESTAMP      NULL DEFAULT NULL COMMENT 'Data de processamento',
  `rejection_reason` TEXT                             COMMENT 'Motivo da rejeição',
  `created_at`       TIMESTAMP      NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at`       TIMESTAMP      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

  PRIMARY KEY (`withdrawal_id`),
  UNIQUE KEY `uniq_request_id` (`request_id`),
  KEY `idx_guest_status` (`guest_id`, `status`),
  KEY `idx_created_at` (`created_at`),

  CONSTRAINT `fk_withdrawals_guest`
    FOREIGN KEY (`guest_id`) REFERENCES `pixreward_guest_users` (`guest_id`)
    ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Solicitações de saque PIX (também serve como log de níveis completados)';

-- ============================================================================
-- 6. pixreward_level_configs
--    Configuração dos níveis de saque — gerenciada pelo admin.
--    Lida por get_level_configs.php e create_withdrawal.php.
-- ============================================================================

CREATE TABLE `pixreward_level_configs` (
  `config_id`       INT           NOT NULL AUTO_INCREMENT,
  `level`           INT           NOT NULL          COMMENT 'Número do nível',
  `required_points` INT           NOT NULL          COMMENT 'Pontos necessários',
  `reward_value`    DECIMAL(10,2) NOT NULL          COMMENT 'Valor da recompensa em R$',
  `is_active`       TINYINT(1)    DEFAULT 1         COMMENT 'Nível ativo?',
  `created_at`      TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at`      TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

  PRIMARY KEY (`config_id`),
  UNIQUE KEY `uniq_level` (`level`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Configuração dos níveis de saque';

--
-- Dados iniciais dos níveis
--
INSERT INTO `pixreward_level_configs` (`config_id`, `level`, `required_points`, `reward_value`, `is_active`, `created_at`, `updated_at`) VALUES
(1, 1, 150,  1.00, 1, '2025-12-26 18:59:34', '2025-12-27 22:59:09'),
(2, 2, 290,  2.00, 1, '2025-12-26 18:59:34', '2025-12-26 18:59:34'),
(3, 3, 390,  3.00, 1, '2025-12-26 18:59:34', '2026-01-02 23:02:23'),
(4, 4, 710,  5.00, 1, '2025-12-26 18:59:34', '2026-01-02 23:02:30');

-- ============================================================================
-- 7. pixreward_rate_limits
--    Tabela de rate-limiting. Auto-gerenciada pelo rateLimit() em config.php.
--    Linhas são criadas e limpas automaticamente — dados são efêmeros.
--    NOTA: config.php cria esta tabela com CREATE TABLE IF NOT EXISTS,
--    mas a incluímos aqui para referência do schema completo.
-- ============================================================================

CREATE TABLE `pixreward_rate_limits` (
  `id`         BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `ip_address` VARCHAR(45)     NOT NULL,
  `endpoint`   VARCHAR(64)     NOT NULL,
  `hit_at`     DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,

  PRIMARY KEY (`id`),
  KEY `idx_rate` (`ip_address`, `endpoint`, `hit_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

COMMIT;

/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;

-- ============================================================================
-- SCRIPT DE MIGRAÇÃO (executar no banco existente com dados)
--
-- Rode estes comandos no phpMyAdmin ou terminal MySQL para aplicar as
-- mudanças no banco de produção SEM perder dados existentes.
-- ============================================================================

-- 1. Remover views não utilizadas
-- DROP VIEW IF EXISTS `v_guest_summary`;
-- DROP VIEW IF EXISTS `v_pending_withdrawals`;

-- 2. Remover tabelas não utilizadas
-- DROP TABLE IF EXISTS `pixreward_ad_events`;
-- DROP TABLE IF EXISTS `pixreward_guest_levels`;
-- DROP TABLE IF EXISTS `pixreward_system_settings`;

-- 3. Remover colunas de referral/conversão (sistema não implementado)
-- ALTER TABLE `pixreward_guest_users`
--   DROP COLUMN `converted_to_user_id`,
--   DROP COLUMN `referral_code`,
--   DROP COLUMN `referred_by`,
--   DROP COLUMN `referral_points_earned`,
--   DROP COLUMN `total_referrals`,
--   DROP COLUMN `converted_at`;

-- 4. Simplificar ENUM status (remover 'converted' não utilizado)
-- ALTER TABLE `pixreward_guest_users`
--   MODIFY `status` ENUM('active','inactive') DEFAULT 'active';

-- 5. Remover índices sem utilidade (nenhuma query os utiliza)
-- ALTER TABLE `pixreward_guest_missions`   DROP INDEX `idx_guest`,  DROP INDEX `idx_is_locked`;
-- ALTER TABLE `pixreward_guest_scores`     DROP INDEX `idx_points`, DROP INDEX `idx_level`;
-- ALTER TABLE `pixreward_guest_transactions` DROP INDEX `idx_type`, DROP INDEX `idx_status`, DROP INDEX `idx_created_at`;
-- ALTER TABLE `pixreward_guest_users`      DROP INDEX `idx_last_access`, DROP INDEX `idx_referral_code`, DROP INDEX `idx_active_guests`;
-- ALTER TABLE `pixreward_guest_withdrawals` DROP INDEX `idx_status`, DROP INDEX `idx_pending_withdrawals`, DROP INDEX `idx_level`;
