using Windows.UI.Xaml.Controls;

// Pour plus d'informations sur le modèle d'élément Boîte de dialogue de contenu, consultez la page https://go.microsoft.com/fwlink/?LinkId=234238

namespace NADHatLIB
{
  public sealed partial class ParametresSynchronisationNtpDLG : ContentDialog
  {
    public string Apn
    {
      get { return ApnTBX.Text; }
      set { ApnTBX.Text = value; }
    }
    public string NtpUrl
    {
      get { return NtpUrlTBX.Text; }
      set { NtpUrlTBX.Text = value; }
    }
    public ParametresSynchronisationNtpDLG()
    {
      this.InitializeComponent();
    }
   
  }
}
