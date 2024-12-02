using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System.Data;
using System.Globalization;
using System.Text;
using System.Xml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FS2020Control
{
  public class XmlToSqlite(ControlContext? ct = null)
  {
    public string? FS2020RootDir { get; private set; } = "";
    public string? FS2020ContainerDir { get; private set; } = "";
    public string[] XmlFiles { get; private set; } = default!;
    public ControlContext? Context { get; } = ct;
    public bool IsSteam { get; set; }

    public void CheckInstallations()
    {
#if DEBUG
      bool forceSteam = false; //Debug 
#else
      bool forceSteam = false; // Always check for Store first
#endif
      if (!forceSteam)
        CheckStandardInstallation();
      if (FS2020ContainerDir == "" || forceSteam)
        CheckSteamInstallation();
    }

    private static (string file, DateTime date)
      GetLastFileWriteTime(string path, string pattern = "inputprofile_*")
    {
      var fs = Directory.EnumerateFiles(path, pattern);
      (string file, DateTime date) emptyTuple = default;
      if (!fs.Any())
        return emptyTuple;
      return fs
        .Select(f => (file: f, date: File.GetLastWriteTime(f)))
        .OrderBy(f => f.date)
        .Last();
    }

    private void CheckSteamInstallation()
    {
      string? steamPath =
         Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
      if (steamPath == null || steamPath == "")
        return;
      string appPath = $@"{steamPath}\userdata";
      // Assumption: 1250410 is FS2020 token
      // https://forums.flightsimulator.com/t/anyone-know-where-your-controller-config-is-stored-on-disk/241669/9
      var dirs = Directory.EnumerateDirectories(appPath, "*.*",
        SearchOption.AllDirectories)
      .Where(dir => dir.EndsWith("1250410\\remote"))
      .ToList();

      if (dirs.Count == 0) return;
      // When there is only one directory, use it
      if (dirs.Count == 1)
      {
        appPath = dirs[0];
      } else // When there are multiple directories, use that one with the last changed file
      {
        var (file, date) =
          dirs.Select(f => GetLastFileWriteTime(f))
          .OrderBy(f => f.date)
          .Last();

        appPath = Path.GetDirectoryName(file) ?? "";
      }

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
      var line = new byte[6];
      // https://github.com/dmenne/FS2020Control/pull/4/files
      using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
        fs.ReadExactly(line);
      return Encoding.Default.GetString(line).StartsWith("<?xml");
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
      if (FS2020ContainerDir == null || FS2020ContainerDir == "")
        return 0;
      string pattern = IsSteam ? "inputprofile_*" : "*.";
      List<string> xmlFiles =
        Directory.GetFiles(FS2020ContainerDir, pattern, SearchOption.AllDirectories)
        .Where(path => IsXmlFile(path))
        .ToList();
      int savedToDb = 0;

      foreach (string? xmlFile in xmlFiles)
      {
        savedToDb += ImportXmlFile(xmlFile);
      }
      XmlFiles = [.. xmlFiles];
      return savedToDb;
    }

    private static string MakeValidXml(string path)
    {
      string[] rawFile = File.ReadAllLines(path);
      if (!rawFile[1].Trim().StartsWith("<Version"))
        throw new FS2020Exception($"No <Version.. \n{path}");
      // Replace <Version> 
      rawFile[1] = "<FS2020>"; // Dummy root element
      // Append Closing (using "from end" = ^ operator)
      rawFile[^1] = $"{rawFile[^1]}</FS2020>";
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
      return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(str.ToLower())
        .Replace("_", " ");
    }

    public int ImportXmlFile(string filePath)
    {
      string friendlyName;     
      XmlDocument doc = new();
      doc.LoadXml(MakeValidXml(filePath));

      XmlNode dNode = (doc.DocumentElement?.SelectSingleNode("/FS2020/Device")) ??
        throw new FS2020Exception("No Device " + filePath);
      string deviceName = dNode.Attributes?["DeviceName"]?.Value ??
        throw new FS2020Exception("No Device Name" + filePath);

      XmlNode fNode = (doc.DocumentElement?.SelectSingleNode("/FS2020/FriendlyName")) ??
        throw new FS2020Exception("No friendly name found in " + filePath);
      friendlyName = fNode.InnerText;
      XmlNodeList cNodes = (doc.DocumentElement?.SelectNodes("/FS2020/Device/Context")) ??
        throw new FS2020Exception("No ContextName found in " + filePath);

      List<FSControl> fsControls = new();      
      foreach (XmlNode? currentNode in cNodes)
      {
        if (currentNode == null) continue;
        string contextName = currentNode.Attributes?["ContextName"]?.Value ?? "";
        contextName = ToTitleCase(contextName) ?? "";
        List<FSControl> fsControlsContext = SaveActions(filePath, currentNode, contextName);
        fsControls = fsControls.Concat(fsControlsContext).ToList();
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
      var ret = Context?.SaveChanges() ?? 0;
      return ret;
    }

    private static List<FSControl> SaveActions(string filePath, XmlNode currentNode, string contextName)
    {
      XmlNodeList nodes = (currentNode.SelectNodes("Action")) ??
        throw new FS2020Exception("No actions in " + filePath);

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
        FSControl ctl = new()
        {
          ActionName = actionName
        };
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
        ctl.ContextName = contextName;
        fsControls.Add(ctl);
      }

      return fsControls;
    }
  }

}
