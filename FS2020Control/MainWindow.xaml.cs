﻿using Microsoft.EntityFrameworkCore;
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
using MsgBoxEx;
using System.Windows.Interop;

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
    MessageBoxResult? MessageBoxRes = null;
    private bool FromDatabase { get; set; }

    public MainWindow()
    {
      InitializeComponent();
#pragma warning disable CS8601 // Possible null reference assignment.
      fsControlFileViewSource =
        FindResource(nameof(fsControlFileViewSource)) as CollectionViewSource;
      fsControlViewSource =
        FindResource(nameof(fsControlViewSource)) as CollectionViewSource;
#pragma warning restore CS8601 // Possible null reference assignment.
      var tracker = Services.Tracker;
      tracker.Track(this);
      tracker.Track(HideDebugCheck);
      var assembly = System.Reflection.Assembly.GetExecutingAssembly()?
        .GetName()?.Version?.ToString();
      Title = "FS2020 Controls - " + assembly;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
      MessageBoxEx.SetFont("Arial", 15.0);
      LoadData();
    }

    private void LoadData()
    {
     
      controlContext.Database.EnsureCreated();
      // Check if recent version
      try
      {
        var x = controlContext.FSControls.ToList();
      }
      catch {
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
        MessageBoxEx.Show(ex.Message, "FS2020 Controls");
      }
      SettingsLabel.Content = FromDatabase ? "From Database": (
        xh.IsSteam ? "Steam controls" : "Store controls");
      UpdateSelectedControlsBox();
    }

    private void FSControlFileGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
    {
      fsControlViewSource.SortDescriptions.Clear();
      fsControlViewSource.SortDescriptions.Add(new SortDescription("FriendlyAction", ListSortDirection.Ascending));
      UpdateSelectedControlsBox();
    }

    private void CollectionViewSource_Filter(object sender, FilterEventArgs e)
    {
      FSControl? fsc = e.Item as FSControl;
      if (fsc == null) return;
      bool hc = HideDebugCheck.IsChecked ?? false;
      if (hc)
      {
        Match m = Regex.Match(fsc.FriendlyAction!, @"Debug|Devmode",
                  RegexOptions.IgnoreCase);
        e.Accepted = !m.Success;
      }
      // When all or none are selected, use all
      int selectedCount = SelectedControlsBox.SelectedItems.Count;
      int itemsCount = SelectedControlsBox.Items.Count;
      if (selectedCount == 0 || selectedCount == itemsCount)
        return;
      bool accepted = false;
      for (int i = 0; i < selectedCount; i++)
      {
        string selected = SelectedControlsBox.SelectedItems[i] as string ?? "";
        if (selected == fsc.Actor)
        {
          accepted = true;
          break;
        }
      }
      e.Accepted = accepted;
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
      for (int i = 0; i < da.Count; i++)
        SelectedControlsBox.Items.Add(da[i]);
    }

    private List<string?> DistinctActors()
    {
      var cgItems =
       CollectionViewSource.GetDefaultView(FSControlGrid.ItemsSource);
      if (cgItems == null)
        return new List<string?>();
      IEnumerable<FSControl> cg = cgItems
        .OfType<FSControl>();
      bool hc = HideDebugCheck.IsChecked ?? false;
      if (hc)
        cg = cg
          .Where(x => x.Actor != "Debug");
      return
        cg
        .Select(x => x.Actor)
        .Distinct()
        .OrderBy(x => x)
        .ToList();
    }

    private void HideDebugCheck_Click(object sender, RoutedEventArgs e)
    {
      CollectionViewSource.GetDefaultView(FSControlGrid.ItemsSource).Refresh();
      UpdateSelectedControlsBox();
    }

    private void SelectedControlsBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      CollectionViewSource.GetDefaultView(FSControlGrid.ItemsSource).Refresh();
    }

    private void FSControlFileGrid_PreviewMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
      if (FSControlFileGrid.SelectedItem is not FSControlFile ct) return;
      MessageBoxRes ??= MessageBoxEx.Show(
          "This message will be shown only once per session.\n" +
          "When you select <Yes>, the file will be opened with Notepad.\n" +
          "When you select <No>, the full path to the settings will be copied to the clipboard.\n" +
          "Any changes to the file are at your own risk.\n" +
          "The file will be deleted and replaced with a new one when\n" +
          "you make changes in the control settings of FS2020.",
          "Open FS2020 Settings File",
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
        controlContext.FSControlsFile.RemoveRange(controlContext.FSControlsFile);
        controlContext.SaveChanges();
        LoadData();
      }
    }
  }

  public class WaitCursor : IDisposable
  {
    private Cursor _previousCursor;

    public WaitCursor()
    {
      _previousCursor = Mouse.OverrideCursor;

      Mouse.OverrideCursor = Cursors.Wait;
    }

    #region IDisposable Members

    public void Dispose()
    {
      Mouse.OverrideCursor = _previousCursor;
    }

    #endregion
  }
}
