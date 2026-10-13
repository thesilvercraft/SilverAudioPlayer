namespace SilverAudioPlayer.Shared;

public class CommandPalette
{
    readonly List<ICommandPaletteItem> Items = [];
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
        if (string.IsNullOrWhiteSpace(term))
            return Items;
        var i = Items.OrderBy(x => LevenshteinDistance.Compute(x.HumanName.ToLower(), term.ToLower()));
        return [.. i];
    }
}