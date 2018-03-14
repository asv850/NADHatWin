using System.ComponentModel;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace GlobalLIB
{
  public abstract class VMBase : INotifyPropertyChanged
  {
    protected static CoreDispatcher _dispatcher = null;
    protected virtual void OnPropertyChanged(string propertyName = "")
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public VMBase()
    {
      if (_dispatcher == null)
        _dispatcher = Window.Current.Dispatcher;
    }
    public event PropertyChangedEventHandler PropertyChanged;
  }
}
