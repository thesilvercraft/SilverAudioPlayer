namespace SilverCraft.Ekorensis;

/// <summary>
///     A numeric up-down input element
///     <seealso href="https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.numericupdown?view=windowsdesktop-7.0" />
/// </summary>
public interface IConfigurableNumericUpDown : IConfigurableElement
{
    public decimal Value { get; set; }
    public decimal Minimum { get; }
    public decimal Maximum { get; }
    public decimal Increment { get; }
    public int DecimalPlaces { get; }
}
