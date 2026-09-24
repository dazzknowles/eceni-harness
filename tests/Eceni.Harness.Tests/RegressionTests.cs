using System.Text.Json;
using System.Text.Json.Nodes;

namespace Eceni.Harness.Tests;

internal static class RegressionTests
{
    private static readonly string ExamplePath = Path.Combine(AppContext.BaseDirectory, "TestData", "solar-optimiser-5.json");

    internal static int RunAll(TextWriter output)
    {
        (string Name, Action Test)[] tests =
        [
            ("Solar issue #5 preflight is valid", SolarIssue5PreflightIsValid),
            ("Missing authority fails closed", MissingAuthorityFailsClosed),
            ("Grant and prohibition conflict fails closed", GrantAndProhibitionConflictFailsClosed),
            ("Change repository requires explicit write grant", ChangeRepositoryRequiresExplicitWriteGrant),
            ("Unpinned Git input is rejected", UnpinnedGitInputIsRejected),
            ("Adopted baseline must match authoritative input", AdoptedBaselineMustMatchAuthoritativeInput),
            ("Independent verifier differs from implementer", IndependentVerifierDiffersFromImplementer),
            ("Open follow-up requires acceptance conditions", OpenFollowUpRequiresAcceptanceConditions),
            ("Valid definition writes reproducible pass record", ValidDefinitionWritesReproduciblePassRecord),
            ("Invalid definition writes fail record", InvalidDefinitionWritesFailRecord),
            ("Existing record is not overwritten", ExistingRecordIsNotOverwritten),
        ];

        int failures = 0;
        foreach ((string name, Action test) in tests)
        {
            try
            {
                test();
                output.WriteLine($"PASS {name}");
            }
            catch (Exception exception)
            {
                failures++;
                output.WriteLine($"FAIL {name}: {exception.Message}");
            }
        }

        output.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed.");
        return failures == 0 ? 0 : 1;
    }

    private static void SolarIssue5PreflightIsValid()
    {
        ValidationOutcome outcome = Validate(ReadExample());
        Equal(0, outcome.Findings.Count, "Expected no findings.");
        Equal("solar-optimiser-issue-5-preflight-1", outcome.Definition?.Id, "Expected a typed definition.");
    }

    private static void MissingAuthorityFailsClosed()
    {
        JsonObject document = ReadExample();
        ObjectAt(document, "authority").Remove("grants");
        ContainsCode(Validate(document), "WD009");
    }

    private static void GrantAndProhibitionConflictFailsClosed()
    {
        JsonObject document = ReadExample();
        JsonArray grants = ArrayAt(ObjectAt(document, "authority"), "grants");
        grants.Add(new JsonObject
        {
            ["id"] = "BAD-GRANT",
            ["repository"] = "https://github.com/dazzknowles/solar-optimiser",
            ["capability"] = "repository.write",
            ["scope"] = "Contradicts the explicit prohibition.",
            ["source"] = "test:contradiction",
        });
        ContainsCode(Validate(document), "AUTH001");
    }

    private static void ChangeRepositoryRequiresExplicitWriteGrant()
    {
        JsonObject document = ReadExample();
        JsonObject execution = ObjectAt(document, "execution");
        JsonArray references = ArrayAt(execution, "reference_only_repositories");
        references.Remove(JsonValue.Create("https://github.com/dazzknowles/solar-optimiser"));
        ArrayAt(execution, "change_repositories").Add("https://github.com/dazzknowles/solar-optimiser");
        ValidationOutcome outcome = Validate(document);
        ContainsCode(outcome, "AUTH003");
        ContainsCode(outcome, "AUTH004");
    }

    private static void UnpinnedGitInputIsRejected()
    {
        JsonObject document = ReadExample();
        ObjectAt(ArrayAt(document, "authoritative_inputs"), 0)["revision"] = "main";
        ContainsCode(Validate(document), "WD014");
    }

    private static void AdoptedBaselineMustMatchAuthoritativeInput()
    {
        JsonObject document = ReadExample();
        ObjectAt(ObjectAt(document, "governance"), "baseline")["commit"] = new string('0', 40);
        ContainsCode(Validate(document), "WD016");
    }

