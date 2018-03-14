using GlobalLIB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;

namespace SerialLIB
{
  public enum ElementModifie { portConnecte, portOuvert };
  public delegate void ModificationEtat(object sender, ElementModifie e);
  public abstract class PeripheriqueSerie
  {
    private bool _autoOuvrir;
    private uint _nbMaxOctetsALire;
    private DeviceInformation _infoPeripherique = null;
    private SerialDevice _portSerie = null;
    private object _portSerieLock = new object();
    private CancellationTokenSource _jetonAnnulationLectureContinue = null;
    private async void LectureContinue()
    {
      Debug.WriteLine("PSERIE " + _infoPeripherique.Name + ", " + _infoPeripherique.Id + "  :  LectureContinue();");
      _jetonAnnulationLectureContinue = new CancellationTokenSource();
      DataReader donneesLues = new DataReader(_portSerie.InputStream);
      donneesLues.InputStreamOptions = InputStreamOptions.Partial;
      while (!_jetonAnnulationLectureContinue.IsCancellationRequested)
      {
        uint nbOctetsLus = 0;
        try
        {
          nbOctetsLus = await donneesLues.LoadAsync(_nbMaxOctetsALire).AsTask(_jetonAnnulationLectureContinue.Token);
        }
        catch (Exception ex)
        {
          Debug.WriteLine("PSERIE " + _infoPeripherique.Name + ", " + _infoPeripherique.Id + "  : LectureContinue ERROR = " + ex.GetType().ToString() + " " + ex.Message);
        }
        if (nbOctetsLus > 0)
          DonnesRecues(donneesLues);
      }
    }

    protected abstract void DonnesRecues(DataReader donnees);
    protected virtual void DetruirePortSerie()
    {
      lock (_portSerieLock)
      {
        if (_portSerie != null)
          _portSerie.Dispose();
        _portSerie = null;
      }
    }
    protected async Task<bool> EnvoyerDonnees(byte[] octets)
    {
      bool res = false;
      if ((Ouvert) && (octets.Length > 0))
      {
        DataWriter donneesAEcrire = new DataWriter(_portSerie.OutputStream);
        try
        {
          donneesAEcrire.WriteBytes(octets);
          res = (await donneesAEcrire.StoreAsync() == octets.Length);
        }
        finally
        {
          donneesAEcrire.DetachStream();
        }
      }
      return res;
    }

