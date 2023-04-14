using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Xml;

namespace FS2020Control
{
  static class ObjectHelper
  {
    public static void Dump<T>(this T x)
    {
      string json = JsonSerializer.Serialize(x);
      Console.WriteLine(json);
    }
  }

  internal class XmlToSqlite

  {
    public string? FS2020RootDir { get; private set; }
    public string? FS2020ContainerDir { get; private set; }
    public string[] XmlFiles { get; private set; } = default!;
    public ControlContext? Context { get; }
    public bool IsSteam { get; set; }

    public XmlToSqlite(ControlContext? ct = null)
    {
      Context = ct;
      FS2020RootDir = "";
    }

    public void CheckInstallations()
    {
#if DEBUG
      bool forceStore = true; // Debug 
#else
      bool forceStore = false; // Always check for Steam in release
#endif
    if (!forceStore)
    {
      CheckSteamInstallation();
    }
    if (FS2020ContainerDir == "" || forceStore)
        CheckStandardInstallation();
    }

    private void CheckSteamInstallation()
    {
#pragma warning disable CA1416 // Use pattern matching
      var steamPath =
         Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
#pragma warning restore CA1416 // Use pattern matching
      if (steamPath == null || steamPath == "")
        throw new FS2020Exception("No settings for standard or Steam installation found");
      string appPath = $"{steamPath}\\steamapps\\common\\MicrosoftFlightSimulator\\Input";
      if (!Directory.Exists(appPath))
        throw new FS2020Exception(
          String.Join(Environment.NewLine,
          "According to the registry, the directory ", "",
          appPath, "",
          "should contain the input files.",
          "This directory could not be found on your computer"));
      FS2020ContainerDir = appPath;
      IsSteam = true;
    }

    private void CheckStandardInstallation()
    {
      string localDir = Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Packages");
      if (!Directory.Exists(localDir)) return;
      List<string> rootDir = Directory.GetDirectories(localDir)
         .Where(path => path.Contains("Microsoft.FlightSimulator"))
         .ToList();
      if (rootDir.Count == 0)
      {
        return;
      }
      if (rootDir.Count > 1)
      {
        throw new FS2020Exception(
          "Multiple Flight Simulator Directories found; don't know which one you want");
      }
      FS2020RootDir = Path.Combine(rootDir[0], "SystemAppData", "wgs");
      if (!Directory.Exists(FS2020RootDir))
        return;
      List<string> containerDir = Directory.GetDirectories(FS2020RootDir)
        .Where(path => path.Length >= FS2020RootDir.Length + 48)
        .ToList();
      if (containerDir.Count == 0)
      {
        throw new FS2020Exception("No settings directories found");
      }
      if (containerDir.Count > 1)
      {
        throw new FS2020Exception(
          "Multiple Settings Directories found; don't know which one you want");
      }
      FS2020ContainerDir = containerDir[0];
      IsSteam = false;
      // Directory name below this will change whenever control settings are changed
    }

    private bool IsXmlFile(string path)
    {
      if (FS2020ContainerDir == null)
        return false;
      if (!IsSteam && path.Length < FS2020ContainerDir.Length + 32)
        return false;
      string line = File.ReadLines(path).First();
      return line.StartsWith("<?xml ");
    }

    public int ImportXmlFiles()
    {
      // Clean database table
      if (Context != null)
      {
        Context.FSControls.RemoveRange(Context.FSControls);
        Context.SaveChanges();
        Context.Database.ExecuteSql($"UPDATE sqlite_sequence SET seq = 0 WHERE name = 'FSControls'");
      }
      if (FS2020ContainerDir == null)
        return 0;
      string pattern = IsSteam ? "*.xml" : "*.";
      List<string> xmlFiles =
        Directory.GetFiles(FS2020ContainerDir, pattern, SearchOption.AllDirectories)
        .Where(path => IsXmlFile(path))
        .ToList();
      int savedToDb = 0;

      foreach (string? xmlFile in xmlFiles)
      {
        savedToDb += ImportXmlFile(xmlFile);
      }
      XmlFiles = xmlFiles.ToArray();
      return savedToDb;
    }

