using GlobalLIB;
using SerialLIB;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace NADHatLIB
{
  public delegate void RepondreSms(string noTelephone);
  public class SmsVM : VMBase
  {
    private void ChargerValeurs(string modele, int posDebutTableau, int posFinTableau)
    {
      string[] tabValeurs = modele.Substring(posDebutTableau + 1, posFinTableau - posDebutTableau - 2).Split(new string[] { "\",\"" }, StringSplitOptions.None);
      EtatMessage = tabValeurs[0];
      EnReception = tabValeurs[0].StartsWith("REC ");
      NumeroTelephone = tabValeurs[1];
      if (tabValeurs.Length >= 4)
        Horodatage = NADHat.ConvertGsmTimestampToDateTimeOffset(tabValeurs[3]);
      else
        Horodatage = null;
      Texte = modele.Substring(posFinTableau + 2);
    }
    private void AffecterCommandes() { RepondreSms_Cmd = new CommandBase((x) => { SurRepondreSms?.Invoke(NumeroTelephone); }); }

    public uint Index { get; protected set; }
    public string EtatMessage { get; protected set; }
    public string NumeroTelephone { get; protected set; }
    public DateTimeOffset? Horodatage { get; protected set; }
    public string Texte { get; protected set; }
    public bool EnReception { get; protected set; }
    public ICommand RepondreSms_Cmd { get; private set; }
    public SmsVM() { }
    public SmsVM(string modele) : base()
    {
      AffecterCommandes();
      int posFinTableau = modele.IndexOf("\r\n");
      int premiereVirgule = modele.IndexOf(',');
      if ((posFinTableau >= 0) && (premiereVirgule >= 0) && (premiereVirgule < posFinTableau))
      {
        Index = Convert.ToUInt16(modele.Substring(7, premiereVirgule - 7));
        ChargerValeurs(modele, premiereVirgule + 1, posFinTableau);
      }
    }
    public SmsVM(string modele, uint index) : base()
    {
      AffecterCommandes();
      Index = index;
      int posFinTableau = modele.IndexOf("\r\n");
      if (posFinTableau >= 0)
        ChargerValeurs(modele, 7, posFinTableau);
    }
    public event RepondreSms SurRepondreSms;
  }

  public enum EtatPin { epinOk, epinSim, epinSimPuk, epinPhSim, epinPhSimPuk, epinSim2, epinSimPuk2, epinError }
  public class NADHatVM : PeripheriqueSerieVM
  {
    private DispatcherTimer _timer = null;
    private ListeSmsVM _listeSms = new ListeSmsVM();
    private NADHat Modele { get { return (_modele as NADHat); } }

    private async void Modele_SurReceptionReponseCommandeAT(object sender, ReponseCommandeAT reponseRecue)
    {
      if (reponseRecue.ReponseCommande.StartsWith("+CPIN: "))
      {
        EtatSim = GetEtatPin(reponseRecue.ReponseCommande);
        await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
        {
          OnPropertyChanged("EtatSim");
          OnPropertyChanged("SimDebloquee");
        });
      }
    }
    private async void Modele_SurSuppressionTousSmsDansMemoire(object sender)
    {
      await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
      {
        _listeSms.Clear();
        OnPropertyChanged("ListeSms");
      });
    }
    private async void Modele_SurAjoutSmsDansMemoire(object sender, uint noSms)
    {
      await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
      {
        ReponseCommandeAT smsRecu = await Modele.GetSms(noSms, true);
        if (smsRecu != null)
        {
          SmsVM unSmsViewModel = new SmsVM(smsRecu.ReponseCommande, noSms);
          _listeSms.Add(unSmsViewModel);
          unSmsViewModel.SurRepondreSms += UnSmsViewModel_SurRepondreSms;
          OnPropertyChanged("ListeSms");
        }
      });
    }
    private async void Modele_SurLectureTousSmsDepuisMemoire(object sender, ReponseCommandeAT reponseCommandeAT_CMGL_ALL)
    {
      await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { RafraichirListeSms(reponseCommandeAT_CMGL_ALL); });
    }
    private void UnSmsViewModel_SurRepondreSms(string noTelephone) { SurRepondreSms?.Invoke(noTelephone); }
    private async void _timer_Tick(object sender, object e)
    {
      _timer.Interval = TimeSpan.FromMinutes(1);
      await _dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
      {
        Maintenant = await Modele.GetHorloge();
        OnPropertyChanged("Maintenant");
      });
    }
    private void RafraichirListeSms(ReponseCommandeAT reponseAT_CMGL_ALL)
    {
      _listeSms.Clear();
      int startidx = reponseAT_CMGL_ALL.ReponseCommande.IndexOf("+CMGL:");
      bool stop = (startidx < 0);
      while (!stop)
      {
        int endidx = reponseAT_CMGL_ALL.ReponseCommande.IndexOf("\r\n+CMGL:", startidx + 6);
        string unSms;
        if (endidx >= 0)
          unSms = reponseAT_CMGL_ALL.ReponseCommande.Substring(startidx, endidx - startidx - 2);
        else
        {
          unSms = reponseAT_CMGL_ALL.ReponseCommande.Substring(startidx);
          stop = true;
        }
        SmsVM unSmsViewModel = new SmsVM(unSms);
        _listeSms.Add(unSmsViewModel);
        unSmsViewModel.SurRepondreSms += UnSmsViewModel_SurRepondreSms;
        startidx = endidx + 2;
      }
      OnPropertyChanged("ListeSms");
    }
    private EtatPin? GetEtatPin(string etatPin)
    {
      switch (etatPin)
      {
        case "+CPIN: READY": return EtatPin.epinOk;
        case "+CPIN: SIM PIN": return EtatPin.epinSim;
        case "+CPIN: SIM PUK": return EtatPin.epinSimPuk;
        case "+CPIN: PH_SIM PIN": return EtatPin.epinPhSim;
        case "+CPIN: PH_SIM PUK": return EtatPin.epinPhSimPuk;
        case "+CPIN: SIM PIN2": return EtatPin.epinSim2;
        case "+CPIN: SIM PUK2": return EtatPin.epinSimPuk2;
        default: return EtatPin.epinError;
      }
    }

    public EtatPin? EtatSim { get; private set; }
    public bool SimDebloquee { get { return (EtatSim.HasValue && EtatSim.Value == EtatPin.epinOk); } }
    public ListeSmsVM ListeSms { get { return _listeSms; } }
    public bool RafraichissementHorloge
    {
      get { return _timer.IsEnabled; }
      set
      {
        if (value != _timer.IsEnabled)
        {
          if (value)
          {
            _timer.Interval = TimeSpan.FromSeconds(5);
            _timer.Start();
          }
          else
            _timer.Stop();
        }
      }
    }
    public DateTimeOffset Maintenant { get; private set; }
    public NADHatVM(NADHat modele) : base(modele)
    {
      modele.SurReceptionReponseCommandeAT += Modele_SurReceptionReponseCommandeAT;
      modele.SurReceptionNotificationAvecDonnees += Modele_SurReceptionReponseCommandeAT;
      modele.SurSuppressionTousSmsDansMemoire += Modele_SurSuppressionTousSmsDansMemoire;
      modele.SurAjoutSmsDansMemoire += Modele_SurAjoutSmsDansMemoire;
      modele.SurLectureTousSmsDepuisMemoire += Modele_SurLectureTousSmsDepuisMemoire;
      _timer = new DispatcherTimer();
      _timer.Tick += _timer_Tick;
    }    

    public event RepondreSms SurRepondreSms;
  }

  public class ListeSmsVM : ObservableCollection<SmsVM> { }
}
