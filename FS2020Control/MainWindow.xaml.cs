// #define TEST_RELOAD // Will rename a file every 9 seconds to test reload information
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;


// When the designer complaints or crashes, delete the bin folder and refresh
// https://learn.microsoft.com/en-us/ef/core/get-started/wpf

namespace FS2020Control
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {
    private readonly ControlContext controlContext = new(test: false);
    private readonly CollectionViewSource fsControlFileViewSource = default!;
    private readonly CollectionViewSource fsControlViewSource = default!;
    private MessageBoxResult? MessageBoxRes = null;
    private string lastSettingsLabel = "";
    private readonly System.Windows.Threading.DispatcherTimer CheckFilesTimer = new ();

    private bool FromDatabase { get; set; }
    // https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/how-to-sort-a-gridview-column-when-a-header-is-clicked
    GridViewColumnHeader? lastHeaderClicked = null;
    ListSortDirection lastDirection = ListSortDirection.Ascending;
#if TEST_RELOAD && DEBUG
    int checkFilesCount = 0;
#endif

    public MainWindow()
    {
      InitializeComponent();
      fsControlFileViewSource =
        (FindResource(nameof(fsControlFileViewSource)) as CollectionViewSource)!;
      fsControlViewSource =
        (FindResource(nameof(fsControlViewSource)) as CollectionViewSource)!;
      var tracker = Services.Tracker;
      tracker.Track(this);
      tracker.Track(HideDebugCheck);
      var assembly = System.Reflection.Assembly.GetExecutingAssembly()?
        .GetName()?.Version?.ToString();
      Title = $"FS2020 Controls - {assembly}";
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
      const int checkFilesSeconds = 2;
      LoadData();
      
      CheckFilesTimer.Tick += new EventHandler(CheckFilesTimer_Tick!);
      CheckFilesTimer.Interval = new TimeSpan(0, 0, checkFilesSeconds);
      CheckFilesTimer.Start();
    }


    private void CheckFilesTimer_Tick(object sender, EventArgs e)
    {
#if TEST_RELOAD && DEBUG
      checkFilesCount++;
      if (checkFilesCount % 3 == 0) {
        string? file = controlContext.FSControlsFile.Local
          .Select(fn => fn.FileName)
          .FirstOrDefault();
        if (file != null && System.IO.File.Exists(file))
        {
          System.IO.File.Move(file, file + "X");
          Console.WriteLine("Renamed File " + checkFilesCount);
        }
      }
#endif
      bool cf = controlContext.FSControlsFile.Local
        .Select(fn => fn.FileName)
        .Any(fn => !System.IO.File.Exists(fn));
      Brush? bisque = new BrushConverter().ConvertFromString("Bisque") as Brush;
      if (!cf)
      {
        SettingsLabel.Background = bisque;
        SettingsLabel.Content = lastSettingsLabel;
        return;
      }

      Brush? orange = new BrushConverter().ConvertFromString("Orange") as Brush;
      SettingsLabel.Content = "Reload!";
      SettingsLabel.Background = SettingsLabel.Background == bisque ? orange : bisque;
    }

    private void LoadData()
    {

      controlContext.Database.EnsureCreated();
      // Check if recent version
      try
      {
        var x = controlContext.FSControls.ToList();
      }
      catch
      {
        controlContext.Database.EnsureDeleted();
        controlContext.Database.EnsureCreated();
      }
      var xh = new XmlToSqlite(controlContext);
      try
      {
        FromDatabase = controlContext.HasData;
        if (!FromDatabase)
        {
          xh.CheckInstallations();
          _ = xh.ImportXmlFiles();
        }

        controlContext.FSControlsFile.Load();
        fsControlFileViewSource.Source =
          controlContext.FSControlsFile.Local.ToObservableCollection().OrderBy(a => a.FriendlyName);
      }
      catch (FS2020Exception ex)
      {
        // https://www.codeproject.com/Articles/5290638/Customizable-WPF-MessageBox
        MessageBox.Show(ex.Message, "FS2020 Controls");
      }
      lastSettingsLabel =  FromDatabase ? "From Database" : (
        xh.IsSteam ? "From Steam" : "From Store");
      SettingsLabel.Content = lastSettingsLabel;
      CollectionView ? view = (CollectionView)CollectionViewSource.GetDefaultView(fsControlFileViewSource);
      view?.SortDescriptions.Add(new SortDescription("Device", ListSortDirection.Ascending));
      UpdateSelectedContextBox();
      UpdateSelectedControlsBox();
    }

    private void FSControlFileGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      fsControlViewSource.SortDescriptions.Clear();
      fsControlViewSource.SortDescriptions.Add(new SortDescription("FriendlyAction", ListSortDirection.Ascending));
      UpdateSelectedContextBox();
      UpdateSelectedControlsBox();
    }

    private void CollectionViewSource_Filter(object sender, FilterEventArgs e)
    {
      if (e.Item is not FSControl fsc) return;
      bool hc = HideDebugCheck.IsChecked ?? false;
      if (hc)
      {
        Match m = Regex.Match(fsc.FriendlyAction!, @"Debug|Devmode",
                  RegexOptions.IgnoreCase);
        e.Accepted = !m.Success;
      }
      // When all or none are selected, use all
      int selectedControlsCount = SelectedControlsBox.SelectedItems.Count;
      int selectedControlItemsCount = SelectedControlsBox.Items.Count;
      int selectedContextCount = SelectedContextBox.SelectedItems.Count;
      int selectedContextItemsCount = SelectedContextBox.Items.Count;
      bool allControlsSelected = selectedControlsCount == 0 || selectedControlsCount == selectedControlItemsCount;
      bool allContextsSelected = selectedContextCount == 0 || selectedContextCount == selectedContextItemsCount;
      if (allControlsSelected && allContextsSelected)
        return;

      bool acceptedControls = allControlsSelected;
      if (!allControlsSelected)
        for (int i = 0; i < selectedControlsCount; i++)
        {
          string selected = SelectedControlsBox.SelectedItems[i] as string ?? "";
          if (selected == fsc.Actor)
          {
            acceptedControls = true;
            break;
          }
        }
      bool acceptedContexts = allContextsSelected;
      if (!acceptedContexts)
        for (int i = 0; i < selectedContextCount; i++)
        {
          string selected = SelectedContextBox.SelectedItems[i] as string ?? "";
          if (selected == fsc.ContextName)
          {
            acceptedContexts = true;
            break;
          }
        }
      e.Accepted = acceptedContexts && acceptedControls;
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
      Services.Tracker.PersistAll();
    }

    private void UpdateSelectedControlsBox()
    {
      SelectedControlsBox.SelectedItems.Clear(); // Must be first
      SelectedControlsBox.Items.Clear();
      var da = DistinctActors();
      foreach (string? v in da)
        if (v != null) SelectedControlsBox.Items.Add(v);
    }

    private List<string?> DistinctActors()
    {
      var cgItems =
       CollectionViewSource.GetDefaultView(FSControlGrid.ItemsSource);
      if (cgItems == null)
        return [];
      IEnumerable<FSControl> cg = cgItems.OfType<FSControl>();
      bool hc = HideDebugCheck.IsChecked ?? false;
      if (hc)
        cg = cg
          .Where(x => x.Actor != "Debug");
      return
        [.. cg
        .Select(x => x.Actor)
        .Distinct()
        .OrderBy(x => x)];
    }

    private void UpdateSelectedContextBox()
    {
      SelectedContextBox.SelectedItems.Clear(); // Must be first
      SelectedContextBox.Items.Clear();
      var dc = DistinctContexts();
      foreach (string v in dc)
        SelectedContextBox.Items.Add(v);
    }

    private List<string> DistinctContexts()
    {
      var cgItems =
       CollectionViewSource.GetDefaultView(FSControlGrid.ItemsSource);
      if (cgItems == null)
        return [];
      IEnumerable<FSControl> cg = cgItems
        .OfType<FSControl>();
      bool hc = HideDebugCheck.IsChecked ?? false;
      if (hc)
        cg = cg
          // TODO: or has debug
          .Where(x => x.ContextName != "Debug");
      return
        [.. cg
        .Select(x => x.ContextName)
        .Distinct()
        .OrderBy(x => x)];
    }

    private void HideDebugCheck_Click(object sender, RoutedEventArgs e)
    {
      CollectionViewSource.GetDefaultView(FSControlGrid.ItemsSource).Refresh();
      UpdateSelectedContextBox();
      UpdateSelectedControlsBox();
    }

    private void SelectedControlsBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      CollectionViewSource.GetDefaultView(FSControlGrid.ItemsSource).Refresh();
    }

    private void SelectedContextBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      CollectionViewSource.GetDefaultView(FSControlGrid.ItemsSource).Refresh();
    }

    private void FSControlFileGrid_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
      if (FSControlFileGrid.SelectedItem is not FSControlFile ct) return;
      if (!System.IO.File.Exists(ct.FileName))
      {
        MessageBox.Show("This file was deleted by FS2020; press reload to synchronize");
        return;

      }


      // Once MessageBoxRes is not null, it will not be shown at later calls
      MessageBoxRes ??= MessageBox.Show(
          "When you select <Yes>, the file will be opened with Notepad.\n" +
          "When you select <No>, the full path to the settings will be copied to the clipboard.\n\n" +
          "Any changes to the file are at your own risk.\n" +
          "The file will be deleted and replaced with a new one by FS2020\n" +
          "when you make changes in the control settings.\n\n" +
          "This message is shown only once per session.",
          "Open FS2020 Settings File - Shown only once per session",
          MessageBoxButton.YesNoCancel,
          MessageBoxImage.Question,
          MessageBoxResult.Yes
          );
      switch (MessageBoxRes)
      {
        case MessageBoxResult.Yes:
          Process.Start("notepad.exe", ct.FileName);
          break;
        case MessageBoxResult.No:
          Clipboard.SetText(ct.FileName);
          MessageBox.Show($"Copied to clipboard:\n{ct.FileName}", "FS2020Control",
             MessageBoxButton.OK, MessageBoxImage.Information );
          break;
      }

    }

    private void Pdf_Click(object sender, RoutedEventArgs e)
    {
      if (FSControlFileGrid.SelectedItem is not FSControlFile cf) return;
      using (new WaitCursor())
      {
        string pdfFile = Export.ItemsCollectionToPdf(FSControlGrid.Items,
          controlContext.DbDir, cf.Device, cf.FriendlyName);
        Export.OpenWithDefaultProgram(pdfFile);
      }
    }

    private void Excel_Click(object sender, RoutedEventArgs e)
    {
      if (FSControlFileGrid.SelectedItem is not FSControlFile cf) return;
      string xlFile = Export.ItemsCollectionToExcel(FSControlGrid.Items,
        controlContext.DbDir, cf.Device, cf.FriendlyName);
      Export.OpenWithDefaultProgram(xlFile);

    }

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
      using (new WaitCursor())
      {
        controlContext.ChangeTracker.Clear();
        controlContext.FSControlsFile.ExecuteDelete();
        controlContext.ChangeTracker.Clear();
        LoadData();
      }
    }


    void FSControlFile_GridViewColumnHeaderClick(object sender,
                                               RoutedEventArgs e)
    {
      ListSortDirection direction;

      if (e.OriginalSource is not GridViewColumnHeader headerClicked) return;
      if (headerClicked.Role == GridViewColumnHeaderRole.Padding) return;
      if (headerClicked != lastHeaderClicked)
      {
        direction = ListSortDirection.Ascending;
      }
      else
      {
        if (lastDirection == ListSortDirection.Ascending)
        {
          direction = ListSortDirection.Descending;
        }
        else
        {
          direction = ListSortDirection.Ascending;
        }
      }

      var columnBinding = headerClicked.Column.DisplayMemberBinding as Binding;
      string? sortBy = columnBinding?.Path.Path ?? headerClicked.Column.Header as string;

      if (sortBy != null)
        Sort(sortBy, direction);

      if (direction == ListSortDirection.Ascending)
      {
        headerClicked.Column.HeaderTemplate =
          Resources["HeaderTemplateArrowUp"] as DataTemplate;
      }
      else
      {
        headerClicked.Column.HeaderTemplate =
          Resources["HeaderTemplateArrowDown"] as DataTemplate;
      }

      // Remove arrow from previously sorted header
      if (lastHeaderClicked != null && lastHeaderClicked != headerClicked)
      {
        lastHeaderClicked.Column.HeaderTemplate = null;
      }

      lastHeaderClicked = headerClicked;
      lastDirection = direction;
    }

    private void Sort(string sortBy, ListSortDirection direction)
    {
      ICollectionView dataView =
        CollectionViewSource.GetDefaultView(FSControlFileGrid.ItemsSource);

      dataView.SortDescriptions.Clear();
      SortDescription sd = new(sortBy, direction);
      dataView.SortDescriptions.Add(sd);
      dataView.Refresh();
    }

    public partial class WaitCursor : IDisposable
    {
      private readonly Cursor _previousCursor;

      public WaitCursor()
      {
        _previousCursor = Mouse.OverrideCursor;

        Mouse.OverrideCursor = Cursors.Wait;
      }

      #region IDisposable Members

      public void Dispose()
      {
        GC.SuppressFinalize(this);
        Mouse.OverrideCursor = _previousCursor;
      }

      #endregion
    }
  }
}
