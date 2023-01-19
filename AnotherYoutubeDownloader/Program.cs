﻿using System.Reflection;
using Spectre.Console;
using YoutubeExplode;

AnsiConsole.Write(new FigletText("AYD"));
AnsiConsole.MarkupLine($"Version {Assembly.GetExecutingAssembly().GetName().Version} by AHpx");
AnsiConsole.MarkupLine($"GitHub: https://github.com/SinoAHpx/AnotherYoutubeDownloader");

var ytClient = new YoutubeClient();
while (true)
{
    #if DEBUG
    var url = "https://www.youtube.com/watch?v=ANIX12gCtyM";
    #else
    var url = AnsiConsole.Ask<string>("Input a url or video or playlist:");
    #endif
    
    if (!Uri.IsWellFormedUriString(url, UriKind.Absolute) || !url.Contains("youtube.com"))
    {
        AnsiConsole.MarkupLine("[red]Invalid url, please input again[/]");
        continue;
    }

    var video = await ytClient.Videos.GetAsync(url);
    // ytClient.Videos.Streams.GetAsync();

    // AnsiConsole.Progress().Start(c =>
    // {
    //     var task1 = c.AddTask("Downloading audio");
    //     var task2 = c.AddTask("Downloading video");
    //     while (!c.IsFinished)
    //     {
    //         Thread.Sleep(1);
    //         task1.Increment(1);
    //         task2.Increment(1);
    //     }
    // });
    
    AnsiConsole.MarkupLine("Download completed");
}