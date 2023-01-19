using System.Reflection;
using Spectre.Console;

AnsiConsole.Write(new FigletText("AYD"));
AnsiConsole.MarkupLine($"Version {Assembly.GetExecutingAssembly().GetName().Version} by AHpx");
AnsiConsole.MarkupLine($"GitHub: https://github.com/SinoAHpx/AnotherYoutubeDownloader");

while (true)
{
    var url = AnsiConsole.Ask<string>("Input a url or video or playlist:");
    // if (!Uri.IsWellFormedUriString(url, UriKind.Absolute) || !url.Contains("youtube.com"))
    // {
    //     AnsiConsole.MarkupLine("[red]Invalid url, please input again[/]");
    //     continue;
    // }

    AnsiConsole.Progress().Start(c =>
    {
        var task1 = c.AddTask("Downloading audio");
        var task2 = c.AddTask("Downloading video");
        while (!c.IsFinished)
        {
            Thread.Sleep(1);
            task1.Increment(1);
            task2.Increment(1);
        }
    });
    
    AnsiConsole.MarkupLine("Download completed");
}