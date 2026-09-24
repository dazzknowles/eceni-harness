using System.Reflection;
using System.Text;
using System.Globalization;

namespace Eceni.Harness;

internal static class RunRecord
{
    internal static string Render(
        string runId,
        string assessedAt,
        string sourcePath,
        string sourceDigest,
        string definitionId,
        IReadOnlyList<Finding> findings)
    {
        string result = findings.Count == 0 ? "Pass" : "Fail";
        string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown";
        StringBuilder content = new();
        content.AppendLine(CultureInfo.InvariantCulture, $"# Eceni Harness run: {runId}");
        content.AppendLine();
        content.AppendLine(CultureInfo.InvariantCulture, $"- Result: **{result}**");
        content.AppendLine(CultureInfo.InvariantCulture, $"- Criterion assessed: `{definitionId}` conforms structurally to `{WorkDefinitionValidator.SchemaVersion}`");
        content.AppendLine(CultureInfo.InvariantCulture, $"- Assessed at: `{assessedAt}`");
        content.AppendLine(CultureInfo.InvariantCulture, $"- Responsible assessment: `eceni-harness {version}` deterministic validator");
        content.AppendLine(CultureInfo.InvariantCulture, $"- Input: `{sourcePath}`");
        content.AppendLine(CultureInfo.InvariantCulture, $"- Input SHA-256: `{sourceDigest}`");
        content.AppendLine();
        content.AppendLine("## Evidence");
        content.AppendLine();
        if (findings.Count > 0)
        {
            content.AppendLine(CultureInfo.InvariantCulture, $"Validation produced {findings.Count} finding(s):");
            content.AppendLine();
            foreach (Finding finding in findings)
            {
                content.AppendLine(CultureInfo.InvariantCulture, $"- **{finding.Code}** `{finding.Path}` — {finding.Message}");
            }
        }
        else
        {
            content.AppendLine("The input passed every implemented structural, source-pinning, authority,");
            content.AppendLine("acceptance, independent-verification, and obligation-completeness check.");
            content.AppendLine("This is preflight evidence only; it is not implementation or product acceptance evidence.");
        }

        content.AppendLine();
        content.AppendLine("## Result boundary");
        content.AppendLine();
        content.AppendLine("`Pass` means only that the supplied definition satisfied this validator. It does not");
        content.AppendLine("establish that cited authority is authentic, that evidence is substantively adequate,");
        content.AppendLine("or that the target work has been implemented or accepted.");
        return content.ToString();
    }

    internal static void WriteNew(string path, string content)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using StreamWriter writer = new(stream, new UTF8Encoding(false));
        writer.NewLine = "\n";
        writer.Write(content);
    }
}
