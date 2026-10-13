using System.Globalization;

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
    public static LrcFile Parse(string lyrics)
    {
        LrcFile file = new();
        var lrcLines = lyrics.Split("\n");
        foreach (var line in lrcLines)
        {
            if (TryParse(line, out var phrases, file))
            {
                file.AddRange(phrases);
            }
            else if (line.StartsWith("[offset:") && line.EndsWith(']') && int.TryParse(line[8..^1].Trim(), CultureInfo.InvariantCulture, out var offset))
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
            && double.TryParse('0' + time[(ssep)..], CultureInfo.InvariantCulture, out var h)) //Apparently modern day tools cannot follow the simple MM:SS.XX (where XX is centisecond, where 01 is 10ms) and some tools prefer saying .981 (instead of just rounding it up to .98) 
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
        if (line.Length > 10 && line[0] == '[')
        {
            lp = new();
            var r = line.IndexOf(']');
            var time = line[1..r];
            if (TryParseTime(time, out var timeMs))
            {
                var restOfLine = line[(r + 1)..];
                if (IsBalancedAndContainsAdditional(restOfLine))
                {
                    int prevleft = 0;
                    while (restOfLine.Length != 0)
                    {
                        int tillnextLeft = restOfLine.IndexOf('<');
                        if (tillnextLeft == -1)
                        {
                            tillnextLeft = restOfLine.Length;
                        }

                        if (tillnextLeft == 0)
                        {
                            tillnextLeft = restOfLine.IndexOf('<', 1);
                            if (tillnextLeft == -1)
                            {
                                tillnextLeft = restOfLine.Length;
                            }
                            var right = restOfLine.IndexOf('>');
                            if (right != -1)
                            {
                                time = restOfLine[1..right];
                                if (TryParseTime(time, out timeMs))
                                {
                                }
                            }

                            prevleft = right + 1;
                        }
                        lp.Add(new(timeMs + file.OffsetInMs
                            , restOfLine[prevleft..tillnextLeft].TrimStart()));
                        restOfLine = restOfLine[tillnextLeft..];
                    }

                    lp.Last().Content += '\n';

                }
                else
                {
                    lp.Add(new(timeMs + file.OffsetInMs
                        , restOfLine + "\n"));
                }
            }

            if (lp.Count != 0)
            {
                return true;
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
