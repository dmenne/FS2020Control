using Microsoft.EntityFrameworkCore;
using System.IO;
using System;
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
    }

    public bool HasData { 
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
