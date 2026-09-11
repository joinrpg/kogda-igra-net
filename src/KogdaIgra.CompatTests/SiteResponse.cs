using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace KogdaIgra.CompatTests;

/// <summary>
/// Разбор ответа сайта. Отдельный тип нужен потому, что по HTTP-статусу о
/// работоспособности этого приложения судить нельзя: PHP-версия отвечает 200
/// и на страницу с фатальной SQL-ошибкой.
/// </summary>
public static class SiteResponse
{
    private static readonly HtmlParser Parser = new();

    /// <summary>Ответ похож на настоящую страницу, а не на текст ошибки БД.</summary>
    public static bool LooksLikeHtmlPage(string body)
        => body.Contains("</html>", StringComparison.OrdinalIgnoreCase) && !ContainsDatabaseError(body);

    /// <summary>Тело ответа — это вывалившаяся наружу ошибка Postgres.</summary>
    public static bool ContainsDatabaseError(string body)
        => body.Contains("ERROR:", StringComparison.Ordinal)
            || body.Contains("pg_query", StringComparison.OrdinalIgnoreCase);

    /// <summary>Разобранный документ — для ассертов по структуре страницы.</summary>
    public static IHtmlDocument Parse(string body) => Parser.ParseDocument(body);

    /// <summary>Короткая выжимка тела для сообщений об ошибках.</summary>
    public static string Excerpt(string body)
        => body.Length <= 300 ? body : body[..300] + "…";
}
