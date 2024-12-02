using FS2020Control;

using System.Text.RegularExpressions;

namespace FS2020ControlNunitTest
{
  public class XmlToSqliteTest
  {
    // There is no simple method to find out if FS is installed
    // Even ChatGPT returns unusable code
    // Gave up, set it manually
    private readonly bool NoFS = false;
    [Test]
    public void PathToFSMustExist()
    {
      if (NoFS) return;
      var xh = new XmlToSqlite();
      xh.CheckInstallations();
      Assert.That(Directory.Exists(xh.FS2020RootDir), Is.True);
      Assert.That(Directory.Exists(xh.FS2020ContainerDir), Is.True);
    }

    [Test]
    public void ImportedFilesMustBeXML()
    {
      if (NoFS) return;
      // No database
      var xh = new XmlToSqlite();
      xh.CheckInstallations();
      xh.ImportXmlFiles();
      Assert.That(xh.XmlFiles.Length > 10, Is.True);
    }

    [Test]
    public void UseDatabaseWhenDataAreAvailableOneFile()
    {
      // This test works without FS installed
      using var ct = new ControlContext(test: true);
      Assert.That(ct, Is.Not.Null);
      ct.Database.EnsureDeleted();
      ct.Database.EnsureCreated();
      Assert.IsFalse(ct.HasData);

      var xh = new XmlToSqlite(ct);
      xh.CheckInstallations();
      string big_store = "../../../testdata/big_store.xml";
      Assert.That(File.Exists(big_store), Is.True);
      int x = xh.ImportXmlFile(big_store);
      Assert.That(x > 0, Is.True);
    }

    [Test]
    public void UseDatabaseWhenDataAreAvailableAllFiles()
    {
      if (NoFS) return;
      using var ct = new ControlContext(test: true);
      Assert.IsNotNull(ct);
      ct.Database.EnsureDeleted();
      ct.Database.EnsureCreated();
      Assert.That(ct.HasData, Is.False);

      var xh = new XmlToSqlite(ct);
      xh.CheckInstallations();
      xh.ImportXmlFiles();

      int? keys = xh.Context?.FSControls.Count();
      Assert.Multiple(() =>
      {
        Assert.That(ct.HasData, Is.True);
        Assert.That(keys, Is.Not.Null);
      });
      Assert.That(keys > 1000, Is.True);
    }


  }
}