namespace MSBuildGraphHelper;

public sealed class CliOptions
{
    public HelperRunMode Mode { get; set; } = HelperRunMode.Closure;
    public string SolutionPath { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public string Configuration { get; set; } = "Debug";
    public string SolutionPlatform { get; set; } = "x64";
    public string MSBuildPath { get; set; } = "";
    public string VCTargetsPath { get; set; } = "";
    public string? OutputPath { get; set; }
}
