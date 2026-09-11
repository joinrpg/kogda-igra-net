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

    /// <summary>Клиент, настроенный на <see cref="BaseUrl"/>. Вызывающий владеет результатом.</summary>
    HttpClient CreateClient();

    /// <summary>Приводит БД к чистому, заранее засеянному состоянию. Для write-тестов.</summary>
    Task ResetDatabaseAsync(CancellationToken cancellationToken = default);
}
