using System.Reflection;
using Spectre.Console;

AnsiConsole.Write(new FigletText("AYD"));
AnsiConsole.MarkupLine($"Version {Assembly.GetExecutingAssembly().GetName().Version} by AHpx");
AnsiConsole.MarkupLine($"GitHub: https://www.github.com/");