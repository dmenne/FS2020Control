using FS2020Control;
using FS2020Controls;
using System.Text.RegularExpressions;

namespace FS2020ControlTest
{
  [TestClass]
  public class XmlToSqliteTest
  {

    private static bool NoFS {
      // Cannot run tests on github actions
      get {
        var xh = new XmlToSqlite();
        return  !Directory.Exists(xh.FS2020RootDir);
    } }


    [TestMethod]
    public void PathToFSMustExist()
    {
      if (NoFS) return;
      var xh = new XmlToSqlite();
      Assert.IsTrue(Directory.Exists(xh.FS2020RootDir));
      Assert.IsTrue(Directory.Exists(xh.FS2020ContainerDir));
    }

    [TestMethod]
    public void ImportedFilesMustBeXML()
    {
      if (NoFS) return;
      // No database
      var xh = new XmlToSqlite();
      xh.ImportXmlFiles();
      Assert.IsTrue(xh.XmlFiles.Length > 10);
    }

    [TestMethod]
    public void ImportXmlWithoutDataContext()
    {
      if (NoFS) return;
      var xh = new XmlToSqlite();
      var s = new StringWriter();
      Console.SetOut(s);
      xh.ImportXmlFiles();
      Assert.IsTrue(Regex.IsMatch(s.ToString(), "\"FSControlId\""));
    }

    [TestMethod]
    public void UseDatabaseWhenDataAreAvailable()
    {
      if (NoFS) return;
      using (var ct = new ControlContext(test: true))
      {
        Assert.IsNotNull(ct);
        ct.Database.EnsureDeleted();
        ct.Database.EnsureCreated();
        Assert.IsFalse(ct.HasData);

        var xh = new XmlToSqlite(ct);
        xh.ImportXmlFiles();

        int? keys = xh.Context?.FSControls.Count();
        Assert.IsTrue(ct.HasData);
        Assert.IsNotNull(keys);
        Assert.IsTrue(keys > 10);
      }
      using (var ct = new ControlContext(test: true))
      {
        Console.WriteLine(ct.FSControlsFile.First().FileName);
      }
    }
  }
}