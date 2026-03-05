# 📤 Guia: Upload do unified_submit_score.php para o Servidor

## 📋 Pré-requisitos

Antes de fazer upload, verifique se você tem:

- ✅ Acesso FTP ou cPanel File Manager ao servidor
- ✅ Arquivo `unified_submit_score.php` (já existe no projeto)
- ✅ Arquivos de dependência: `config.php` e `Database.php`
- ✅ Permissões para criar pastas e arquivos no servidor

---

## 📁 Estrutura de Arquivos Necessária

O arquivo precisa estar na seguinte estrutura no servidor:

```
app_pix01/
  php/
    unified_submit_score.php  ← Arquivo principal
    config.php                ← Configuração do banco (já existe?)
    Database.php              ← Classe de banco (já existe?)
    logs/                     ← Pasta para logs (será criada automaticamente)
```

---

## 🚀 Passo a Passo

### Passo 1: Verificar Arquivos no Projeto

No seu projeto Unity, os arquivos estão em:
- `Assets/php/unified_submit_score.php`
- `Assets/php/config.php`
- `Assets/php/Database.php`

### Passo 2: Acessar o Servidor

**Opção A: Via FTP**
1. Use um cliente FTP (FileZilla, WinSCP, etc.)
2. Conecte ao servidor `serveapp.mobplaygames.com.br`
3. Navegue até `public_html/app_pix01/php/`

**Opção B: Via cPanel File Manager**
1. Acesse o cPanel
2. Abra "File Manager"
3. Navegue até `public_html/app_pix01/php/`

### Passo 3: Verificar Dependências

Antes de fazer upload, verifique se já existem:
- ✅ `config.php` - Deve conter configurações do banco de dados
- ✅ `Database.php` - Classe para conexão com banco

**Se não existirem**, você precisará criá-los ou fazer upload também.

### Passo 4: Fazer Upload do Arquivo

1. **Selecione o arquivo** `unified_submit_score.php` do projeto
2. **Faça upload** para `app_pix01/php/`
3. **Verifique permissões**: O arquivo deve ter permissão 644 (leitura/escrita para dono, leitura para outros)

### Passo 5: Criar Pasta de Logs

O script precisa de uma pasta `logs` para salvar logs:

1. Crie a pasta `logs` dentro de `app_pix01/php/`
2. Defina permissão 755 (leitura/escrita/execução para dono, leitura/execução para outros)
3. O script criará automaticamente os arquivos de log dentro dela

### Passo 6: Verificar Permissões

Certifique-se de que:
- ✅ `unified_submit_score.php` tem permissão **644**
- ✅ Pasta `logs` tem permissão **755**
- ✅ Arquivos `config.php` e `Database.php` existem e têm permissão **644**

---

## ✅ Verificação Pós-Upload

### Teste 1: Verificar se arquivo existe

Acesse no navegador:
```
https://serveapp.mobplaygames.com.br/app_pix01/php/unified_submit_score.php
```

**Resposta esperada:**
- Se retornar erro de método (405) ou JSON de erro: ✅ Arquivo existe!
- Se retornar 404: ❌ Arquivo não foi encontrado

### Teste 2: Testar via Página de Teste

1. Abra a página `test_points.html`
2. Preencha os dados
3. Clique em "🌐 Enviar Via Web"
4. Verifique os logs

**Se funcionar:**
```
✅ Pontos enviados com sucesso!
   - Pontos adicionados: 2
   - Novo total: 152
```

**Se ainda der erro:**
- Verifique os logs na página de teste
- Verifique se `config.php` e `Database.php` estão corretos
- Verifique permissões dos arquivos

---

## 🔧 Configuração do config.php

O arquivo `config.php` deve conter algo como:

```php
<?php
// Configurações do banco de dados
define('DB_HOST', 'localhost');
define('DB_NAME', 'nome_do_banco');
define('DB_USER', 'usuario_banco');
define('DB_PASS', 'senha_banco');
define('DB_CHARSET', 'utf8mb4');
```

**⚠️ IMPORTANTE:** Certifique-se de que as credenciais estão corretas!

---

## 🔧 Configuração do Database.php

O arquivo `Database.php` deve conter a classe para conexão com PDO:

```php
<?php
class Database {
    private static $instance = null;
    private $connection;
    
    private function __construct() {
        // Conexão PDO
    }
    
    public static function getInstance() {
        // Singleton pattern
    }
    
    public function getConnection() {
        // Retorna conexão PDO
    }
}
```

---

## 🐛 Solução de Problemas

### Problema: "404 Not Found" após upload

**Soluções:**
1. Verifique se o arquivo está em `app_pix01/php/unified_submit_score.php`
2. Verifique permissões do arquivo (deve ser 644)
3. Verifique se o servidor está configurado para executar PHP
4. Tente acessar via URL completa no navegador

### Problema: "500 Internal Server Error"

**Soluções:**
1. Verifique se `config.php` existe e está correto
2. Verifique se `Database.php` existe e está correto
3. Verifique logs de erro do PHP no servidor
4. Verifique permissões da pasta `logs`

### Problema: "Database connection failed"

**Soluções:**
1. Verifique credenciais no `config.php`
2. Verifique se o banco de dados existe
3. Verifique se o usuário do banco tem permissões
4. Teste conexão manualmente

### Problema: "No account found"

**Soluções:**
1. Isso é normal se não houver guest_id/user_id válido
2. Certifique-se de que o GuestInitializer criou o guest primeiro
3. Verifique se o device_id está sendo enviado corretamente

---

## 📝 Checklist Final

Antes de considerar concluído, verifique:

- [ ] Arquivo `unified_submit_score.php` foi enviado
- [ ] Arquivo está em `app_pix01/php/`
- [ ] Permissões do arquivo estão corretas (644)
- [ ] Pasta `logs` existe e tem permissão 755
- [ ] Arquivos `config.php` e `Database.php` existem
- [ ] Teste no navegador retorna erro de método (não 404)
- [ ] Teste via página de teste funciona

---

## 🎯 Próximos Passos

Após fazer upload:

1. ✅ Teste via página de teste
2. ✅ Verifique se pontos estão sendo salvos no banco
3. ✅ Verifique logs em `app_pix01/php/logs/score_submissions.log`
4. ✅ Teste no app Unity completo

---

**Última atualização:** 2025-01-27










