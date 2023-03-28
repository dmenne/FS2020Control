using Jot;
using System.Windows;
using System.Windows.Controls;

namespace FS2020Control
{

  // Expose services as static class to keep the example simple 
  // https://github.com/anakic/Jot
  static class Services
  {
    // expose the tracker instance
    public static Tracker Tracker = new();

    static Services()
    {
      // tell Jot how to track Window objects
      Tracker.Configure<Window>()
        .Id(w => w.Name)
        .Properties(w => new { w.Top, w.Width, w.Height, w.Left, w.WindowState });
      Tracker.Configure<CheckBox>()
        .Id(c => c.Name)
        .Properties(c => new { c.IsChecked });
      // Call Tracker.Services.PersistAll on Main Window closing
    }
  }
}
