using CatBox.NET;
using Microsoft.Extensions.Options;

namespace SilverAudioPlayer.DiscordRP;

public class RealOptions : IOptions<CatboxOptions>
{
    public CatboxOptions Value { get; init; }
}