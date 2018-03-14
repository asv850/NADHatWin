using Windows.UI.Xaml.Controls;

// Pour plus d'informations sur le modèle d'élément Boîte de dialogue de contenu, consultez la page https://go.microsoft.com/fwlink/?LinkId=234238

namespace NADHatLIB
{
  public sealed partial class CodePinDLG : ContentDialog
  {
    public string codePinSaisi { get { return CodePinPBX.Password; } }
    public CodePinDLG()        { this.InitializeComponent(); }

  }
}
