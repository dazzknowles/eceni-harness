using System.Security.Cryptography;
using System.Text.Json;
using System.Globalization;

namespace Eceni.Harness;

internal static class HarnessCli
{
    internal static int Run(string[] arguments, TextWriter output, TextWriter error)
    {
        if (!TryParse(arguments, out CliOptions? options, error))
        {
            return 2;
        }

        CliOptions parsedOptions = options ?? throw new InvalidOperationException("Parsed options were unexpectedly absent.");

        byte[] sourceBytes;
        JsonDocument document;
        try
        {
            sourceBytes = File.ReadAllBytes(parsedOptions.DefinitionPath);
            document = JsonDocument.Parse(sourceBytes);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            error.WriteLine($"Unable to read work definition: {exception.Message}");
            return 2;
        }

        using (document)
        {
            ValidationOutcome outcome = WorkDefinitionValidator.Validate(document.RootElement);
            string runId = parsedOptions.RunId ?? Guid.NewGuid().ToString();
            string assessedAt = parsedOptions.AssessedAt ?? DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
            string definitionId = GetDefinitionId(document.RootElement);
            string sourcePath = parsedOptions.DefinitionPath.Replace('\\', '/');
            string digest = Convert.ToHexStringLower(SHA256.HashData(sourceBytes));
            string content = RunRecord.Render(
                runId,
                assessedAt,
                sourcePath,
                digest,
                definitionId,
                outcome.Findings);
            try
            {
                RunRecord.WriteNew(parsedOptions.RecordPath, content);
            }
            catch (IOException) when (File.Exists(parsedOptions.RecordPath))
            {
                error.WriteLine($"Refusing to overwrite existing run record: {parsedOptions.RecordPath}");
                return 2;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                error.WriteLine($"Unable to write run record: {exception.Message}");
                return 2;
            }

            string result = outcome.Findings.Count == 0 ? "PASS" : "FAIL";
            output.WriteLine($"{result}: {parsedOptions.RecordPath}");
            return outcome.Findings.Count == 0 ? 0 : 1;
        }
    }

    private static string GetDefinitionId(JsonElement document)
    {
        if (document.ValueKind == JsonValueKind.Object
            && document.TryGetProperty("id", out JsonElement identifier)
            && identifier.ValueKind == JsonValueKind.String)
        {
            return identifier.GetString() ?? "<missing>";
        }

        return document.ValueKind == JsonValueKind.Object ? "<missing>" : "<invalid>";
    }

    private static bool TryParse(string[] arguments, out CliOptions? options, TextWriter error)
    {
        options = null;
        if (arguments.Length < 4 || arguments[0] != "validate")
        {
            WriteUsage(error);
            return false;
        }

        string definitionPath = arguments[1];
        string? recordPath = null;
        string? runId = null;
        string? assessedAt = null;
        int index = 2;
        while (index < arguments.Length)
        {
            string option = arguments[index];
            if (index + 1 >= arguments.Length)
            {
                error.WriteLine($"Missing value for {option}.");
                WriteUsage(error);
                return false;
            }

            string value = arguments[index + 1];
            if (option == "--record" && recordPath is null)
            {
                recordPath = value;
            }
            else if (option == "--run-id" && runId is null)
            {
                runId = value;
            }
            else if (option == "--assessed-at" && assessedAt is null)
            {
                assessedAt = value;
            }
            else
            {
                error.WriteLine($"Unknown or repeated option: {option}.");
                WriteUsage(error);
                return false;
            }

            index += 2;
        }

        if (string.IsNullOrWhiteSpace(recordPath))
        {
            error.WriteLine("--record is required.");
            WriteUsage(error);
            return false;
        }

        options = new CliOptions(definitionPath, recordPath, runId, assessedAt);
        return true;
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage: eceni-harness validate <definition> --record <path> [--run-id <id>] [--assessed-at <UTC timestamp>]");
    }

    private sealed record CliOptions(
        string DefinitionPath,
        string RecordPath,
        string? RunId,
        string? AssessedAt);
}
