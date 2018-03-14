using NADHatLIB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// Pour plus d'informations sur le modèle d'élément Page vierge, consultez la page https://go.microsoft.com/fwlink/?LinkId=234238

namespace NADHatWin
{
  /// <summary>
  /// Une page vide peut être utilisée seule ou constituer une page de destination au sein d'un frame.
  /// </summary>
  public sealed partial class SaisieSmsPAGE : Page
  {
    private SaisieSms _sms = null;
    private void RetourBTN_Click(object sender, RoutedEventArgs e) { this.Frame.Navigate(typeof(HistoriqueSmsPAGE)); }
    private async void EnvoyerSmsBTN_Click(object sender, RoutedEventArgs e)
    {
      this.IsEnabled = false;
      try
      {
        MainPage._mainPageInstance.AfficherStatut("Enregistrement SMS...");
        int noSms = await MainPage._nadHat.EnregistrerSms(_sms.NumeroTelephone, NADHat.TODA_TYPES[0], _sms.Texte);
        MainPage._mainPageInstance.AfficherStatut("Code retour Enregistrement SMS = " + noSms.ToString() + ".");
        if (noSms > 0)
        {
          MainPage._mainPageInstance.AfficherStatut("SMS enregistré en no " + noSms.ToString() + ".");
          await Task.Delay(5000);
          MainPage._mainPageInstance.AfficherStatut("Envoi du SMS...");
          if (await MainPage._nadHat.EnvoyerSmsEnregistre((uint)noSms))
          {
            MainPage._mainPageInstance.AfficherStatut("Envoi réussi !", 5000);
            this.Frame.Navigate(typeof(HistoriqueSmsPAGE));
          }
          else
            MainPage._mainPageInstance.AfficherStatut("Envoi échoué!");
        }
      }
      finally
      {
        this.IsEnabled = true;
      }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
      _sms = new SaisieSms();
      if (e.Parameter != null)
        _sms.NumeroTelephone = (string)e.Parameter;
      MainGRD.DataContext = new SaisieSmsVM(_sms);
    }

    public SaisieSmsPAGE()
    {
      this.InitializeComponent();
    }
  }
}
