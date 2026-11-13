using System;

namespace SilverCraft.Ekorensis;

public class SimpleNumericUpDown : IConfigurableNumericUpDown
{
    public SimpleNumericUpDown() { }

    public SimpleNumericUpDown(
        decimal value,
        decimal minimum = 0,
        decimal maximum = 100,
        decimal increment = 1,
        int decimalPlaces = 0,
        Action<decimal>? onValueChanged = null)
    {
        _value = value;
        _min = minimum;
        _max = maximum;
        _increment = increment;
        _decimalPlaces = decimalPlaces;
        ValueChanged = onValueChanged;
    }

    public Func<decimal>? GetValue { get; set; }
    public Action<decimal>? ValueChanged { get; set; }
    public Func<decimal>? GetMinimum { get; set; }
    public Func<decimal>? GetMaximum { get; set; }
    public Func<decimal>? GetIncrement { get; set; }
    public Func<int>? GetDecimalPlaces { get; set; }

    private decimal _value;
    public decimal Value
    {
        get => GetValue != null ? GetValue() : _value;
        set
        {
            _value = value;
            ValueChanged?.Invoke(value);
        }
    }

    private decimal _min = 0;
    public decimal Minimum
    {
        get => GetMinimum != null ? GetMinimum() : _min;
        set => _min = value;
    }

    private decimal _max = 100;
    public decimal Maximum
    {
        get => GetMaximum != null ? GetMaximum() : _max;
        set => _max = value;
    }

    private decimal _increment = 1;
    public decimal Increment
    {
        get => GetIncrement != null ? GetIncrement() : _increment;
        set => _increment = value;
    }

    private int _decimalPlaces = 0;
    public int DecimalPlaces
    {
        get => GetDecimalPlaces != null ? GetDecimalPlaces() : _decimalPlaces;
        set => _decimalPlaces = value;
    }
}
