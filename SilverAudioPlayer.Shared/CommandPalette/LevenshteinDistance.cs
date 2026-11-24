namespace SilverAudioPlayer.Shared;

/// <summary>
/// https://stackoverflow.com/a/6944095
/// </summary>
public static class LevenshteinDistance
{
    public static int Compute(string s, string t)
    {
        if (string.IsNullOrEmpty(s))
        {
            if (string.IsNullOrEmpty(t))
                return 0;
            return t.Length;
        }

        if (string.IsNullOrEmpty(t))
        {
            return s.Length;
        }

        int n = s.Length;
        int m = t.Length;
        int[] prev = new int[m + 1];
        int[] curr = new int[m + 1];

        for (int j = 0; j <= m; j++)
            prev[j] = j;

        for (int i = 1; i <= n; i++)
        {
            curr[0] = i;
            for (int j = 1; j <= m; j++)
            {
                int cost = t[j - 1] == s[i - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(prev[j] + 1, curr[j - 1] + 1), prev[j - 1] + cost);
            }

            (prev, curr) = (curr, prev);
        }

        return prev[m];
    }
}