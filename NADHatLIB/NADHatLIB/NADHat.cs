using SerialLIB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Gpio;
using Windows.Storage.Streams;

namespace NADHatLIB
{
  //--------------------------------------------------------------------------------------------------
  //----------------------------------- ReponseCommandeAT --------------------------------------------
  //--------------------------------------------------------------------------------------------------
  public class ReponseCommandeAT
  {
    private int GetCodeRetourNumerique()
    {
      switch (CodeRetour)
      {
        case "OK": return 0;
        case "CONNECT": return 1;
        case "RING": return 2;
        case "NO CARRIER": return 3;
        case "ERROR": return 4;
        case "NO DIALTONE": return 5;
        case "BUSY": return 6;
        case "NO ANSWER": return 7;
        case "PROCEEDING": return 8;
        default: return 9;
      }
    }
    public string CommandeAT { get; private set; }
    public string ReponseCommande { get; private set; }
    public string CodeRetour { get; private set; }
    public int CodeRetourNumerique { get { return GetCodeRetourNumerique(); } }
    public ReponseCommandeAT(string commandeAT, string reponseAT, string codeRetour)
    {
      CommandeAT = commandeAT;
      if (reponseAT.Length > 4)
        ReponseCommande = reponseAT.Substring(2, reponseAT.Length - 4);
      else
        ReponseCommande = "";
      CodeRetour = codeRetour;
    }
    public ReponseCommandeAT(string donneesNotif)
    {
      CommandeAT = "";
      ReponseCommande = donneesNotif;
      CodeRetour = "";
    }
    public static ReponseCommandeAT CreateFromMatch(Match regexRep)
    {
      if (regexRep.Success)
        return new ReponseCommandeAT(regexRep.Groups["cmdAT"].Value, regexRep.Groups["repAT"].Value, regexRep.Groups["resAT"].Value);
      else
        return null;
    }
    public static ReponseCommandeAT CreateFromMatchNotif(Match regexRep)
    {
      if (regexRep.Success)
        if (regexRep.Groups["notif"].Success)
          return new ReponseCommandeAT(regexRep.Groups["notif"].Value + ": " + regexRep.Groups["notifData"].Value);
        else
          return new ReponseCommandeAT(regexRep.Groups["notifData"].Value);
      else
        return null;
    }
  }

  //--------------------------------------------------------------------------------------------------
  //--------------------------------- NotificationMessageSms -----------------------------------------
  //--------------------------------------------------------------------------------------------------
  public sealed class NotificationMessageSms
  {
    public string TypeMemoire { get; private set; }
    public ushort IndexMemoire { get; private set; }
    public bool EstMMS { get; private set; }
    public NotificationMessageSms(string donneesCMTI)
    {
      string[] donnees = donneesCMTI.Split(',');
      TypeMemoire = donnees[0].Trim('"');
      IndexMemoire = Convert.ToUInt16(donnees[1]);
      if (donnees.GetUpperBound(0) >= 2)
        EstMMS = (donnees[2] == "MMS PUSH");
      else
        EstMMS = false;
    }
    public static NotificationMessageSms CreateFromMatch(Match regexRep)
    {
      if (regexRep.Success)
        return new NotificationMessageSms(regexRep.Groups["notifData"].Value);
      else
        return null;
    }
  }

  //-------------------------------------------------------------------------------------------------
  //----------------------------------- ConfigurationNtp --------------------------------------------
  //-------------------------------------------------------------------------------------------------
  public class ConfigurationNtp
  {
    public string UrlServeurNtp { get; private set; }
    public TimeSpan DecalageUtc { get; private set; }
    public static ConfigurationNtp CreateFrom(string reponseCommandeAT_CNTP)
    {
      ConfigurationNtp res = null;
      int idxCNTP = reponseCommandeAT_CNTP.IndexOf("+CNTP: ");
      if (idxCNTP >= 0)
      {
        string[] tabchamps = reponseCommandeAT_CNTP.Substring(idxCNTP + 7).Split(',');
        res = new ConfigurationNtp() { UrlServeurNtp = tabchamps[0] };
        res.DecalageUtc = TimeSpan.FromMinutes(Convert.ToDouble(tabchamps[1]) * 15);
      }
      return res;
    }
  }

