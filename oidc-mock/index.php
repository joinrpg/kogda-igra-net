<?php
// Поддельный сервер OIDC для compat-тестов входа через JoinRPG.
//
// Нужен потому, что адрес сервера берётся из переменной JOINRPG_ID_BASE, а значит
// его можно увести на обычный HTTP — сертификат не требуется, и PHP-репозиторий
// править не надо.
//
// Логика намеренно примитивная: email кодируется прямо в authorization code,
// чтобы тест управлял результатом входа, не договариваясь с сервером отдельно.
//
//   /connect/token      POST -> {"access_token": "token:<code>"}
//   /connect/user_info  GET  -> {"email": "<code>"}
//
// Особые значения code:
//   "deny-token" — сервер отвечает без access_token (обмен кода не удался);
//   "deny-email" — токен выдаётся, но user_info приходит без email.

header('Content-Type: application/json');

$path = parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH);

// Каждый запрос записывается, чтобы тест мог проверить, что именно прислало
// приложение — в первую очередь code_verifier из PKCE.
function remember(string $endpoint, array $payload): void
{
    $line = json_encode(['endpoint' => $endpoint] + $payload) . "\n";
    @file_put_contents('/requests/requests.log', $line, FILE_APPEND | LOCK_EX);
}

if ($path === '/connect/token') {
    $code = $_POST['code'] ?? '';

    remember('token', [
        'code' => $code,
        'client_id' => $_POST['client_id'] ?? '',
        'client_secret' => $_POST['client_secret'] ?? '',
        'grant_type' => $_POST['grant_type'] ?? '',
        'redirect_uri' => $_POST['redirect_uri'] ?? '',
        'code_verifier' => $_POST['code_verifier'] ?? '',
    ]);

    if ($code === 'deny-token') {
        echo json_encode(['error' => 'invalid_grant']);
        exit;
    }

    echo json_encode(['access_token' => 'token:' . $code, 'token_type' => 'Bearer']);
    exit;
}

if ($path === '/connect/user_info') {
    // HTTP_AUTHORIZATION заполняется правилом из .htaccess; REDIRECT_ — это то же
    // значение после внутреннего перенаправления на index.php.
    $authorization = $_SERVER['HTTP_AUTHORIZATION']
        ?? $_SERVER['REDIRECT_HTTP_AUTHORIZATION']
        ?? '';
    $token = str_starts_with($authorization, 'Bearer ') ? substr($authorization, 7) : '';

    remember('user_info', ['authorization' => $authorization]);

    $code = str_starts_with($token, 'token:') ? substr($token, 6) : '';

    if ($code === '' || $code === 'deny-email') {
        echo json_encode(['sub' => 'nobody']);
        exit;
    }

    echo json_encode(['sub' => $code, 'email' => $code]);
    exit;
}

http_response_code(404);
echo json_encode(['error' => 'not_found', 'path' => $path]);
