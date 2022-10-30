using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using Windows.Foundation;

namespace SilverAudioPlayer.WinUI;

public class NullableReplacingConverter : IMultiValueConverter
{
  
    object IMultiValueConverter.Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        return values[0] ?? values[1];
    }

    object[] IMultiValueConverter.ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class RelativePointConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d) return new Point(d, 0d);
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
public static class MultiBindingHelper
{ 
    public static string OneOrOther(string a, string b)
    {
        return a ?? b;
    }
}
