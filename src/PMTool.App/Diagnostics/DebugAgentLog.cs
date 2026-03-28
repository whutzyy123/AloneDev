using System.Text.Json;

namespace PMTool.App.Diagnostics;

/// <summary>Session ec45cc — append NDJSON for debug workflow; remove after verification.</summary>
internal static class DebugAgentLog
{
    private const string SessionId = "ec45cc";
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    internal static void Write(string hypothesisId, string location, string message, object? data = null)
    {
        try
        {
            var path = ResolveLogPath();
            var payload = new Dictionary<string, object?>
            {
                ["sessionId"] = SessionId,
                ["hypothesisId"] = hypothesisId,
                ["location"] = location,
                ["message"] = message,
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
            if (data is not null)
            {
                payload["data"] = data;
            }

            File.AppendAllText(path, JsonSerializer.Serialize(payload, JsonOpts) + Environment.NewLine);
        }
        catch
        {
            // intentionally empty
        }
    }

    private static string ResolveLogPath()
    {
        try
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (var i = 0; i < 22 && dir is not null; i++, dir = dir.Parent!)
            {
                var sln = Path.Combine(dir.FullName, "src", "PMTool.sln");
                if (File.Exists(sln))
                {
                    return Path.Combine(dir.FullName, "debug-ec45cc.log");
                }
            }
        }
        catch
        {
            // fall through
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AloneDev", "debug-ec45cc.log");
    }
}