  //--------------------------------------------------------------------------------------------------
  //----------------------------------- EtatConnexionDonnees -----------------------------------------
  //--------------------------------------------------------------------------------------------------
  public enum StatutConnexionDonnees { scConnexionEnCours, scConnecte, scDeconnexionEnCours, scDeconnecte };
  public sealed class EtatConnexionDonnees
  {
    public byte NoConnexion { get; private set; }
    public StatutConnexionDonnees StatutConnexion { get; private set; }
    public string AdresseIP { get; private set; }
    public static EtatConnexionDonnees CreateFrom(string reponseCommandeAT_SAPBR)
    {
      EtatConnexionDonnees res = null;
      int idxSAPBR = reponseCommandeAT_SAPBR.IndexOf("+SAPBR: ");
      if (idxSAPBR >= 0)
      {
        string[] tabchamps = reponseCommandeAT_SAPBR.Substring(idxSAPBR + 8).Split(',');
        res = new EtatConnexionDonnees() { AdresseIP = tabchamps[2].Trim('"') };
        try
        {
          res.NoConnexion = Convert.ToByte(tabchamps[0]);
          res.StatutConnexion = (StatutConnexionDonnees)Convert.ToByte(tabchamps[1]);
        }
        catch (Exception)
        {
          res = null;
        }
      }
      return res;
    }
  }

  //-------------------------------------------------------------------------------------------------
  //----------------------------------------- NADHat ------------------------------------------------
  //-------------------------------------------------------------------------------------------------
  public delegate void ReceptionReponseCommandeAT(object sender, ReponseCommandeAT reponseRecue);
  public delegate void ReceptionNotificationAvecDonnees(object sender, ReponseCommandeAT notificationRecue);
  public delegate void ReceptionNotificationMessageSMS(object sender, NotificationMessageSms notificationMessageSmsRecue);
  public delegate void AjoutSmsDansMemoire(object sender, uint noSms);
  public delegate void SuppressionTousSmsDansMemoire(object sender);
  public delegate void LectureTousSmsDepuisMemoire(object sender, ReponseCommandeAT reponseCommandeAT_CMGL_ALL);
  public delegate void ReceptionNotificationSynchroNtp(object sender, byte resultatSynchroNtp);
  public class NADHat : PeripheriqueSerie
  {
    private const int NADHAT_ERREUR_ENVOI_DONNEES = -1000;
    private const int NADHAT_ERREUR_REPONSE_INCORRECTE = -1001;

    public static readonly byte[] TODA_TYPES = new byte[5] { 0, 129, 161, 145, 177 };

