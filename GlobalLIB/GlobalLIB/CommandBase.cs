using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GlobalLIB
{
  public class CommandBase : ICommand
  {
    Action<object> _executeDelegate;
    public CommandBase(Action<object> executeDelegate)
    {
      _executeDelegate = executeDelegate;
    }
    public bool CanExecute(object parameter) { return true; }
    public void Execute(object parameter)
    {
      _executeDelegate(parameter);
    }
    public event EventHandler CanExecuteChanged;
  }
}
