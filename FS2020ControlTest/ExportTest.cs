using ClosedXML.Excel;

namespace FS2020ControlNunitTest
{
  public class ExportTest
  {
    record Pastry(
      string Name, int Sales
    );

    [Test]
    public void CanCreateExcelFile()
    {
      // https://closedxml.readthedocs.io/en/latest/features/tables.html#table-creation
      using var wb = new XLWorkbook();
      var ws = wb.AddWorksheet();
      ws.ColumnWidth = 12;
      var tb = new[] {
        new Pastry("Pie", 10),
        new Pastry("Cake", 7),
        new Pastry("Waffles", 17)
     };
      ws.FirstCell().InsertTable(tb, "PastrySales", true);

      ws.Range("D2:D5").CreateTable("Table");
      string outFile = Path.Combine(Path.GetTempPath(), "tables-create.xlsx");
      wb.SaveAs(outFile);
      Assert.That(File.Exists(outFile), Is.True);
      File.Delete(outFile);
    }

  }
}