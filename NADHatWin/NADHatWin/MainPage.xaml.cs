
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.SerialCommunication;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using NADHatLIB;
using SerialLIB;
using Windows.System;
using Windows.UI.Xaml.Data;

// Pour plus d'informations sur le modèle d'élément Page vierge, consultez la page https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace NADHatWin
{
  /// <summary>
  /// Une page vide peut être utilisée seule ou constituer une page de destination au sein d'un frame.
  /// </summary>
  public sealed partial class MainPage : Page
  {
    private const int NADHAT_BAUD_RATE = 115200;
    private const int DELAI_ENTRE_FIN_ENUM_ET_DEMARRAGE_NADHAT = 1000; //ms
    private const byte NO_CONNEXION_NTP = 1;
    public static NADHat _nadHat = null;
    public static NADHatVM _nadHatViewModel = null;
    public static MainPage _mainPageInstance = null;
    private CoreDispatcher _dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;
    private AutoResetEvent _attenteSynchroNtp = new AutoResetEvent(false);

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
      Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
      _mainPageInstance = this;

      ParametresPortSerie parametres = ParametresPortSerie.CreerPourUARTGpioPi(NADHAT_BAUD_RATE, SerialParity.None, SerialStopBitCount.One, 8, SerialHandshake.None, 100, 0);
      _nadHat = new NADHat(parametres, true, 1024);
      _nadHatViewModel = new NADHatVM(_nadHat);
      MainFRM.Navigate(typeof(HistoriqueSmsPAGE), _nadHatViewModel);
      MainPageGRD.DataContext = _nadHatViewModel;
      _nadHatViewModel.SurRepondreSms += _nadHatViewModel_SurRepondreSms;
      _nadHat.SurReceptionNotificationSynchroNtp += _nadHat_SurSynchronisationNtp;

      PeripheriqueSerie.PeripheriquesSerie.SurEnumerationTerminee += PeripheriquesSerie_SurEnumerationTerminee;
      PeripheriqueSerie.PeripheriquesSerie.DemarrerSurveillance();
    }
    private async void NTPSyncBTN_Click(object sender, RoutedEventArgs e)
    {
      NTPSyncBTN.IsEnabled = false;
      try
      {
        IDictionary<string, string> parametresConnexion = await _nadHat.GetParametresConnexionDonnees(NO_CONNEXION_NTP);
        ConfigurationNtp ntpConfig = await _nadHat.GetConfigurationNtp();
        ParametresSynchronisationNtpDLG paramSynchroNtpDLG = new ParametresSynchronisationNtpDLG() { Apn = parametresConnexion["APN"], NtpUrl = ntpConfig.UrlServeurNtp };
        if (await paramSynchroNtpDLG.ShowAsync() == ContentDialogResult.Primary)
        {
          int res = await _nadHat.SynchroniserHorlogeNtp(NO_CONNEXION_NTP, paramSynchroNtpDLG.Apn, paramSynchroNtpDLG.NtpUrl, TimeSpan.Zero);
          if (res == 0)
            _attenteSynchroNtp.WaitOne();
          if (res > -4)
            await _nadHat.FermerConnexionDonnees(NO_CONNEXION_NTP);
          switch (res)
          {
            case -1:
              AfficherStatut("Erreur lors du lancement de la synchronisation NTP!");
              break;
            case -2:
              AfficherStatut("Erreur lors de la configuration du serveur NTP!");
              break;
            case -3:
              AfficherStatut("Erreur lors de l'affectation de la connexion no " + NO_CONNEXION_NTP.ToString() + " au service NTP.");
              break;
            case -4:
              AfficherStatut("Erreur lors de l'ouverture de la connexion GPRS no " + NO_CONNEXION_NTP.ToString() + ".");
              break;
            case -5:
              AfficherStatut(@"Erreur lors de la configuration de l'APN """ + paramSynchroNtpDLG.Apn + @""".");
              break;
            case -6:
              AfficherStatut("Erreur lors de la configuration de la connexion en mode GPRS.");
              break;
            case -7:
              AfficherStatut("Erreur lors de la lecture de l'état de connexion GPRS");
              break;
            case -8:
              AfficherStatut("La connexion GPRS est bloquée avec l'état connexion en cours.");
              break;
            case -9:
              AfficherStatut("La connexion GPRS est bloquée avec l'état déconnexion en cours.");
              break;
            default:
              
              break;
          }
        }
      }
      finally
      {
        NTPSyncBTN.IsEnabled = true;
      }
    }
    private async void EteindreBTN_Click(object sender, RoutedEventArgs e)
    {
      ContentDialog confirmationExtinction = new ContentDialog() { Title = "Eteindre ?", PrimaryButtonText = "OK", SecondaryButtonText = "Annuler" };
      if (await confirmationExtinction.ShowAsync() == ContentDialogResult.Primary)
      {
        await _nadHat.Eteindre(false);
#if DEBUG
        App.Current.Exit();
#else
        ShutdownManager.BeginShutdown(ShutdownKind.Shutdown, new TimeSpan(0));
#endif
      }
    }
    private void _nadHatViewModel_SurRepondreSms(string noTelephone) { MainFRM.Navigate(typeof(SaisieSmsPAGE), noTelephone); }
    private async void _nadHat_SurSynchronisationNtp(object sender, byte resultatSynchroNtp)
    {
      _attenteSynchroNtp.Set();
      switch (resultatSynchroNtp)
      {
        case 1:
          AfficherStatut("Synchronisation NTP réussie.", 5000);
          break;
        case 61:
          AfficherStatut("Erreur réseau!");
          break;
        case 62:
          AfficherStatut("Erreur de résolution DNS!");
          break;
        case 63:
          AfficherStatut("Erreur de connexion!");
          break;
        case 64:
          AfficherStatut("Erreur de réponse du service!");
          break;
        case 65:
          AfficherStatut("Délai d'attente de réponse du service dépassé!");
          break;
        default:
          AfficherStatut("Code retour synchronisation NTP = " + resultatSynchroNtp.ToString());
          break;
      }
      await _dispatcher.RunAsync(CoreDispatcherPriority.Low, () => 
      {
        _nadHatViewModel.RafraichissementHorloge = false;
        _nadHatViewModel.RafraichissementHorloge = true;
      });
    }
    private async void PeripheriquesSerie_SurEnumerationTerminee(object sender)
    {
      await Task.Delay(DELAI_ENTRE_FIN_ENUM_ET_DEMARRAGE_NADHAT);
      DemarrerCarteNadHat();
    }
    private async void DemarrerCarteNadHat()
    {
      AfficherStatut("Démarrage de la carte NADHat");
      if (await _nadHat.DemarrerNadHat())
      {
        AfficherStatut("Code pin ....");
        string pinStatus = await _nadHat.GetEtatSaisiePin();
        if (pinStatus == "+CPIN: READY")
        {
          AfficherStatut("Code pin OK.");
          InitialiserCarteNadHatDepuisSIM();
        }
        else if (pinStatus == "+CPIN: SIM PIN")
        {
          AfficherStatut("Entrez le code pin!");
          ManualResetEvent drapeauCodePin = new ManualResetEvent(false);
          await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
          {
            bool codePinOk = false;
            CodePinDLG codePinDLG = new CodePinDLG();
            ContentDialogResult dlgRes;
            do
            {
              dlgRes = await codePinDLG.ShowAsync();
              if (dlgRes == ContentDialogResult.Primary)
                codePinOk = await _nadHat.EntrerPin(codePinDLG.codePinSaisi);
              if (!codePinOk)
                AfficherStatut("Mauvais code PIN !");
            } while ((dlgRes == ContentDialogResult.Primary) && (!codePinOk));
            if (codePinOk)
              drapeauCodePin.Set();
          });
          if (drapeauCodePin.WaitOne())
          {
            await Task.Delay(5000);
            InitialiserCarteNadHatDepuisSIM();
          }
        }
        else
          AfficherStatut("Problème de déblocage de la carte SIM : " + pinStatus);
      }
      else
        AfficherStatut("Impossible d'allumer ou de communiquer avec la carte NADHat !");
    }
    private async void InitialiserCarteNadHatDepuisSIM()
    {
      await _dispatcher.RunAsync(CoreDispatcherPriority.Low, () => { _nadHatViewModel.RafraichissementHorloge = true; });      
      AfficherStatut("Configuration SMS...");
      if ((await _nadHat.SetFormatMessageSms(true)) && (await _nadHat.RestaurerConfigurationSMS(0)))
      {
        AfficherStatut("Listage des SMS en mémoire !");
        ReponseCommandeAT rep = await _nadHat.ListerTousLesSms();
        if (rep.CodeRetourNumerique == 0)
          AfficherStatut("Système prêt !", 5000);
        else
          AfficherStatut("Impossible de lister les SMS !!!");
      }
    }
    public async void AfficherStatut(string message, ushort DelaiEffacementMs = 0)
    {
      await _dispatcher.RunAsync(CoreDispatcherPriority.High, async () => 
      {
        StatutTBK.Text = message;
        if (DelaiEffacementMs>0)
        {
          await Task.Delay(DelaiEffacementMs);
          StatutTBK.Text = "";
        }
      }); }

    public MainPage()
    {
      this.InitializeComponent();
    }
  }

  public class DateTimeOffsetToHorlogeConverter : IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, string language)
    {
      if ((value == null) || !(value is DateTimeOffset) || ((DateTimeOffset)value == DateTimeOffset.MinValue))
        return "";
      else
        return ((DateTimeOffset)value).ToLocalTime().ToString("g");
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
      throw new NotImplementedException();
    }
  }
}
