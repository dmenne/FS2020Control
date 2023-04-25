using Microsoft.EntityFrameworkCore;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace FS2020Control
{
  // https://learn.microsoft.com/de-de/ef/core/get-started/overview/first-app?tabs=netcore-cli

  internal class ControlContext : DbContext
  {
    public string DbPath { get; }
    public string DbDir { get; }

    public ControlContext(bool test = false)
    {
      DbDir = Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.Personal), "FS2020Keys");
      if (!Directory.Exists(DbDir))
      {
        Directory.CreateDirectory(DbDir);
      }
      string baseName = test ? "FS2020Test.sqlite" : "FS2020.sqlite";
      DbPath = Path.Combine(DbDir, baseName);
      TruncateDatabaseIfOlderThanExe();
    }

    private void TruncateDatabaseIfOlderThanExe()
    {
      if (!File.Exists(DbPath)) return;
      string exeFile = Path.Combine(Directory.GetCurrentDirectory(), "FS2020Control.exe");
      DateTime lastWriteExe = File.GetLastWriteTime(exeFile);
      DateTime lastWriteDb = File.GetLastWriteTime(DbPath);
      TimeSpan diffTime = lastWriteExe - lastWriteDb;
      if (diffTime.TotalSeconds > 0)
      {
        File.Delete(DbPath);
        Debug.WriteLine(@"Your database file was created by an earlier version of the program. It has been deleted and will be recreated.");
      }
    }

    public bool HasData
    {
      get
      {
        return FSControlsFile.Any() &&
          FSControls.Any();
      }
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
      optionsBuilder.UseSqlite($"Data Source={DbPath}");
      optionsBuilder.UseLazyLoadingProxies();
    }


    public DbSet<FSControlFile> FSControlsFile { get; set; } = default!;
    public DbSet<FSControl> FSControls { get; set; } = default!;


  }
}
