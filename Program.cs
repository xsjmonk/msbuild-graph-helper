using System;
using System.IO;

namespace MSBuildGraphHelper;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var options = CliParser.Parse(args);
            var json = HelperRunner.RunToJson(options);
            if (!string.IsNullOrWhiteSpace(options.OutputPath))
            {
                File.WriteAllText(options.OutputPath, json, System.Text.Encoding.UTF8);
            }
            else
            {
                Console.Out.Write(json);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            if (ex.InnerException != null)
            {
                Console.Error.WriteLine(ex.InnerException.Message);
            }

            return 1;
        }
    }
}
