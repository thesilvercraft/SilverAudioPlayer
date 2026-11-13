using System.Globalization;
using System.Text.RegularExpressions;

namespace SilverAudioPlayer.Shared.Metadata;

/// <summary>
/// Provides static methods for parsing lyrics formatted in LRC format.
/// </summary>
/// <remarks>
/// Implements the Core format and part of the Enhanced format
/// https://en.wikipedia.org/wiki/LRC_(file_format)
/// </remarks>
public static class LrcParser
{
    private static readonly Regex EnhancedTagRegex = new(@"<([^>]+)>", RegexOptions.Compiled);

    public static LrcFile Parse(string lyrics)
    {
        LrcFile file = new();
        var lrcLines = lyrics.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        foreach (var rawLine in lrcLines)
        {
            var line = rawLine.Trim();
            if (TryParse(line, out var phrases, file))
            {
                file.AddRange(phrases);
            }
            else if (line.StartsWith("[offset:") && line.EndsWith(']') &&
                     int.TryParse(line[8..^1].Trim(), CultureInfo.InvariantCulture, out var offset))
            {
                file.OffsetInMs = -offset;
            }
            else if (line.StartsWith('[') && line.EndsWith(']'))
            {
                file.AdditionalData.Add(line);
            }
        }

        return file;
    }

    /// <summary>
    /// Checks if a string contains correctly balanced and structured bracket markers, typically used to validate the content following an LRC time stamp.
    /// </summary>
    /// <param name="x">The substring of the line containing the lyric text that needs structural validation.</param>
    /// <returns>Returns true if the brackets in the input string are considered balanced and structurally valid according to LRC format rules; otherwise, returns false.</returns>
    public static bool IsBalancedAndContainsAdditional(string x)
    {
        var bracketL = 0;
        foreach (var t in x)
        {
            if (t == '<')
            {
                if (bracketL == 1)
                {
                    return false;
                }

                bracketL = 1;
            }

            if (t != '>') continue;
            if (bracketL == 1)
            {
                bracketL = -1;
            }
            else
            {
                return false;
            }
        }

        return bracketL == -1;
    }

    private static bool TryParseTime(string time, out long timeInMS)
    {
        if (time.IndexOf(':') is { } msep and >= 0
            && int.TryParse(time[0..msep], CultureInfo.InvariantCulture, out var m)
            && time.IndexOf('.') is { } ssep and >= 0
            && int.TryParse(time[(msep + 1)..ssep], CultureInfo.InvariantCulture, out var s)
            && double.TryParse('0' + time[(ssep)..], CultureInfo.InvariantCulture, out var h))
        {
            timeInMS = (int)(h * 1000) + (((m * 60) + s) * 1000);
            return true;
        }

        timeInMS = 0;
        return false;
    }

    /// <summary>
    /// Attempts to parse a single line of text from an LRC file that contains time-stamped lyrics.
    /// </summary>
    /// <param name="line">The string line read from the source LRC content.</param>
    /// <param name="lp">When this method returns, this parameter contains a list of parsed lyric phrases if successful; otherwise, it is null.</param>
    /// <param name="file">The overall LRC file object used to access global offset information for time calculation.</param>
    /// <returns>Returns true if the line was successfully identified and parsed as a structured lyric phrase marker; otherwise, returns false.</returns>
    public static bool TryParse(string line, out List<LyricPhrase>? lp, LrcFile file)
    {
        line = line.Trim();
        if (line.Length >= 9 && line[0] == '[')
        {
            var r = line.IndexOf(']');
            if (r > 1)
            {
                var time = line[1..r];
                if (TryParseTime(time, out var timeMs))
                {
                    lp = new List<LyricPhrase>();
                    var restOfLine = line[(r + 1)..];
                    if (IsBalancedAndContainsAdditional(restOfLine))
                    {
                        var matches = EnhancedTagRegex.Matches(restOfLine);
                        var currentPhraseTime = timeMs;
                        var lastIndex = 0;

                        foreach (Match m in matches)
                        {
                            var textBefore = restOfLine[lastIndex..m.Index];
                            if (textBefore.Length > 0 || lp.Count > 0)
                            {
                                lp.Add(new LyricPhrase(currentPhraseTime + file.OffsetInMs, textBefore));
                            }

                            if (TryParseTime(m.Groups[1].Value, out var tagTime))
                            {
                                currentPhraseTime = tagTime;
                            }

                            lastIndex = m.Index + m.Length;
                        }

                        var textAfter = restOfLine[lastIndex..];
                        lp.Add(new LyricPhrase(currentPhraseTime + file.OffsetInMs, textAfter + "\n"));
                    }
                    else
                    {
                        lp.Add(new(timeMs + file.OffsetInMs, restOfLine + "\n"));
                    }

                    if (lp.Count != 0)
                    {
                        return true;
                    }
                }
            }
        }

        lp = null;
        return false;
    }
}

public class LrcFile : List<LyricPhrase>
{
    public List<string> AdditionalData { get; set; } = new();
    public int OffsetInMs { get; set; } = 0;
}
