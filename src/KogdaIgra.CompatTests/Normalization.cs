using System.Text.RegularExpressions;

namespace KogdaIgra.CompatTests;

/// <summary>
/// Вычищает из ответов недетерминированные куски перед сравнением: автоинкрементные id,
/// таймстампы, session id (требование ADR 0001). Наполняется по мере появления реальных
/// compat-тестов — сюда попадает только то, что действительно мешает сравнению.
/// </summary>
public static partial class Normalization
{
    /// <summary>Убирает PHPSESSID из тела ответа.</summary>
    public static string StripSessionId(string body) => SessionId().Replace(body, "PHPSESSID=<session>");

    [GeneratedRegex("PHPSESSID=[0-9a-zA-Z]+")]
    private static partial Regex SessionId();
}