    private const string REGEX_COMMANDE_AT = "AT[\\S]*?";
    private const string REGEX_CODERETOUR = "0|1|2|3|4|5|6|7|8|9|OK|CONNECT|RING|NO\\sCARRIER|ERROR|NO\\sDIALTONE|BUSY|NO\\sANSWER|PROCEEDING";
    private static Regex _RegexTrameAT = new Regex("(?<cmdAT>" + REGEX_COMMANDE_AT + ")\r(?<repAT>\r\n[\\s\\S]*\r\n)??\r\n(?<resAT>" + REGEX_CODERETOUR + ")\r\n", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private const string REGEX_UNSOLLICITED_RESULT_CODES = "NORMAL\\sPOWER\\sDOWN|SMS\\sReady|Call\\sReady";
    private static Regex _RegexNotif = new Regex("\r\n((?<notif>\\+C\\S{2,3}):\\s(?<notifData>[\\S\\s]+?)|(?<notifData>" + REGEX_UNSOLLICITED_RESULT_CODES + "))\r\n", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private ManualResetEvent _attenteReponse = new ManualResetEvent(false);
    private Semaphore _envoiCommandeATPossible = new Semaphore(1, 1);
    private ReponseCommandeAT _derniereReponseCommandeAT = null;
    private GpioPin _pinOnOff = null;
    private object _bufferLOCK = new object();
    private string _buffer = "";

    private async Task<bool> EnvoyerCommandeATAvecReponseBool(string commandeAT, int timeoutSeconds = 1)
    {
      ReponseCommandeAT rep = await EnvoyerCommandeATAvecReponseAT(commandeAT, timeoutSeconds);
      return (rep != null) && (rep.CodeRetour == "OK");
    }
    private async Task<string> EnvoyerCommandeATAvecReponseChaine(string commandeAT, int timeoutSeconds = 1)
    {
      ReponseCommandeAT rep = await EnvoyerCommandeATAvecReponseAT(commandeAT, timeoutSeconds);
      if ((rep != null) && (rep.CodeRetour == "OK"))
        return _derniereReponseCommandeAT.ReponseCommande;
      else
        return null;
    }
    private async Task<ReponseCommandeAT> EnvoyerCommandeATAvecReponseAT(string commandeAT, int timeoutSeconds = 1, string texte = "")
    {
      _envoiCommandeATPossible.WaitOne();
      _attenteReponse.Reset();
      ReponseCommandeAT res = null;
      if (await EnvoyerCommandeAT(commandeAT))
      {
        if ((texte != "") && (!await EnvoyerTexte(texte)))
          _attenteReponse.Set();
        else
        {
          if (!_attenteReponse.WaitOne(new TimeSpan(0, 0, timeoutSeconds)))
            _attenteReponse.Set();
          res = _derniereReponseCommandeAT;
        }
      }
      _envoiCommandeATPossible.Release();
      return res;
    }
    private async Task<bool> EnvoyerCommandeAT(string commande)
    {
      Debug.WriteLine(">>>>>>>>>>>>>>>");
      Debug.WriteLine(commande);
      Debug.WriteLine("---------------");
      return await EnvoyerDonnees(Encoding.GetEncoding(0).GetBytes(commande + '\r'));
    }
    private async Task<bool> EnvoyerTexte(string texte)
    {
      Debug.WriteLine(">>>>>>>>>>>>>>>> Texte :");
      Debug.WriteLine(texte);
      Debug.WriteLine("------------------------");
      return await EnvoyerDonnees(Encoding.GetEncoding(0).GetBytes(texte + '\x1A'));
    }
    private void OuvrirPin26()
    {
      GpioController gpc = GpioController.GetDefault();

      _pinOnOff = gpc.OpenPin(26);
      _pinOnOff.Write(GpioPinValue.Low);
      _pinOnOff.SetDriveMode(GpioPinDriveMode.Output);
    }
    private bool TraiterLesReponses()
    {
      Match rechercheTrameAT;
      lock (_bufferLOCK)
      {
        rechercheTrameAT = _RegexTrameAT.Match(_buffer);
        SupprimerChaineRechecheeDans(rechercheTrameAT);
      }
      if (rechercheTrameAT.Success)
      {
        _derniereReponseCommandeAT = ReponseCommandeAT.CreateFromMatch(rechercheTrameAT);
        if (_derniereReponseCommandeAT != null)
        {
          _attenteReponse.Set();
          SurReceptionReponseCommandeAT?.Invoke(this, _derniereReponseCommandeAT);
          if (_derniereReponseCommandeAT.CodeRetourNumerique == 0)
          {
            if (_derniereReponseCommandeAT.CommandeAT.StartsWith("AT+CMGD=1,4"))
              SurSuppressionTousSmsDansMemoire?.Invoke(this);
            if (_derniereReponseCommandeAT.CommandeAT.StartsWith(@"AT+CMGL=""ALL"""))
              SurLectureTousSmsDepuisMemoire?.Invoke(this, _derniereReponseCommandeAT);
            if (_derniereReponseCommandeAT.CommandeAT.StartsWith("AT+CMGW="))
            {
              int posCMGW = _derniereReponseCommandeAT.ReponseCommande.IndexOf("\r\n+CMGW: ");
              if (posCMGW >= 0)
                SurAjoutSmsDansMemoire(this, Convert.ToUInt32(_derniereReponseCommandeAT.ReponseCommande.Substring(posCMGW + 9)));
            }
          }
        }
      }
      return rechercheTrameAT.Success;
    }
    private bool TraiterLesNotifications()
    {
      Match rechercheNotifSMS;
      lock (_bufferLOCK)
      {
        rechercheNotifSMS = _RegexNotif.Match(_buffer);
        SupprimerChaineRechecheeDans(rechercheNotifSMS);
      }
      ReponseCommandeAT notif = ReponseCommandeAT.CreateFromMatchNotif(rechercheNotifSMS);
      if (notif != null)
      {
        SurReceptionNotificationAvecDonnees?.Invoke(this, notif);
        if (notif.ReponseCommande.StartsWith("+CMTI: "))
        {
          NotificationMessageSms notifSMS = NotificationMessageSms.CreateFromMatch(rechercheNotifSMS);
          if (notifSMS != null)
          {
            SurReceptionNotificationMessageSMS?.Invoke(this, notifSMS);
            SurAjoutSmsDansMemoire?.Invoke(this, notifSMS.IndexMemoire);
          }
        }
        //else if (notif.ReponseCommande == "NORMAL POWER DOWN")
        //  SurNormalPowerDown?.Invoke(this);
        //else if (notif.ReponseCommande == "SMS Ready")
        //  SurSMSReady?.Invoke(this);
        //else if (notif.ReponseCommande == "Call Ready")
        //  SurCallReady?.Invoke(this);
        else if (notif.ReponseCommande.StartsWith("+CNTP: "))
          SurReceptionNotificationSynchroNtp?.Invoke(this, Convert.ToByte(notif.ReponseCommande.Substring(7)));
      }
      return rechercheNotifSMS.Success;
    }
    internal void SupprimerChaineRechecheeDans(Match rech)
    {
      if (rech.Success)
      {
        _buffer = _buffer.Substring(0, rech.Index) + _buffer.Substring(rech.Index + rech.Length);
        //--SurBufferChanged?.Invoke(this, _buffer);
      }
    }
    private IDictionary<string, string> ConvertirParametresConnexionDonnees(string parametres)
    {
      Dictionary<string, string> res = new Dictionary<string, string>();
      string[] lignes = parametres.Split(new string[] { "\r\n" }, StringSplitOptions.None);
      foreach (string ligne in lignes)
      {
        string[] paire = ligne.Split(':');
        if (paire.Length > 1)
          res.Add(paire[0], paire[1].TrimStart(' '));
        else
          res.Add(paire[0], "");
      }
      return res;
    }
    private async Task<EtatConnexionDonnees> GetStatutConnexionDonneesStable(byte noConnexion)
    {
      EtatConnexionDonnees res = await GetStatutConnexionDonnees(noConnexion);
      int nbFois = 17;
      while ((nbFois > 0) && (res != null) && ((res.StatutConnexion == StatutConnexionDonnees.scConnexionEnCours) || (res.StatutConnexion == StatutConnexionDonnees.scDeconnexionEnCours)))
      {
        await Task.Delay(5000);
        nbFois--;
        res = await GetStatutConnexionDonnees(noConnexion);
      }
      return res;
    }
    private string GetChaineToda(byte toda) { return ((toda == 129) || (toda == 161) || (toda == 145) || (toda == 177)) ? "," + toda.ToString() : ""; }
    private static string ConvertGsmTimezoneToDateTimeOffsetTimezone(string gsmTimezoneValue)
    {
      TimeSpan decalageTz = TimeSpan.FromMinutes(Convert.ToDouble(gsmTimezoneValue) * 15);
      return decalageTz.ToString(@"hh\:mm");
    }

    protected override void DonnesRecues(DataReader donnees)
    {
      byte[] donneesOctets = new byte[donnees.UnconsumedBufferLength];
      donnees.ReadBytes(donneesOctets);
      string donneesRecues = Encoding.GetEncoding(0).GetString(donneesOctets).Replace("\0", "");
      if (donneesRecues.Length > 0)
      {
        Debug.WriteLine("<<<<<<<");
        Debug.WriteLine(donneesRecues);
        Debug.WriteLine("-------");
        lock (_bufferLOCK)
        {
          _buffer += donneesRecues;
        }
        while (TraiterLesReponses()) ;
        if (_attenteReponse.WaitOne(0))
          while (TraiterLesNotifications()) ;
      }
    }

    public NADHat(ParametresPortSerie parametres, bool autoOuvrir, uint nbMaxOctetsALire) : base(parametres, autoOuvrir, nbMaxOctetsALire) { }
    private async Task<bool> AllumerEteindre()
    {
      if (_pinOnOff == null) OuvrirPin26();
      if (_pinOnOff != null)
      {
        _pinOnOff.Write(GpioPinValue.High);
        await Task.Delay(1500);
        _pinOnOff.Write(GpioPinValue.Low);
        await Task.Delay(5000);
        return true;
      }
      return false;
    }
    public async Task<bool> DemarrerNadHat()
    {
      byte nbFois = 4;
      bool init;
      bool allumage = true;
      do
      {
        init = await Initialiser();
        if (!init)
          allumage = await AllumerEteindre();
        nbFois--;

      } while (allumage && !init && (nbFois > 0));
      return init;
    }
    public async Task<bool> Eteindre(bool enUrgence)
    {
      _envoiCommandeATPossible.WaitOne();
      _attenteReponse.Set();
      bool res = await EnvoyerCommandeAT("AT+CPOWD=" + (enUrgence ? "0" : "1"));
      _envoiCommandeATPossible.Release();
      return res;
    }
    public async Task<bool> Initialiser()    { return await EnvoyerCommandeATAvecReponseBool("ATZ", 5); }
    public async Task<string> GetEtatSaisiePin() { return await EnvoyerCommandeATAvecReponseChaine("AT+CPIN?", 5); }
    public async Task<bool> EntrerPin(string pincode) { return await EnvoyerCommandeATAvecReponseBool("AT+CPIN=" + pincode, 5); }
    public async Task<bool> SetFormatMessageSms(bool textMode) { return await EnvoyerCommandeATAvecReponseBool("AT+CMGF=" + (textMode ? "1" : "0")); }
    public async Task<bool> RestaurerConfigurationSMS(byte noProfil) { return await EnvoyerCommandeATAvecReponseBool("AT+CRES=" + noProfil.ToString(), 5).AsAsyncOperation(); }
    public async Task<ReponseCommandeAT> GetSms(uint noSms, bool changeReadStatus) { return await EnvoyerCommandeATAvecReponseAT("AT+CMGR=" + noSms.ToString() + "," + (changeReadStatus ? "0" : "1"), 5); }
    public async Task<ReponseCommandeAT> ListerTousLesSms() { return await EnvoyerCommandeATAvecReponseAT("AT+CMGL=\"ALL\"", 20); }
    public async Task<int> EnregistrerSms(string destNo, byte toda, string smsText)
    {
      if (destNo.StartsWith("+"))
      {
        destNo = destNo.Substring(1);
        toda = 145;
      }
      ReponseCommandeAT rep = await EnvoyerCommandeATAvecReponseAT(@"AT+CMGW=""" + destNo + @"""" + GetChaineToda(toda), 5, smsText);
      if (rep == null)
        return NADHAT_ERREUR_ENVOI_DONNEES;
      if (rep.CodeRetourNumerique == 0)
      {
        int posCMGW = rep.ReponseCommande.LastIndexOf("\r\n+CMGW: ");
        if (posCMGW >= 0)
          return Convert.ToInt32(rep.ReponseCommande.Substring(posCMGW + 9));
        else
          return NADHAT_ERREUR_REPONSE_INCORRECTE;
      }
      else
        return -rep.CodeRetourNumerique;
    }
    public async Task<bool> EnvoyerSmsEnregistre(uint noSms) { return await EnvoyerCommandeATAvecReponseBool("AT+CMSS=" + noSms.ToString(), 60); }
    public async Task<bool> SupprimerTousLesSms() { return await EnvoyerCommandeATAvecReponseBool("AT+CMGD=1,4", 25); }
    public async Task<IDictionary<string, string>> GetParametresConnexionDonnees(byte noConnexion)
    {
      ReponseCommandeAT rep = await EnvoyerCommandeATAvecReponseAT("AT+SAPBR=4," + noConnexion.ToString());
      if ((rep != null) && (rep.CodeRetourNumerique == 0))
        return ConvertirParametresConnexionDonnees(rep.ReponseCommande);
      else
        return null;
    }
    public async Task<EtatConnexionDonnees> GetStatutConnexionDonnees(byte noConnexion)
    {
      ReponseCommandeAT rep = await EnvoyerCommandeATAvecReponseAT("AT+SAPBR=2," + noConnexion.ToString());
      if ((rep != null) && (rep.CodeRetourNumerique == 0))
        return EtatConnexionDonnees.CreateFrom(rep.ReponseCommande);
      return null;
    }
    public async Task<ConfigurationNtp> GetConfigurationNtp()
    {
      ReponseCommandeAT rep = await EnvoyerCommandeATAvecReponseAT("AT+CNTP?");
      if ((rep != null) && (rep.CodeRetourNumerique == 0))
        return ConfigurationNtp.CreateFrom(rep.ReponseCommande);
      else
        return null;
    }
    public async Task<DateTimeOffset> GetHorloge()
    {
      ReponseCommandeAT rep = await EnvoyerCommandeATAvecReponseAT("AT+CCLK?");
      if ((rep != null) && (rep.CodeRetourNumerique == 0))
        return ConvertGsmTimestampToDateTimeOffset(rep.ReponseCommande.Substring(8, rep.ReponseCommande.Length - 9));
      return DateTimeOffset.MinValue;
    }
    public async Task<int> SynchroniserHorlogeNtp(byte noConnexion, string apn, string ntpServerUrl, TimeSpan timezoneOffset)
    {
      string noConStr = noConnexion.ToString();
      if (await EnvoyerCommandeATAvecReponseBool("AT+SAPBR=3," + noConStr + @",""Contype"",""GPRS"""))
        if (await EnvoyerCommandeATAvecReponseBool("AT+SAPBR=3," + noConStr + @",""APN"",""" + apn + @""""))
        {
          EtatConnexionDonnees ecData = await GetStatutConnexionDonneesStable(noConnexion);
          if (ecData == null)
            return -7;
          else if (ecData.StatutConnexion == StatutConnexionDonnees.scConnexionEnCours)
            return -8;
          else if (ecData.StatutConnexion == StatutConnexionDonnees.scDeconnexionEnCours)
            return -9;
          else if (ecData.StatutConnexion == StatutConnexionDonnees.scDeconnecte)
            if (!await EnvoyerCommandeATAvecReponseBool("AT+SAPBR=1," + noConStr, 85))
              return -4;
          if (await EnvoyerCommandeATAvecReponseBool("AT+CNTPCID=" + noConStr))
            if (await EnvoyerCommandeATAvecReponseBool(@"AT+CNTP=""" + ntpServerUrl + @"""," + (timezoneOffset.TotalHours * 4).ToString()))
              if (await EnvoyerCommandeATAvecReponseBool("AT+CNTP"))
                return 0;
              else
                return -1;
            else
              return -2;
          else
            return -3;
        }
        else
          return -5;
      else
        return -6;
    }
    public async Task<bool> FermerConnexionDonnees(byte noConnexion) { return await EnvoyerCommandeATAvecReponseBool("AT+SAPBR=0," + noConnexion.ToString(), 65); }

    public event ReceptionReponseCommandeAT SurReceptionReponseCommandeAT;
    public event ReceptionNotificationAvecDonnees SurReceptionNotificationAvecDonnees;
    public event ReceptionNotificationMessageSMS SurReceptionNotificationMessageSMS;
    public event AjoutSmsDansMemoire SurAjoutSmsDansMemoire;
    public event SuppressionTousSmsDansMemoire SurSuppressionTousSmsDansMemoire;
    public event LectureTousSmsDepuisMemoire SurLectureTousSmsDepuisMemoire;
    public event ReceptionNotificationSynchroNtp SurReceptionNotificationSynchroNtp;

    public static DateTimeOffset ConvertGsmTimestampToDateTimeOffset(string gsmTimestampValue)
    {
      const string format = "yy/MM/dd,HH:mm:sszzz";
      string gsmTime = gsmTimestampValue.Substring(0, gsmTimestampValue.Length - 2) + ConvertGsmTimezoneToDateTimeOffsetTimezone(gsmTimestampValue.Substring(gsmTimestampValue.Length - 2));
      return DateTimeOffset.ParseExact(gsmTime, format, CultureInfo.CurrentCulture);
    }
  }
}
