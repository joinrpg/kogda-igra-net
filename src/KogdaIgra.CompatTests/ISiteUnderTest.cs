namespace KogdaIgra.CompatTests;

/// <summary>
/// Сайт, против которого гоняется compat-тест.
/// Единственная точка подмены цели: сегодня это PHP-оригинал (<see cref="PhpSiteUnderTest"/>),
/// завтра — порт на ASP.NET Core. Сами тесты про разницу знать не должны.
/// </summary>
public interface ISiteUnderTest
{
    /// <summary>Базовый адрес поднятого сайта.</summary>
    Uri BaseUrl { get; }

    /// <summary>
    /// Клиент, настроенный на <see cref="BaseUrl"/>. Вызывающий владеет результатом.
    /// </summary>
    /// <param name="followRedirects">
    /// Ставить <c>false</c>, когда сам редирект и есть проверяемое поведение:
    /// PHP-версия отвечает редиректом на невалидный ввод, и порт обязан делать так же.
    /// </param>
    HttpClient CreateClient(bool followRedirects = true);

    /// <summary>Приводит БД к чистому, заранее засеянному состоянию. Для write-тестов.</summary>
    Task ResetDatabaseAsync(CancellationToken cancellationToken = default);
}