    private static void IndependentVerifierDiffersFromImplementer()
    {
        JsonObject document = ReadExample();
        JsonObject acceptance = ObjectAt(document, "acceptance");
        ObjectAt(acceptance, "independent_verification")["verifier_role"] = acceptance["implementer_role"]?.DeepClone();
        ContainsCode(Validate(document), "ACC007");
    }

    private static void OpenFollowUpRequiresAcceptanceConditions()
    {
        JsonObject document = ReadExample();
        ObjectAt(ArrayAt(ObjectAt(document, "obligations"), "follow_ups"), 0)["acceptance_conditions"] = new JsonArray();
        ContainsCode(Validate(document), "OBL003");
    }

    private static void ValidDefinitionWritesReproduciblePassRecord()
    {
        using TemporaryDirectory temporary = new();
        string record = Path.Combine(temporary.Path, "valid-run.md");
        int result = HarnessCli.Run(
            ["validate", ExamplePath, "--record", record, "--run-id", "test-run", "--assessed-at", "2026-09-24T18:00:00Z"],
            TextWriter.Null,
            TextWriter.Null);
        Equal(0, result, "Expected a successful exit code.");
        string content = File.ReadAllText(record);
        Contains(content, "Result: **Pass**");
        Contains(content, "test-run");
        Contains(content, "preflight evidence only");
    }

    private static void InvalidDefinitionWritesFailRecord()
    {
        using TemporaryDirectory temporary = new();
        JsonObject document = ReadExample();
        ObjectAt(document, "authority")["grants"] = new JsonArray();
        string invalid = Path.Combine(temporary.Path, "invalid.json");
        File.WriteAllText(invalid, document.ToJsonString());
        string record = Path.Combine(temporary.Path, "failed-run.md");
        int result = HarnessCli.Run(
            ["validate", invalid, "--record", record, "--run-id", "failed-run", "--assessed-at", "2026-09-24T18:01:00Z"],
            TextWriter.Null,
            TextWriter.Null);
        Equal(1, result, "Expected a validation-failure exit code.");
        Contains(File.ReadAllText(record), "Result: **Fail**");
    }

    private static void ExistingRecordIsNotOverwritten()
    {
        using TemporaryDirectory temporary = new();
        string record = Path.Combine(temporary.Path, "existing.md");
        File.WriteAllText(record, "existing");
        int result = HarnessCli.Run(["validate", ExamplePath, "--record", record], TextWriter.Null, TextWriter.Null);
        Equal(2, result, "Expected an I/O exit code.");
        Equal("existing", File.ReadAllText(record), "Existing content changed.");
    }

    private static JsonObject ReadExample()
    {
        return JsonNode.Parse(File.ReadAllText(ExamplePath))?.AsObject()
            ?? throw new InvalidOperationException("Example was not a JSON object.");
    }

    private static ValidationOutcome Validate(JsonObject document)
    {
        using JsonDocument parsed = JsonDocument.Parse(document.ToJsonString());
        return WorkDefinitionValidator.Validate(parsed.RootElement);
    }

    private static JsonObject ObjectAt(JsonObject parent, string property)
    {
        return parent[property]?.AsObject()
            ?? throw new InvalidOperationException($"{property} was not an object.");
    }

    private static JsonObject ObjectAt(JsonArray parent, int index)
    {
        return parent[index]?.AsObject()
            ?? throw new InvalidOperationException($"Item {index} was not an object.");
    }

    private static JsonArray ArrayAt(JsonObject parent, string property)
    {
        return parent[property]?.AsArray()
            ?? throw new InvalidOperationException($"{property} was not an array.");
    }

    private static void ContainsCode(ValidationOutcome outcome, string code)
    {
        if (!outcome.Findings.Any((Finding finding) => finding.Code == code))
        {
            throw new InvalidOperationException($"Expected finding {code}; received {string.Join(", ", outcome.Findings.Select((Finding finding) => finding.Code))}.");
        }
    }

    private static void Contains(string content, string expected)
    {
        if (!content.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected content to contain '{expected}'.");
        }
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected '{expected}', received '{actual}'.");
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        internal TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"eceni-harness-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, true);
        }
    }
}
