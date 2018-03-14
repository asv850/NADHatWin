using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace GlobalLIB.Convertisseurs
{
  public class BoolToVisibilityConverter : IValueConverter
  {
    public bool Invert { get; set; }
    public object Convert(object value, Type targetType, object parameter, string language)
    {
      if (value is bool)
      {
        bool val;
        if (Invert)
          val = !(bool)value;
        else
          val = (bool)value;
        return val ? Visibility.Visible : Visibility.Collapsed;
      }
      return Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
      if (value is Visibility)
      {
        bool val = (Visibility)value == Visibility.Visible ? true : false;
        if (Invert)
          return !val;
        else
          return val;
      }
      return Invert;
    }
  }
}
