using GlobalLIB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace SerialLIB
{
  public class PeripheriqueSerieVM : VMBase
  {
    private async void _modele_SurModificationEtat(object sender, ElementModifie e)
    {
      switch (e)
      {
        case ElementModifie.portConnecte:
          await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
          {
            OnPropertyChanged("Connecte");
          });
          break;
        case ElementModifie.portOuvert:
          await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
          {
            OnPropertyChanged("Ouvert");
          });
          break;
        default:
          break;
      }
    }

    protected PeripheriqueSerie _modele = null;

    public bool Connecte { get { return _modele.Connecte; } }
    public bool Ouvert   { get { return _modele.Ouvert; } }
    public PeripheriqueSerieVM(PeripheriqueSerie modele)
    {
      _modele = modele;
      _modele.SurModificationEtat += _modele_SurModificationEtat ;
    }
  }
}
