namespace SilverAudioPlayer.Shared;
/// <summary>
/// The shared instance of a <see cref="System.Net.Http.HttpClient"/>
/// </summary>
public static class HttpClient
{
    public static readonly System.Net.Http.HttpClient Client = new();
}