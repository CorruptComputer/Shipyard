using System.CommandLine;

namespace Shipyard;

/// <summary>
///   Application entry point
/// </summary>
public static class Program
{
    /// <summary>
    ///   The main entry point for the application.
    /// </summary>
    /// <param name="args">Arguments to provide</param>
    public static async Task<int> Main(string[] args)
    {
        RootCommand rootCommand = new()
        {
            Description = "Shipyard - .NET Packaging Tool for Linux",
            Action = new ShipyardAction()
        };

        foreach (Option option in ShipyardAction.Options)
        {
            rootCommand.Add(option);
        }

        return await rootCommand.Parse(args).InvokeAsync();
    }
}
