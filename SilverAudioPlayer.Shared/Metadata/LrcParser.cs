using System.Diagnostics;

namespace SilverAudioPlayer.Shared.Metadata;

public static class LrcParser
{
    public static LrcFile Parse(string lyrics)
    {
        LrcFile file = new();
        var lrcLines = lyrics.Split(Environment.NewLine);
        foreach (var line in lrcLines)
        {
            if (TryParse(line, out var phrases, file))
            {
                file.AddRange(phrases);
            }
            else if(line.StartsWith("[offset:") && line.EndsWith(']') && int.TryParse(line[8..^1].Trim(), out var  offset))
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

    public static bool IsBalancedAndContainsAdditional(string x)
    {
        int
            bracketL = 0;
        for (int i = 0; i < x.Length; i++)
        {
            if (x[i] == '<')
            {
                if (bracketL == 1)
                {
                    return false;
                }
                else
                {
                    bracketL = 1;
                }
            }
            if (x[i] == '>')
            {
                if (bracketL == 1)
                {
                    bracketL = -1;
                }
                else
                {
                    return false;
                }
            }
        }

        return bracketL==-1;
    }

    private static bool TryParseTime(string time, out long timeInMS)
    {
        if (time.IndexOf(':') is { } msep and >= 0
            && int.TryParse(time[0..msep], out var m)
            && time.IndexOf('.') is { } ssep and >= 0
            && int.TryParse(time[(msep + 1)..ssep], out var s)
            && double.TryParse('0'+time[(ssep )..], out var h)) //Apparently modern day tools cannot follow the simple MM:SS:XX (where XX is centisecond, where 01 is 10ms) and some tools prefer saying .981 (instead of just rounding it up to .98) 
        {
            timeInMS = (int)(h* 1000) + (((m * 60) + s) * 1000);
            return true;
        }

        timeInMS = 0;
        return false;
    }
    public static bool TryParse(string line, out List<LyricPhrase>? lp, LrcFile file)
    {
        line = line.Trim();
        if (line.Length>10 && line[0] == '[')
        {
            lp = new();
            var r = line.IndexOf(']');
            var time = line[1..r];
            if (TryParseTime(time,  out  var timeMs))
            {
                var restOfLine = line[(r+1)..];
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
                            tillnextLeft = restOfLine.IndexOf('<',1);
                            if (tillnextLeft == -1)
                            {
                                tillnextLeft = restOfLine.Length;
                            }
                            var right = restOfLine.IndexOf('>');
                            if (right != -1)
                            {
                                time = restOfLine[1..right];
                                if(TryParseTime(time,  out timeMs))
                                {
                                }
                            }

                            prevleft = right+1;
                        }
                        lp.Add(new(timeMs+file.OffsetInMs
                            , restOfLine[prevleft..tillnextLeft].TrimStart()));
                        restOfLine = restOfLine[tillnextLeft..];
                    }

                    lp.Last().Content+='\n';

                }
                else
                {
                    lp.Add(new(timeMs+file.OffsetInMs
                        , restOfLine+"\n"));
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
    public int OffsetInMs { get; set; } =0;
}
