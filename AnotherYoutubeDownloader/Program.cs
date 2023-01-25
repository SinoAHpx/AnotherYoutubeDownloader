using System.Reflection;
using Manganese.Text;
using Polly;
using Spectre.Console;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

#region Greeting

AnsiConsole.Write(new FigletText("AYD"));
AnsiConsole.MarkupLine($"[red]Version {Assembly.GetExecutingAssembly().GetName().Version} by AHpx[/]");
AnsiConsole.MarkupLine($"GitHub: https://github.com/SinoAHpx/AnotherYoutubeDownloader");

#endregion

#region Initializing

var ytClient = new YoutubeClient();


#endregion

#region Loop body

while (true)
{
    var url = AskUrl();

    if (url.Contains("list="))
        await DownloadVideoAsync(url);
    else
        await DownloadPlaylistAsync(url);
    
    AnsiConsole.MarkupLine("Download completed");
}

#endregion

#region Interactions

string AskUrl()
{
    var url = AnsiConsole.Ask<string>("Input a url or video or playlist:");
    
    if (!Uri.IsWellFormedUriString(url, UriKind.Absolute) || !url.Contains("youtube.com"))
    {
        AnsiConsole.MarkupLine("[red]Invalid url, please input again[/]");
        return AskUrl();
    }

    return url;
}

string AskOutputDirectory()
{
    const string configPath = "./ayd.json";
    if (File.Exists(configPath))
    {
        var defaultLocation = File.ReadAllText(configPath).Fetch("DefaultLocation");
        if (!defaultLocation.IsNullOrEmpty())
            return defaultLocation;

    }
    
    var rawLocation = AnsiConsole.Confirm("Download to default location[yellow](./ayd)[/]? ")
        ? "./ayd"
        : AnsiConsole.Ask<string>("Where do you want?");

    if (AnsiConsole.Confirm("Keep that location as default? "))
    {
        File.WriteAllText("./ayd.json", new
        {
            DefaultLocation = rawLocation
        }.ToJsonString());

        AnsiConsole.MarkupLine("New default location saved, you can delete file [yellow]./ayd.json[/] to modify it.");
    }
    
    if (rawLocation.IsNullOrEmpty())
        return AskOutputDirectory();

    return rawLocation;
}

#endregion

#region Video

async Task<IVideo> RetrieveVideoAsync(string url)
{
    var videoMetadata = await ytClient!.Videos.GetAsync(url);
    AnsiConsole.MarkupLine($"Title: [red]{videoMetadata.Title}[/]");
    AnsiConsole.MarkupLine($"Author: [red]{videoMetadata.Author.ChannelTitle}[/]");

    return videoMetadata;
}

async Task DownloadVideoAsync(string url)
{
    var videoMetadata = await RetrieveVideoAsync(url);
    
    var rawLocation = AskOutputDirectory();
    
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
}

#endregion


#region Playlist

async Task DownloadPlaylistAsync(string url)
{
    //todo: make it selectable for videos in a playlist to download
}

#endregion


#region Helpers

FileInfo GetExactLocation(IVideo videoMetadata, DirectoryInfo downloadDirectory)
{
    var fileName = $"{videoMetadata?.Author.ChannelTitle} - {videoMetadata?.Title}.mp4";
    var filePath = Path.Combine(downloadDirectory.FullName.RemoveInvalidPathChars(),
        fileName.RemoveInvalidFileNameChars());
    return new FileInfo(filePath);
}

#endregion