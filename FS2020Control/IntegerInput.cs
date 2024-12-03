using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;

namespace FS2020Control;

public class IntegerInput : Window
{
  private readonly int maxInt;
  internal int number = 0;
  public IntegerInput(int maxInt)
  {
    this.maxInt = maxInt;
    this.Content = "\n   " + maxInt.ToString() + " installations of FS were found.\n   " +
      "Type an integer between 1 and  " + maxInt + " to select one.\n   " +
      "Remember the selection for the next program start.\n   " +
      "This box is a temporary workaround. \n   In a future revision, the selection will be persisted.";
    this.FontSize = 20;
    this.Width = 550;
    this.Height = 250;
    this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
    this.KeyDown += new KeyEventHandler(OnKeyDownHandler);
  }

  private void OnKeyDownHandler(object sender, KeyEventArgs e)
  {
    if (e.Key >= Key.D1 && e.Key <= Key.D9) 
      number = e.Key - Key.D1;
   if (number < maxInt)  this.Close();
  }
}
