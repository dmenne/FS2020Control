using FS2020Control;

namespace FS2020ControlNunitTest
{
  public class ControlContextTest
  {
    readonly FSControlFile sampleFile = new FSControlFile
    {
      FileName = "asdfk",
      FriendlyName = "Dieter",
      Device = "keyboard"

    };

    readonly FSControl sampleControlNoSecondary = new()
    {
      ActionName = "KEY_COCKPIT_QUICKVIEW1",
      Actor = "Key",
      ContextName = "ContextName",
      FriendlyAction = "Cockpit Quickviews 1",
      PrimaryKeys = "primary",
      PrimaryKeysCode = "13"
    };

    readonly FSControl sampleControlAll = new()
    {
      ActionName = "KEY_COCKPIT_QUICKVIEW5",
      Actor = "Key",
      ContextName = "ContextName",
      FriendlyAction = "Cockpit Quickviews 5",
      PrimaryKeys = "allprimary",
      PrimaryKeysCode = "16",
      SecondaryKeys = "allsecondary",
      SecondaryKeysCode = "25"
    };

    [Test]
    public void DatabaseIsCreatedWhenItDoesNotExist()
    {
      using var ct = new ControlContext(test: true);
      {
        ct.Database.EnsureDeleted();
        Assert.IsFalse(File.Exists(ct.DbPath));
        ct.Database.EnsureCreated();
        Assert.IsNotNull(ct);
        Assert.IsTrue(File.Exists(ct.DbPath));
        ct.Database.EnsureDeleted();
      }
    }

    [Test]
    public void CanSaveControlData()
    {
      using var ct = new ControlContext(test: true);
      {
        ct.Database.EnsureDeleted();
        ct.Database.EnsureCreated();

        sampleFile.FSControls = new List<FSControl>
        {
          sampleControlAll,
          sampleControlNoSecondary
        };
        ct.Add(sampleFile);
        ct.SaveChanges();

        Assert.Multiple(() =>
        {
          Assert.That(ct.FSControlsFile.Count(), Is.EqualTo(1));
          Assert.That(ct.FSControls.Count(), Is.EqualTo(2));
        });
        ct.Remove(sampleControlAll);
        ct.SaveChanges();
        Assert.That(ct.FSControls.Count(), Is.EqualTo(1));
        // Check cascade
        ct.Remove(sampleFile);
        ct.SaveChanges();
        Assert.That(ct.FSControlsFile.Count(), Is.EqualTo(0));
      }
    }
  }
}