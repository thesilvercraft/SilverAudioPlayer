using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
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

    public class RelativePointConverter : IValueConverter
    {
       
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                return new RelativePoint(d, 0d, RelativeUnit.Absolute);
            }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
   
}
