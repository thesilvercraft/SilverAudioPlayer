namespace SilverAudioPlayer.Shared;

public class CommandPalette
{
    readonly List<ICommandPaletteItem> Items = [];
    readonly Lock _lock = new();

    public void AddItems(IEnumerable<ICommandPaletteItem> commandPaletteItems)
    {
        lock (_lock)
        {
            Items.AddRange(commandPaletteItems);
        }
    }

    public void AddItem(ICommandPaletteItem commandPaletteItems)
    {
        lock (_lock)
        {
            Items.Add(commandPaletteItems);
        }
    }

    public List<ICommandPaletteItem> Search(string term)
    {
        List<ICommandPaletteItem> snapshot;
        lock (_lock)
        {
            snapshot = [.. Items];
        }

        if (string.IsNullOrWhiteSpace(term))
            return snapshot;

        var loweredTerm = term.ToLowerInvariant();
        return [.. snapshot.OrderBy(x => LevenshteinDistance.Compute(x.HumanName.ToLowerInvariant(), loweredTerm))];
    }
}