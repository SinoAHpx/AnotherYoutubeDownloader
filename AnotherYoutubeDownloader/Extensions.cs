namespace AnotherYoutubeDownloader;

public static class Extensions
{
    public static T Print<T>(this T t)
    {
        Console.WriteLine(t);
        return t;
    }
}