using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;

namespace SerialLIB
{
  public delegate void ChangementEtat(object sender);
  public class PeripheriquesSerieDuSysteme
  {
    private void _scrutateur_EnumerationCompleted(DeviceWatcher sender, object args) { SurEnumerationTerminee.Invoke(this); }

    protected DeviceWatcher _scrutateur = null;

    public PeripheriquesSerieDuSysteme()
    {
      _scrutateur = DeviceInformation.CreateWatcher(SerialDevice.GetDeviceSelector());
      _scrutateur.EnumerationCompleted += _scrutateur_EnumerationCompleted;
    }
    public void DemarrerSurveillance()      { _scrutateur.Start(); }
    public void AbonnerPeripherique(PeripheriqueSerie peripherique)
    {
      _scrutateur.Added += peripherique.PeripheriqueSerieAjoute;
      _scrutateur.Removed += peripherique.PeripheriqueSerieSupprime;
    }

    public event ChangementEtat SurEnumerationTerminee;
  }
}
