namespace ReadingPal.E2E.Support;

/// <summary>
/// Where the system under test runs. Every value can be overridden with an environment variable; the
/// defaults are the local Docker stack described in TEST-PLAN.md §2.
/// </summary>
public static class E2EConfig
{
    public static string FrontendUrl => Env("E2E_FRONTEND_URL", "http://localhost:5173");
    public static string UserServiceUrl => Env("E2E_USER_SERVICE_URL", "http://localhost:5050");
    public static string InventoryServiceUrl => Env("E2E_INVENTORY_SERVICE_URL", "http://localhost:5001");
    public static string LendingServiceUrl => Env("E2E_LENDING_SERVICE_URL", "http://localhost:5148");
    public static string NotificationServiceUrl => Env("E2E_NOTIFICATION_SERVICE_URL", "http://localhost:5003");

    // The built-in admin exists only in Development (services/user-service/appsettings.Development.json).
    public static string AdminEmail => Env("E2E_ADMIN_EMAIL", "admin@library.com");
    public static string AdminPassword => Env("E2E_ADMIN_PASSWORD", "adminpassword");

    public static bool Headless => !string.Equals(Environment.GetEnvironmentVariable("E2E_HEADLESS"), "false", StringComparison.OrdinalIgnoreCase);


    public static TimeSpan UiTimeout => TimeSpan.FromSeconds(15);

    /// <summary>
    /// Kafka-driven results: the Lending outbox runs every 5 s and the bell refreshes every 10 s, so
    /// allow comfortably more than both.
    /// </summary>
    public static TimeSpan EventTimeout => TimeSpan.FromSeconds(45);

    /// <summary>
    /// MySQL, used only for set-up the UI cannot do (a second admin, backdated loans). Defaults to the
    /// credentials in the repository's local .env file, so no password is stored in the tests.
    /// </summary>
    public static string MySqlConnection(string database)
    {
        var configured = Environment.GetEnvironmentVariable("E2E_MYSQL_CONNECTION");
        if (!string.IsNullOrWhiteSpace(configured)) return $"{configured.TrimEnd(';')};Database={database}";
        var env = LocalEnvFile();
        var user = env.GetValueOrDefault("MYSQL_USER", "readingpal_user");
        var password = env.GetValueOrDefault("MYSQL_PASSWORD", "");
        var port = env.GetValueOrDefault("MYSQL_PORT", "3306");
        return $"Server=localhost;Port={port};Uid={user};Pwd={password};Database={database}";
    }

    public static string UserDatabase => LocalEnvFile().GetValueOrDefault("USER_DB_NAME", "ReadingPal_auth");
    public static string LendingDatabase => LocalEnvFile().GetValueOrDefault("LENDING_DB_NAME", "ReadingPal_lending");

    private static string Env(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value.TrimEnd('/') : fallback;

    private static Dictionary<string, string>? _envFile;

    private static Dictionary<string, string> LocalEnvFile()
    {
        if (_envFile != null) return _envFile;
        var values = new Dictionary<string, string>();
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "docker-compose.yml")))
            directory = directory.Parent;
        var path = directory == null ? null : Path.Combine(directory.FullName, ".env");
        if (path != null && File.Exists(path))
        {
            foreach (var line in File.ReadAllLines(path))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#') || !trimmed.Contains('=')) continue;
                var index = trimmed.IndexOf('=');
                values[trimmed[..index].Trim()] = trimmed[(index + 1)..].Trim().Trim('"');
            }
        }
        return _envFile = values;
    }
}
