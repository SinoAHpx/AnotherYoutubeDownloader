using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using Downloader;
using FFMpegCore;
using Manganese.IO;
using Manganese.Text;
using Polly;
using Spectre.Console;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

#region Greeting

AnsiConsole.Write(new FigletText("AYD"));
AnsiConsole.MarkupLine($"[red]Version {Assembly.GetExecutingAssembly().GetName().Version} by AHpx[/]");
AnsiConsole.MarkupLine($"GitHub: https://github.com/SinoAHpx/AnotherYoutubeDownloader");

#endregion

#region Initializing

var ytClient = new YoutubeClient();

GlobalFFOptions.Configure(new FFOptions { BinaryFolder = await AskFFBinariesAsync() });

#endregion

#region Loop body

while (true)
{
    try
    {
        var url = AskUrl();

        var downloadedLocation = url.Contains("list=")
            ? await DownloadPlaylistAsync(url)
            : await DownloadVideoAsync(url);

        AnsiConsole.MarkupLine($"Download completed in [red]{downloadedLocation.EscapeMarkup()}[/]");
        Process.Start(new ProcessStartInfo(downloadedLocation) { UseShellExecute = true });
    }
    catch (Exception e)
    {
        AnsiConsole.WriteException(e);
    }
}

#endregion

#region Interactions

