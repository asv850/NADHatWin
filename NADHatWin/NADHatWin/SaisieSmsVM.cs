using GlobalLIB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NADHatWin
{
  public class SaisieSmsVM : VMBase
  {
    private SaisieSms _modele = null;

    public string NumeroTelephone
    {
      get { return _modele.NumeroTelephone; }
      set { _modele.NumeroTelephone = value; }
    }
    public string Texte
    {
      get { return _modele.Texte; }
      set { _modele.Texte = value; }
    }


    public SaisieSmsVM(SaisieSms modele)
    {
      _modele = modele;
    }
  }
}
