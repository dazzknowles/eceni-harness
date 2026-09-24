namespace Eceni.Harness;

internal sealed record Finding(string Code, string Path, string Message) : IComparable<Finding>
{
    public int CompareTo(Finding? other)
    {
        if (other is null)
        {
            return 1;
        }

        int codeComparison = string.Compare(Code, other.Code, StringComparison.Ordinal);
        if (codeComparison != 0)
        {
            return codeComparison;
        }

        int pathComparison = string.Compare(Path, other.Path, StringComparison.Ordinal);
        return pathComparison != 0
            ? pathComparison
            : string.Compare(Message, other.Message, StringComparison.Ordinal);
    }
}
