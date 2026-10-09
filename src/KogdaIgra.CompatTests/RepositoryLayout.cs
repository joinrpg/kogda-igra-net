namespace KogdaIgra.CompatTests;

/// <summary>
/// Поиск файлов репозитория относительно собранной сборки: из-за UseArtifactsOutput
/// тесты запускаются из artifacts/, а не из папки проекта.
/// </summary>
public static class RepositoryLayout
{
    /// <summary>Корень репозитория — каталог, в котором лежит KogdaIgra.slnx.</summary>
    public static DirectoryInfo Root { get; } = FindRoot();

    /// <summary>Каталог db/ с тестовыми данными. Схема БД берётся из образа сайта, см. <see cref="DatabaseSchema"/>.</summary>
    public static DirectoryInfo Database { get; } = new(Path.Combine(Root.FullName, "db"));

    private static DirectoryInfo FindRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "KogdaIgra.slnx")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException(
            $"Не найден корень репозитория (KogdaIgra.slnx) выше {AppContext.BaseDirectory}.");
    }
}
