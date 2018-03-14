using NADHatLIB;
using System;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

// Pour plus d'informations sur le modèle d'élément Page vierge, consultez la page https://go.microsoft.com/fwlink/?LinkId=234238

namespace NADHatWin
{
  /// <summary>
  /// Une page vide peut être utilisée seule ou constituer une page de destination au sein d'un frame.
  /// </summary>
  public sealed partial class HistoriqueSmsPAGE : Page
  {
    private void NouveauSmsBTN_Click(object sender, RoutedEventArgs e) { this.Frame.Navigate(typeof(SaisieSmsPAGE)); }
    private async void ToutEffacerBTN_Click(object sender, RoutedEventArgs e)
    {
      ContentDialog confirmationEffacerSmsDLG = new ContentDialog() { Title = "", Content = "Effacer tous les SMS ?", PrimaryButtonText = "Oui", SecondaryButtonText = "Non" };
      if (await confirmationEffacerSmsDLG.ShowAsync() == ContentDialogResult.Primary)
        await MainPage._nadHat.SupprimerTousLesSms();
    }
    private void Items_VectorChanged(IObservableVector<object> sender, Windows.Foundation.Collections.IVectorChangedEventArgs @event)
    {
      if (@event.CollectionChange == CollectionChange.ItemInserted)
        ListeSmsLVW.ScrollIntoView(ListeSmsLVW.Items[(int)@event.Index]);
    }

    public HistoriqueSmsPAGE()
    {
      this.InitializeComponent();
      MainGRD.DataContext = MainPage._nadHatViewModel;
      ListeSmsLVW.Items.VectorChanged += Items_VectorChanged;
    }
  }

  public class ListeSmsVideToVisibilityConverter : IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, string language)
    {
      if (value != null)
      {
        ListeSmsVM liste = (ListeSmsVM)value;
        return liste.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
      }
      return Visibility.Visible;
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
      throw new NotImplementedException();
    }
  }
}