    public ParametresPortSerie Parametres { get; private set; }
    public bool Connecte { get { return _infoPeripherique != null; } }
    public bool Ouvert
    {
      get
      {
        lock (_portSerieLock)
          return _portSerie != null;
      }
    }
    public PeripheriqueSerie(ParametresPortSerie parametres, bool autoOuvrir, uint nbMaxOctetsALire)
    {
      Parametres = parametres;
      _autoOuvrir = autoOuvrir;
      _nbMaxOctetsALire = nbMaxOctetsALire;

      PeripheriquesSerie.AbonnerPeripherique(this);
    }
    public void PeripheriqueSerieAjoute(DeviceWatcher sender, DeviceInformation args)
    {
      if (!Connecte && (args.Id.StartsWith(Parametres.IdRecherche)))
      {
        _infoPeripherique = args;
        SurModificationEtat?.Invoke(this, ElementModifie.portConnecte);
        if (_autoOuvrir)
          Ouvrir();
      }
    }
    public void PeripheriqueSerieSupprime(DeviceWatcher sender, DeviceInformationUpdate args)
    {
      if (Connecte && (args.Id == _infoPeripherique.Id))
      {
        Fermer();
        _infoPeripherique = null;
        SurModificationEtat?.Invoke(this, ElementModifie.portConnecte);
      }
    }
    public async void Ouvrir()
    {
      Debug.WriteLine("PSERIE " + _infoPeripherique.Name + ", " + _infoPeripherique.Id + "  : Ouvrir()");
      try
      {
        SerialDevice _temp = await SerialDevice.FromIdAsync( _infoPeripherique.Id);
        if (_temp != null)
        {
          lock (_portSerieLock) { _portSerie = _temp; }
          Debug.WriteLine("PSERIE " + _infoPeripherique.Name + ", " + _infoPeripherique.Id + "  : Ouvrir()=OK!");
          _portSerie.BaudRate     = Parametres.Vitesse;
          _portSerie.Parity       = Parametres.Parite;
          _portSerie.StopBits     = Parametres.BitsDeStop;
          _portSerie.DataBits     = Parametres.BitsDeDonnees;
          _portSerie.Handshake    = Parametres.ControleDeFlux;
          _portSerie.ReadTimeout  = TimeSpan.FromMilliseconds(Parametres.DureeLectureMs);
          _portSerie.WriteTimeout = TimeSpan.FromMilliseconds(Parametres.DureeEcritureMs);
          SurModificationEtat?.Invoke(this, ElementModifie.portOuvert);
          if (Parametres.DureeLectureMs > 0)
            LectureContinue();
        }
        else
          Debug.WriteLine("PSERIE " + _infoPeripherique.Name + ", " + _infoPeripherique.Id + "  : Ouvrir()=ECHOUE !");
      }
      catch (Exception ex)
      {
        lock (_portSerieLock) { _portSerie = null; }
        Debug.WriteLine("PSERIE " + _infoPeripherique.Name + ", " + _infoPeripherique.Id + "  : Ouvrir() ERREUR = " + ex.GetType().ToString() + " " + ex.Message);
      }
    }
    public virtual void Fermer()
    {
      Debug.WriteLine("PSERIE " + _infoPeripherique.Name + ", " + _infoPeripherique.Id + " : Fermer()");
      if ((_jetonAnnulationLectureContinue != null) &&
          (!_jetonAnnulationLectureContinue.IsCancellationRequested))
        _jetonAnnulationLectureContinue.Cancel();
      DetruirePortSerie();
      SurModificationEtat?.Invoke(this, ElementModifie.portOuvert);
    }
    public event ModificationEtat SurModificationEtat;

    public static PeripheriquesSerieDuSysteme PeripheriquesSerie = new PeripheriquesSerieDuSysteme();
  }

  public class ParametresPortSerie
  {
    private static string _piIdString = null;

    public string Nom                     { get; private set; }
    public string IdRecherche             { get; private set; }
    public uint Vitesse                   { get; private set; }
    public SerialParity Parite            { get; private set; }
    public SerialStopBitCount BitsDeStop  { get; private set; }
    public ushort BitsDeDonnees           { get; private set; }
    public SerialHandshake ControleDeFlux { get; private set; }
    public double DureeLectureMs          { get; private set; }
    public double DureeEcritureMs         { get; private set; }
    public ParametresPortSerie(string nom, string idRecherche, uint vitesse, SerialParity parite, SerialStopBitCount bitsDeStop, ushort bitsDeDonnees, SerialHandshake controleDeFlux, double dureeLectureMs, double dureeEcritureMs)
    {
      Nom = nom;
      IdRecherche = idRecherche;
      Vitesse = vitesse;
      Parite = parite;
      BitsDeStop = bitsDeStop;
      BitsDeDonnees = bitsDeDonnees;
      ControleDeFlux = controleDeFlux;
      DureeLectureMs = dureeLectureMs;
      DureeEcritureMs = dureeEcritureMs;
    }
    
    public static ParametresPortSerie CreerPourUARTGpioPi(uint vitesse, SerialParity parite, SerialStopBitCount bitsDeStop, ushort bitsDeDonnees, SerialHandshake controleDeFlux, double dureeLectureMs, double dureeEcritureMs)
    {
      if (string.IsNullOrEmpty(_piIdString))
        _piIdString = RaspberryPi.EstRaspberryPi2() ? @"\\?\ACPI#BCM2837#4#" : @"\\?\ACPI#BCM2836#0#";
      return new ParametresPortSerie("", _piIdString, vitesse, parite, bitsDeStop, bitsDeDonnees, controleDeFlux, dureeLectureMs, dureeEcritureMs);
    }
  }
}
