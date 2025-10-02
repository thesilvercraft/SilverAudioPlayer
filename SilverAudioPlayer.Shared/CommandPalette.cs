namespace SilverAudioPlayer.Shared;

public interface ICommandPaletteItem
{
    public string HumanName { get; }
    public string Id { get; }
    public void Execute(IPlayerEnvironment environment);
}
public record ExportedCommand(string Name, string Id);
public interface IExportCommands
{
    public IEnumerable<ExportedCommand> GetCommands();
    void RaiseCommand(ExportedCommand command);
}
public class CommandPaletteItem(string name, string id, Action<IPlayerEnvironment> exec) : ICommandPaletteItem
{
    public string HumanName { get; } = name;
    public string Id { get; } = id;
    Action<IPlayerEnvironment> _exec = exec;
    public void Execute(IPlayerEnvironment environment)
    {
        _exec(environment);
    }
}
public class ComandPalette
{
    readonly List<ICommandPaletteItem> Items=[];
    public void AddItems(IEnumerable<ICommandPaletteItem> commandPaletteItems)
    {
        Items.AddRange(commandPaletteItems);
    }
    public void AddItem(ICommandPaletteItem commandPaletteItems)
    {
        Items.Add(commandPaletteItems);
    }
    public List<ICommandPaletteItem> Search(string term)
    {
        if(string.IsNullOrWhiteSpace(term))
            return Items;
        var i = Items.OrderBy(x=>LevenshteinDistance.Compute(x.HumanName.ToLower(),term.ToLower()));
        return [.. i];
    }
}
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
        int[,] d = new int[n + 1, m + 1];

        // initialize the top and right of the table to 0, 1, 2, ...
        for (int i = 0; i <= n; d[i, 0] = i++);
        for (int j = 1; j <= m; d[0, j] = j++);

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                int min1 = d[i - 1, j] + 1;
                int min2 = d[i, j - 1] + 1;
                int min3 = d[i - 1, j - 1] + cost;
                d[i, j] = Math.Min(Math.Min(min1, min2), min3);
            }
        }
        return d[n, m];
    }
}