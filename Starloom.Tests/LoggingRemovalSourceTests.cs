using Xunit;

namespace StarLoom.Tests;

public sealed class LoggingRemovalSourceTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void Production_Source_Does_Not_Use_Dalamud_Log_Output()
    {
        var sourceFiles = Directory
            .EnumerateFiles(RepoRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}.worktrees{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}_temp{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Starloom.Tests{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

        var filesWithLogs = sourceFiles
            .Where(path => File.ReadAllText(path).Contains("Svc.Log.", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(RepoRoot, path));

        Assert.Empty(filesWithLogs);
    }
}
