# 🔧 Solução: Erro 404 ao Enviar Pontos

## ❌ Problema Identificado

O erro **404 (Not Found)** indica que o servidor não conseguiu encontrar o arquivo `unified_submit_score.php` no caminho especificado.

### Erro nos Logs:
```
Status: 404
Erro ao parsear JSON: Unexpected token '<'
```

Isso acontece porque o servidor está retornando uma página HTML de erro (404) em vez de JSON.

---

## 🔍 Possíveis Causas

### 1. **Arquivo não existe no servidor**
O arquivo `unified_submit_score.php` não foi enviado para o servidor ou está em outro local.

### 2. **Caminho incorreto**
A estrutura de pastas no servidor pode ser diferente do esperado.

### 3. **Configuração do servidor**
O servidor pode estar configurado para servir arquivos PHP de um diretório diferente.

---

## ✅ Soluções

### Solução 1: Verificar se o arquivo existe no servidor

1. **Acesse o servidor via FTP/cPanel**
2. **Navegue até a pasta do projeto**
3. **Verifique se o arquivo existe em:**
   - `app_pix01/php/unified_submit_score.php`
   - `php/unified_submit_score.php`
   - `server/php/unified_submit_score.php`

### Solução 2: Testar URLs alternativas

Na página de teste, use o menu dropdown "URLs alternativas" para testar diferentes caminhos:

1. **Padrão:** `https://serveapp.mobplaygames.com.br/app_pix01/php/unified_submit_score.php`
2. **Alternativa 1:** `https://serveapp.mobplaygames.com.br/php/unified_submit_score.php`
3. **Alternativa 2:** `https://serveapp.mobplaygames.com.br/server/php/unified_submit_score.php`
4. **Alternativa 3:** `https://serveapp.mobplaygames.com.br/app_pix01/server/php/unified_submit_score.php`

### Solução 3: Verificar estrutura de pastas no servidor

Execute este comando no servidor (via SSH ou terminal do cPanel):

```bash
find /home/seu_usuario -name "unified_submit_score.php" 2>/dev/null
```

Isso mostrará onde o arquivo realmente está localizado.

### Solução 4: Verificar permissões do arquivo

Certifique-se de que o arquivo tem permissões corretas:

```bash
chmod 644 unified_submit_score.php
```

### Solução 5: Testar diretamente no navegador

Tente acessar a URL diretamente no navegador (deve retornar erro de método, não 404):

```
https://serveapp.mobplaygames.com.br/app_pix01/php/unified_submit_score.php
```

- **Se retornar 404:** O arquivo não existe nesse caminho
- **Se retornar erro de método (405 ou JSON de erro):** O arquivo existe, mas precisa de POST

---

## 🛠️ Passos para Resolver

### Passo 1: Localizar o arquivo no servidor

1. Acesse o servidor via FTP ou cPanel File Manager
2. Procure pelo arquivo `unified_submit_score.php`
3. Anote o caminho completo onde ele está

### Passo 2: Verificar estrutura de pastas

Compare a estrutura local com a do servidor:

**Local (Unity):**
```
Assets/
  php/
    unified_submit_score.php
```

**Servidor (esperado):**
```
public_html/
  app_pix01/
    php/
      unified_submit_score.php
```

### Passo 3: Atualizar URL na página de teste

1. Abra a página de teste
2. Use o dropdown "URLs alternativas"
3. Teste cada URL até encontrar a correta
4. Copie a URL que funcionar

### Passo 4: Atualizar código Unity (se necessário)

Se a URL correta for diferente, atualize no `ServerPointsSender.cs`:

```csharp
[SerializeField] private string submitEndpoint = "CAMINHO_CORRETO/unified_submit_score.php";
```

---

## 📋 Checklist de Verificação

- [ ] Arquivo `unified_submit_score.php` existe no servidor?
- [ ] Arquivo está no caminho correto?
- [ ] Permissões do arquivo estão corretas (644)?
- [ ] Servidor PHP está funcionando?
- [ ] URL está correta na página de teste?
- [ ] URL está correta no código Unity?

---

## 🧪 Teste Rápido

### Teste 1: Verificar se arquivo existe

No terminal do servidor:
```bash
ls -la /caminho/completo/unified_submit_score.php
```

### Teste 2: Testar endpoint via curl

```bash
curl -X POST https://serveapp.mobplaygames.com.br/app_pix01/php/unified_submit_score.php \
  -H "Content-Type: application/json" \
  -d '{"points":2,"type":"test","source":"curl_test","device_id":"test1234567890"}'
```

**Resposta esperada (se arquivo existe):**
```json
{
  "status": "error",
  "message": "No account found..."
}
```

**Resposta se arquivo não existe:**
```
404 Not Found
```

---

## 🔗 URLs Comuns para Testar

1. `https://serveapp.mobplaygames.com.br/app_pix01/php/unified_submit_score.php`
2. `https://serveapp.mobplaygames.com.br/php/unified_submit_score.php`
3. `https://serveapp.mobplaygames.com.br/server/php/unified_submit_score.php`
4. `https://serveapp.mobplaygames.com.br/app_pix01/server/php/unified_submit_score.php`
5. `https://serveapp.mobplaygames.com.br/ServidorWeb/server/php/unified_submit_score.php`

---

## 💡 Dica

Use a página de teste para descobrir a URL correta:

1. Abra `test_points.html`
2. Preencha os dados
3. Teste cada URL alternativa do dropdown
4. A primeira que não retornar 404 é a correta!

---

**Última atualização:** 2025-01-27










