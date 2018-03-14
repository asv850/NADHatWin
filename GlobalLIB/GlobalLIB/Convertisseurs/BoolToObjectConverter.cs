using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace GlobalLIB.Convertisseurs
{
  public abstract class BoolToObjectConverter<T> : IValueConverter
  {
    public T ValeurTrue { get; set; }
    public T ValeurFalse { get; set; }
    public T ValeurNull { get; set; }
    public object Convert(object value, Type targetType, object parameter, string language)
    {
      if (value is bool)
        return (bool)value ? ValeurTrue : ValeurFalse;
      else
        return ValeurNull;
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
      throw new NotImplementedException();
    }
  }

  public class BoolToMarginConverter : BoolToObjectConverter<Thickness> { }
}
