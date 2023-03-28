using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml;
using System.Text;
using System.Text.Json;
using System.Data;
using FS2020Control;
using Microsoft.EntityFrameworkCore;

namespace FS2020Controls
{
  public class FS2020Exception : Exception
  {
    public FS2020Exception(string message) : base(message)
    {
    }
  }

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
    public string FS2020RootDir { get; private set; }
    public string FS2020ContainerDir { get; private set; }
    public string[] XmlFiles { get; private set; } = default!;
    public ControlContext? Context { get; }

    public XmlToSqlite(ControlContext? ct = null)
    {
      Context = ct;
      FS2020RootDir = "";
      string localDir = Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Packages");
      List<string> rootDir = Directory.GetDirectories(localDir)
         .Where(path => path.Contains("Microsoft.FlightSimulator"))
         .ToList();
      if (rootDir.Count == 0)
      {
        throw new FS2020Exception("No Flight Simulator Directory found");
      }
      if (rootDir.Count > 1)
      {
        throw new FS2020Exception(
          "Multiple Flight Simulator Directories found; don't know which one you want");
      }
      FS2020RootDir = Path.Combine(rootDir[0], "SystemAppData", "wgs");
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
      // Directory name below this will change whenever control settings are changes
    }

    private bool IsXmlFile(string path)
    {
      if (path.Length < FS2020ContainerDir.Length + 32)
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
      List<string> xmlFiles =
        Directory.GetFiles(FS2020ContainerDir, "*.", SearchOption.AllDirectories)
        .Where(path => IsXmlFile(path))
        .ToList();
      int savedToDb = 0;

      foreach (string xmlFile in xmlFiles)
      {
        savedToDb += ImportXmlFile(xmlFile);
      }
      XmlFiles = xmlFiles.ToArray();
      return savedToDb;
    }

    private static string MakeValidXml(string path)
    {
      string[] rawFile = File.ReadAllLines(path);
      if (!rawFile[1].StartsWith("<Version"))
        throw new FS2020Exception("Second line does not start with <Version.. \n" + path);
      // Replace <Version>
      rawFile[1] = "<FS2020>";
      // Append Closing 
      rawFile[rawFile.Length - 1] = rawFile[rawFile.Length - 1] + "</FS2020>";
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

    public static string ToTitleCase(string str)
    {
      return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(str.ToLower());
    }

    private int ImportXmlFile(string filePath)
    {
      XmlDocument doc = new XmlDocument();
      doc.LoadXml(MakeValidXml(filePath));

      XmlNode fNode = (doc.DocumentElement?.SelectSingleNode("/FS2020/FriendlyName")) ??
        throw new FS2020Exception("No friendly name found in " + filePath);
      XmlNode dNode = (doc.DocumentElement?.SelectSingleNode("/FS2020/Device")) ??
        throw new FS2020Exception("No Device " + filePath);
      string deviceName = dNode.Attributes?["DeviceName"]?.Value ??
        throw new FS2020Exception("No Device Name" + filePath);
      XmlNodeList nodes = (doc.DocumentElement?.SelectNodes("//Action")) ??
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
        FriendlyName = fNode.InnerText,
        FileName = filePath,
        FSControls = fsControls
      };
      Context.Add(fsControlFile);
      return Context?.SaveChanges() ?? 0;
    }
  }

}
