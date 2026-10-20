using System;
using System.IO;

namespace MSBuildGraphHelper.Tests;

internal static class FixturePaths
{
    public static string SampleSolutionPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Fixtures", "SampleSolution", "SampleSolution.sln"));

    public static string ProjectAPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Fixtures", "SampleSolution", "ProjectA", "ProjectA.csproj"));

    public static string ProjectC1Path =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Fixtures", "SampleSolution", "ProjectC", "C1.cs"));

    public static string ProjectD1Path =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Fixtures", "SampleSolution", "ProjectD", "D1.cs"));

    public static string ProjectX1Path =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Fixtures", "SampleSolution", "ProjectX", "X1.cs"));

    public static string ReadmeNotProjectPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Fixtures", "SampleSolution", "readme-not-a-project-file.md"));

    public static string EmptyCompileProjectPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Fixtures", "EmptyCompileProject", "EmptyCompile.csproj"));

    public static string NativeLibProjectPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Fixtures", "NativeLibFixture", "NativeLib.vcxproj"));
}
