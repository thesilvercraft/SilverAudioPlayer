using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SilverAudioPlayer.Avalonia
{
    public class NullableReplacingConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            return values[0] ?? values[1];
        }
    }
}