    private string MakeValidXml(string path)
    {
      string[] rawFile = File.ReadAllLines(path);
      int vLine = IsSteam ? 2 : 1;
      if (!rawFile[vLine].Trim().StartsWith("<Version"))
        throw new FS2020Exception("No <Version.. \n" + path);
      if (IsSteam) return String.Concat(rawFile);
      // Replace <Version>
      rawFile[1] = "<DefaulftInput>";
      // Append Closing 
      rawFile[rawFile.Length - 1] = rawFile[rawFile.Length - 1] + "</DefaulftInput>";
      return String.Concat(rawFile);
    }

    private static string ExtractKeys(XmlNode keyNode)
    {
      StringBuilder s = new();
      foreach (XmlNode? key in keyNode.ChildNodes)
      {
        if (key == null) continue;
        if (s.Length != 0) s.Append('-');
        s.Append(key.Attributes?["Information"]?.Value);
      }
      return s.ToString();
    }

    private static string ExtractKeysCode(XmlNode keyNode)
    {
      StringBuilder s = new();
      foreach (XmlNode? key in keyNode.ChildNodes)
      {
        if (key == null) continue;
        if (s.Length != 0) s.Append(',');
        s.Append(key.InnerXml);
      }
      return s.ToString();
    }

    public static string? ToTitleCase(string str)
    {
      return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(str.ToLower());
    }

    private int ImportXmlFile(string filePath)
    {
      string friendlyName;
      XmlDocument doc = new XmlDocument();
      doc.LoadXml(MakeValidXml(filePath));

      XmlNode dNode = (doc.DocumentElement?.SelectSingleNode("/DefaulftInput/Device")) ??
        throw new FS2020Exception("No Device " + filePath);
      string deviceName = dNode.Attributes?["DeviceName"]?.Value ??
        throw new FS2020Exception("No Device Name" + filePath);
      XmlNodeList nodes = (doc.DocumentElement?.SelectNodes("//Action")) ??
        throw new FS2020Exception("No actions in " + filePath);
      if (IsSteam)
      {
        XmlNode? pNode = (doc.DocumentElement?.SelectSingleNode("/DefaulftInput"));
        friendlyName = pNode?.Attributes?["PlatformAvailability"]?.Value ?? "Default";
      }
      else
      {
        XmlNode fNode = (doc.DocumentElement?.SelectSingleNode("/DefaulftInput/FriendlyName")) ??
          throw new FS2020Exception("No friendly name found in " + filePath);
        friendlyName = fNode.InnerText;
      }
      var fsControls = new List<FSControl>();
      foreach (XmlNode? xnode in nodes)
      {
        if (xnode == null) continue;
        // Action
        string? actionName = xnode.Attributes?["ActionName"]?.Value;
        if (actionName == null) continue;
        // Primary keys
        XmlNode? primary = xnode.SelectSingleNode("Primary");
        if (primary == null) continue;
        string primaryKeys = ExtractKeys(keyNode: primary);
        string primaryKeysCode = ExtractKeysCode(keyNode: primary);
        if (primaryKeys == "" || primaryKeysCode == "") continue;
        // Secondary keys (not required)
        XmlNode? secondary = xnode.SelectSingleNode("Secondary");
        string? secondaryKeys = null;
        string? secondaryKeysCode = null;
        if (secondary != null)
        {
          secondaryKeys = ExtractKeys(keyNode: secondary) ?? null;
          secondaryKeysCode = ExtractKeysCode(keyNode: secondary) ?? null;
        }

        // Copy to entity
        FSControl ctl = new FSControl();
        ctl.ActionName = actionName;
        string[] actionSplit = actionName.Split('_');
        ctl.Actor = ToTitleCase(actionSplit[0]);
        if (actionName.Length > 1)
        {
          // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-8.0/ranges
          var slice = ToTitleCase(String.Join(' ', actionSplit[1..^0]));
          ctl.FriendlyAction = slice;

        }
        ctl.PrimaryKeys = primaryKeys;
        ctl.PrimaryKeysCode = primaryKeysCode;
        ctl.SecondaryKeys = secondaryKeys;
        ctl.SecondaryKeysCode = secondaryKeysCode;
        if (Context == null)
          ctl.Dump();
        fsControls.Add(ctl);
      }
      if (Context == null) return 0;
      var fsControlFile = new FSControlFile
      {
        Device = deviceName,
        FriendlyName = friendlyName,
        FileName = filePath,
        FSControls = fsControls
      };
      Context.Add(fsControlFile);
      return Context?.SaveChanges() ?? 0;
    }
  }

}