// ReSharper disable once InconsistentNaming
async Task<string> AskFFBinariesAsync()
{
    
    var ffbinaries = Directory.GetCurrentDirectory();
    if (IsFFsExist(Directory.GetCurrentDirectory()))
        ffbinaries = Directory.GetCurrentDirectory();
    else
    {
        async Task<string> DownloadFFsAsync(string url, IProgress<double> progress)
        {
            var file = Path.Combine(Path.GetTempPath(), Path.GetFileName(url));
            var builder = DownloadBuilder.New().WithUrl(url).WithFileLocation(file).Build();
            builder.DownloadProgressChanged += (_, eventArgs) =>
            {
                progress.Report(eventArgs.ProgressPercentage);
            };
            
            await builder.StartAsync();

            return file;
        }
        if (AnsiConsole.Confirm("Found no FFmpeg and FFprobe binaries, download latest version of them? "))
        {
            var branches = await new HttpClient().GetStringAsync("https://ffbinaries.com/api/v1/version/latest");
            //we don't support arm yet, neither do 32 bit Windows
            var currentSystem = GetOs();

            await Policy.Handle<Exception>()
                .RetryAsync(3,
                    (exception, i) =>
                    {
                        AnsiConsole.MarkupLine(
                            $"{exception.GetType().Name} occurred: {exception.Message.EscapeMarkup()}, retrying for {i} times");
                    })
                .ExecuteAsync(async () =>
                {
                    await AnsiConsole.Progress().StartAsync(async ctx =>
                    {
                        var task1 = ctx.AddTask("FFmpeg", maxValue: 1);
                        var task2 = ctx.AddTask("FFprobe", maxValue: 1);

                        await Parallel.ForEachAsync(new[] { task1, task2 }, async (task, _) =>
                        {
                            var url = branches.Fetch($"bin.{currentSystem}.{task.Description.ToLower()}")!;
                            var file = await DownloadFFsAsync(url, task);
                            
                            ZipFile.OpenRead(file).ExtractToDirectory(ffbinaries);
                        });
                    });
                });
        }
        else
        {
            var location = AnsiConsole.Ask<string>("Input ffmpeg and ffprobe binaries location: ");
            if (IsFFsExist(location))
                ffbinaries = location;
            else await AskFFBinariesAsync();
        }
    }

    return ffbinaries;
}

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
    var configPath = Path.Combine(Directory.GetCurrentDirectory(), "ayd.json");
    if (File.Exists(configPath))
    {
        var defaultLocation = File.ReadAllText(configPath).Fetch("DefaultLocation");
        if (!defaultLocation.IsNullOrEmpty())
            return defaultLocation;
    }

    var rawLocation = AnsiConsole.Confirm("Download to default location[yellow](./ayd)[/]? ")
        ? Path.Combine(Directory.GetCurrentDirectory(), "ayd")
        : AnsiConsole.Ask<string>("Where do you want?");

    if (AnsiConsole.Confirm("Keep that location as default? "))
    {
        File.WriteAllText(configPath, new
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
    return await Policy<IVideo>.Handle<Exception>()
        .RetryAsync(3,
            (exception, i) =>
            {
                AnsiConsole.MarkupLine(
                    $"{exception.GetType().Name} occurred: {exception.Exception.Message.EscapeMarkup()}, retrying for {i} times");
            })
        .ExecuteAsync(async () =>
        {
            var videoMetadata = await ytClient.Videos.GetAsync(url);
            AnsiConsole.MarkupLine($"Title: [red]{videoMetadata.Title.EscapeMarkup()}[/]");
            AnsiConsole.MarkupLine($"Uploader: [red]{videoMetadata.Author.ChannelTitle.EscapeMarkup()}[/]");

            return videoMetadata;
        });
}

async Task<string> DownloadVideoAsync(string url)
{
    var rawLocation = AskOutputDirectory();

    var videoMetadata = await RetrieveVideoAsync(url);
    var downloadLocation = GetExactLocation(videoMetadata, rawLocation);


    await Policy.Handle<Exception>()
        .RetryAsync(3,
            (exception, i) =>
            {
                AnsiConsole.MarkupLine(
                    $"{exception.GetType().Name} occurred: {exception.Message.EscapeMarkup()}, retrying for {i} times");
            })
        .ExecuteAsync(async () =>
        {
            if (downloadLocation.Exists)
            {
                if (AnsiConsole.Confirm($"{downloadLocation.Name.EscapeMarkup()} already exists, overwrite?"))
                    downloadLocation.Delete();
                else
                    return;
            }

            await AnsiConsole.Progress().StartAsync(async ctx =>
            {
                var progressTask = ctx.AddTask(videoMetadata.Title.EscapeMarkup(), maxValue: 1);
                var streamManifest = await ytClient.Videos.Streams.GetManifestAsync(videoMetadata.Id);
                var stream = streamManifest.GetMuxedStreams().GetWithHighestVideoQuality();

                await ytClient.Videos.Streams.DownloadAsync(stream, downloadLocation.FullName, progressTask);
            });
        });

    return rawLocation;
}

#endregion

#region Playlist

async Task<IPlaylist> RetrievePlaylistAsync(string url)
{
    return await Policy<IPlaylist>.Handle<Exception>()
        .RetryAsync(3,
            (exception, i) =>
            {
                AnsiConsole.MarkupLine(
                    $"{exception.GetType().Name} occurred: {exception.Exception.Message.EscapeMarkup()}, retrying for {i} times");
            })
        .ExecuteAsync(async () =>
        {
            var playlistMeta = await ytClient.Playlists.GetAsync(url);
            AnsiConsole.MarkupLine($"Title: [red]{playlistMeta.Title.EscapeMarkup()}[/]");
            if (playlistMeta.Author is not null)
                AnsiConsole.MarkupLine($"Uploader: [red]{playlistMeta.Author.ChannelTitle.EscapeMarkup()}[/]");

            return playlistMeta;
        });
}

async Task<string> DownloadPlaylistAsync(string url)
{
    var rawLocation = AskOutputDirectory();

    var playlistMeta = await RetrievePlaylistAsync(url);
    var location = Path.Combine(rawLocation, playlistMeta.Title.RemoveInvalidFileNameChars());
    Directory.CreateDirectory(location);

    var rawPlaylist = await ytClient.Playlists.GetVideosAsync(playlistMeta.Id);
    var selectedVideos = AnsiConsole.Prompt(new MultiSelectionPrompt<string>()
        .Title("Select videos to download")
        .Required()
        .AddChoiceGroup(playlistMeta.Title.EscapeMarkup(), rawPlaylist.Select(x => x.Title.EscapeMarkup())));

    var videos = rawPlaylist.Where(v => selectedVideos.Contains(v.Title)).ToList();

    await Policy.Handle<Exception>().RetryAsync(3).ExecuteAsync(async () =>
    {
        await AnsiConsole.Progress().StartAsync(async ctx =>
        {
            var tasks = videos.Select(v => (ctx.AddTask(v.Title, maxValue: 1), v)).ToList();

            await Parallel.ForEachAsync(tasks, async (tuple, _) =>
            {
                var (progressTask, playlistVideo) = tuple;
                var manifest = await ytClient.Videos.Streams.GetManifestAsync(playlistVideo.Id, _);
                var stream = manifest.GetMuxedStreams().GetWithHighestVideoQuality();
                var videoFile = GetExactLocation(playlistVideo, location);

                if (videoFile.Exists)
                {
                    progressTask.Value = 1;
                    return;
                }

                await ytClient.Videos.Streams.DownloadAsync(stream,
                    videoFile.FullName, progressTask);
            });
        });
    });

    var migratingQueue = videos.Select(v => GetExactLocation(v, location)).Select(x => x.FullName).ToList();
    AnsiConsole.MarkupLine($"Migrating {migratingQueue.Count} videos");
    AnsiConsole.MarkupLine($"{migratingQueue.Select(x => x.GetFileName()).Aggregate((a, b) => $"{a}{Environment.NewLine}{b}")}");
    FFMpeg.Join(Path.Combine(location, $"{playlistMeta.Title.RemoveInvalidFileNameChars()} (Migrated).mp4"), migratingQueue.ToArray());

    return location;
}

#endregion

#region Helpers

bool IsFFsExist(string folder)
{
    var executableExtension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? ".exe"
        : string.Empty;
    return File.Exists($"{folder}/ffmpeg{executableExtension}") &&
           File.Exists($"{folder}/ffprobe{executableExtension}");
}

static string GetOs()
{
    string? os = null;
    if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        os = "windows";

    if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        os = "linux";

    if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        os = "osx";

    if (os.IsNullOrEmpty())
        throw new PlatformNotSupportedException();

    return $"{os}-{(Environment.Is64BitOperatingSystem ? "64" : "32")}";
}

FileInfo GetExactLocation(IVideo videoMetadata, string downloadDirectory)
{
    Directory.CreateDirectory(downloadDirectory);
    var fileName = $"{videoMetadata.Title}.mp4";
    var filePath = Path.Combine(downloadDirectory.RemoveInvalidPathChars(),
        fileName.RemoveInvalidFileNameChars());
    return new FileInfo(filePath);
}

#endregion