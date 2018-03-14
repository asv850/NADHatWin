using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

// Pour en savoir plus sur le modèle d'élément Contrôle utilisateur, consultez la page https://go.microsoft.com/fwlink/?LinkId=234236

namespace NADHatWin
{
  public sealed partial class SmsUCTRL : UserControl
  {
    public SmsUCTRL()
    {
      this.InitializeComponent();
    }
  }

  public class DateTimeOffsetToSmsUCTRLConverter : IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, string language)
    {
      if ((value == null) || !(value is DateTimeOffset) || ((DateTimeOffset)value == DateTimeOffset.MinValue))
        return "";
      else
        return ((DateTimeOffset)value).ToString("G");
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
      throw new NotImplementedException();
    }
  }

 
}
