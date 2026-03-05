# 🔧 Correção de Erros PHP - unified_submit_score.php

## ❌ Erros Encontrados

### Erro 1: Redeclaração de jsonResponse()
```
Cannot redeclare jsonResponse() (previously declared in config.php:52)
```

**Causa:** O `config.php` no servidor já define a função `jsonResponse()`.

**Solução:** O script agora verifica se a função existe antes de declarar.

---

### Erro 2: Database.php não encontrado
```
Failed to open stream: No such file or directory Database.php
```

**Causa:** O arquivo `Database.php` não existe no servidor.

**Solução:** O script agora cria a classe `Database` inline se o arquivo não existir, MAS é recomendado fazer upload do arquivo.

---

## ✅ Correções Aplicadas

### 1. Verificação de jsonResponse()
- Script verifica se função já existe antes de declarar
- Usa a função do `config.php` se disponível

### 2. Tratamento de Database.php
- Verifica se arquivo existe antes de incluir
- Se não existir, cria classe `Database` inline automaticamente
- **Recomendado:** Fazer upload do arquivo `Database.php` separado

---

## 📤 Arquivos para Upload

### Arquivo 1: unified_submit_score.php
**Localização no projeto:** `Assets/php/unified_submit_score.php`  
**Localização no servidor:** `app_pix01/php/unified_submit_score.php`

### Arquivo 2: Database.php (RECOMENDADO)
**Localização no projeto:** `Assets/php/Database.php`  
**Localização no servidor:** `app_pix01/php/Database.php`

**Conteúdo do Database.php:**
```php
<?php
class Database {
    private static $instance = null;
    private $connection = null;

    private function __construct() {
        try {
            $dsn = "mysql:host=" . DB_HOST . ";dbname=" . DB_NAME . ";charset=" . DB_CHARSET;
            $options = [
                PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
                PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
                PDO::ATTR_EMULATE_PREPARES => false,
            ];
            $this->connection = new PDO($dsn, DB_USER, DB_PASS, $options);
        } catch (PDOException $e) {
            error_log("Database connection failed: " . $e->getMessage());
            throw new Exception("Database connection failed");
        }
    }

    public static function getInstance() {
        if (self::$instance === null) {
            self::$instance = new self();
        }
        return self::$instance;
    }

    public function getConnection() {
        return $this->connection;
    }

    private function __clone() {}
    public function __wakeup() {
        throw new Exception("Cannot unserialize singleton");
    }
}
```

---

## 🚀 Passos para Resolver

### Opção A: Upload do Database.php (RECOMENDADO)

1. **Fazer upload do arquivo `Database.php`**
   - De: `Assets/php/Database.php`
   - Para: `app_pix01/php/Database.php`

2. **Fazer upload do `unified_submit_score.php` corrigido**
   - De: `Assets/php/unified_submit_score.php`
   - Para: `app_pix01/php/unified_submit_score.php`

3. **Testar novamente**

### Opção B: Usar versão inline (TEMPORÁRIO)

O script agora funciona mesmo sem `Database.php`, mas é melhor fazer upload do arquivo separado.

---

## ✅ Verificação Pós-Correção

Após fazer upload, teste:

1. **Acesse a URL diretamente:**
   ```
   https://serveapp.mobplaygames.com.br/app_pix01/php/unified_submit_score.php
   ```
   - Deve retornar erro de método (não 404, não 500)

2. **Teste via página de teste:**
   - Use `test_points.html`
   - Envie pontos via web
   - Verifique se funciona

3. **Verifique logs:**
   - `app_pix01/php/logs/score_submissions.log`
   - Não deve ter mais erros de redeclaração

---

**Última atualização:** 2025-01-27










