<?php
// Включает сбор покрытия на каждый запрос и сбрасывает результат в /coverage-data.
//
// Подключается через auto_prepend_file, то есть выполняется до кода страницы.
// Специально не использует ничего из приложения: любая ошибка здесь сломала бы
// весь сайт, а не только замер.

if (function_exists('xdebug_start_code_coverage')) {
    // XDEBUG_CC_UNUSED обязателен: без него Xdebug возвращает только выполненные
    // строки, и любой отчёт получается ровно 100-процентным, что бессмысленно.
    // XDEBUG_CC_DEAD_CODE помечает недостижимые строки статусом -2, чтобы они
    // не попадали в знаменатель.
    xdebug_start_code_coverage(XDEBUG_CC_UNUSED | XDEBUG_CC_DEAD_CODE);

    register_shutdown_function(static function (): void {
        if (!function_exists('xdebug_get_code_coverage')) {
            return;
        }

        $coverage = xdebug_get_code_coverage();

        // Своё имя в отчёт не попадает — оно не про приложение.
        unset($coverage['/coverage-collect.php']);

        // Уникальное имя: страницы могут выполняться параллельно.
        $name = sprintf(
            '/coverage-data/%s-%s.json',
            microtime(true),
            bin2hex(random_bytes(4))
        );

        @file_put_contents($name, json_encode($coverage));
    });
}
