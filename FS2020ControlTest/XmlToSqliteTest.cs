using FS2020Control;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace FS2020ControlTest
{

  [SupportedOSPlatform("windows")]
  [TestClass]
  public class XmlToSqliteTest
  {
    // There is no simple method to find out if FS is installed
    // Even ChatGPT returns unusable code
    // Gave up, set it manually
    private readonly bool NoFS = false; 

    [TestMethod]
    public void PathToFSMustExist() {
      if (NoFS) return;
      var xh = new XmlToSqlite();
      xh.CheckInstallations();
      Assert.IsTrue(Directory.Exists(xh.FS2020RootDir));
      Assert.IsTrue(Directory.Exists(xh.FS2020ContainerDir));
    }

    [TestMethod]
    public void ImportedFilesMustBeXML()
    {
      if (NoFS) return;
      // No database
      var xh = new XmlToSqlite();
      xh.CheckInstallations();
      xh.ImportXmlFiles();
      Assert.IsTrue(xh.XmlFiles.Length > 10);
    }

    [TestMethod]
    public void ImportXmlWithoutDataContext()
    {
      if (NoFS) return;
      var xh = new XmlToSqlite();
      xh.CheckInstallations();
      var s = new StringWriter();
      Console.SetOut(s);
      xh.ImportXmlFiles();
      Assert.IsTrue(Regex.IsMatch(s.ToString(), "\"FSControlId\""));
    }

    [TestMethod]
    public void UseDatabaseWhenDataAreAvailableOneFile()
    {
      // This test works without FS installed
      using var ct = new ControlContext(test: true);
      Assert.IsNotNull(ct);
      ct.Database.EnsureDeleted();
      ct.Database.EnsureCreated();
      Assert.IsFalse(ct.HasData);

      var xh = new XmlToSqlite(ct);
      xh.CheckInstallations();
      string big_store = "../../../testdata/big_store.xml";
      Assert.IsTrue(File.Exists(big_store));
      int x = xh.ImportXmlFile(big_store);
      Assert.IsTrue(x > 0);
    }

    [TestMethod]
    public void UseDatabaseWhenDataAreAvailableAllFiles()
    {
      if (NoFS) return;
      using var ct = new ControlContext(test: true);
      Assert.IsNotNull(ct);
      ct.Database.EnsureDeleted();
      ct.Database.EnsureCreated();
      Assert.IsFalse(ct.HasData);

      var xh = new XmlToSqlite(ct);
      xh.CheckInstallations();
      xh.ImportXmlFiles();

      int? keys = xh.Context?.FSControls.Count();
      Assert.IsTrue(ct.HasData);
      Assert.IsNotNull(keys);
      Assert.IsTrue(keys > 1000);
    }
  }
}