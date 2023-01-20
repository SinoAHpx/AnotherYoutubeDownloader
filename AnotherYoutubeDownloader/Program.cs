using System.Reflection;
using Manganese.Text;
using Polly;
using Spectre.Console;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

AnsiConsole.Write(new FigletText("AYD"));
AnsiConsole.MarkupLine($"[red]Version {Assembly.GetExecutingAssembly().GetName().Version} by AHpx[/]");
AnsiConsole.MarkupLine($"GitHub: https://github.com/SinoAHpx/AnotherYoutubeDownloader");

var ytClient = new YoutubeClient();
//https://www.youtube.com/watch?v=ANIX12gCtyM
while (true)
{
    var url = AnsiConsole.Ask<string>("Input a url or video or playlist:");
    
    if (!Uri.IsWellFormedUriString(url, UriKind.Absolute) || !url.Contains("youtube.com"))
    {
        AnsiConsole.MarkupLine("[red]Invalid url, please input again[/]");
        return ;
    }

    var videoMetadata = await ytClient.Videos.GetAsync(url);
    AnsiConsole.MarkupLine($"Title: [red]{videoMetadata.Title}[/]");
    AnsiConsole.MarkupLine($"Author: [red]{videoMetadata.Author.ChannelTitle}[/]");

    var rawLocation = AnsiConsole.Confirm("Download to default location[yellow](./ayd)[/]? ")
        ? "./ayd"
        : AnsiConsole.Ask<string>("Where do you want?");

    if (rawLocation.IsNullOrEmpty())
        return;

    var downloadDirectory = Directory.CreateDirectory(rawLocation);
    var downloadLocation = GetExactLocation(videoMetadata, downloadDirectory);

    if (downloadLocation.Exists)
    {
        if (AnsiConsole.Confirm($"{downloadLocation.Name} already exists, overwrite?"))
            downloadLocation.Delete();
        else
            return;
    }

    await Policy.Handle<Exception>()
        .RetryAsync(3, (exception, i) =>
        {
            AnsiConsole.MarkupLine($"{exception.GetType().Name} occurred: {exception.Message}, retrying for {i} times");
        })
        .ExecuteAsync(async () =>
        {
            await AnsiConsole.Progress().StartAsync(async ctx =>
            {
                var t1 = ctx.AddTask(videoMetadata.Title, maxValue: 1);
                var streamManifest = await ytClient.Videos.Streams.GetManifestAsync(videoMetadata.Id);
                var stream = streamManifest.GetMuxedStreams().GetWithHighestVideoQuality();
                var progress = new Progress<double>();
                progress.ProgressChanged += (_, args) =>
                {
                    t1.Value = args;
                };

                await ytClient.Videos.Streams.DownloadAsync(stream, downloadLocation.FullName, progress);
            });
        });
    

    AnsiConsole.MarkupLine("Download completed");
}

FileInfo GetExactLocation(Video videoMetadata, DirectoryInfo downloadDirectory)
{
    var fileName = $"{videoMetadata?.Author.ChannelTitle} - {videoMetadata?.Title}.mp4";
    var filePath = Path.Combine(downloadDirectory!.FullName, fileName);
    return new FileInfo(filePath);
} 

public static class Extensions
{
    public static T Print<T>(this T t)
    {
        Console.WriteLine(t);
        return t;
    }
}