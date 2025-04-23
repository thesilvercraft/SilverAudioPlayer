using System.Collections.Generic;
using System.Text;

namespace SilverAudioPlayer.Any.Sync;

public static class Formatter
{
    public static string Format(string input, Dictionary<string, string> arguments)
    {
        var stringBuilder = new StringBuilder(input);
        foreach (var argument in arguments)
        {
            stringBuilder.Replace($"{{{argument.Key}}}", argument.Value);
        }
        return stringBuilder.ToString();

    }
}