using CatBox.NET;
using Microsoft.Extensions.Options;

namespace SilverAudioPlayer.DiscordRP;

public class RealOptions : IOptions<CatBoxConfig>
{
    public CatBoxConfig Value { get; init; }
}