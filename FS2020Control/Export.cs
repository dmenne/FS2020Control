//Imports
using ClosedXML.Excel;
using iText.Kernel.Colors;
using iText.Kernel.Events;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Layout.Element;
using iText.Layout.Properties;
using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows.Controls;
using iBorders = iText.Layout.Borders;
using iLayout = iText.Layout;

namespace FS2020Control
{
  internal class TopEventHandler : IEventHandler
  {
    private readonly string title;

    public TopEventHandler(string title)
    {
      this.title = title;
    }

    public virtual void HandleEvent(Event evt)
    {
      PdfDocumentEvent docEvent = (PdfDocumentEvent)evt;
      PdfDocument pdfDoc = docEvent.GetDocument();
      PdfPage page = docEvent.GetPage();
      int pageNumber = pdfDoc.GetPageNumber(page);
      Rectangle pageSize = page.GetPageSize();
      PdfCanvas pdfCanvas = new(page.NewContentStreamBefore(), page.GetResources(), pdfDoc);

      // Add watermark
      iLayout.Canvas canvas = new iLayout.Canvas(pdfCanvas, pageSize);
      canvas.SetFontColor(ColorConstants.BLUE);
      canvas.SetFontSize(25);
      // Strange: Documentation says this should be angle in degree
      float angle = (float)(Math.PI / 2.0);
      string sideText = $"{title} Page {pageNumber}";
      canvas.ShowTextAligned(sideText,
          pageSize.GetWidth() - 10,
          pageSize.GetHeight() / 2,
          TextAlignment.CENTER,
          VerticalAlignment.BOTTOM,
          angle);
      pdfCanvas.Release();
    }
  }

  internal static class Export
  {
    private const int LastColumn = 16384;
    public static void OpenWithDefaultProgram(string path)
    {
      using Process fo = new();

      fo.StartInfo.FileName = "explorer";
      fo.StartInfo.Arguments = "\"" + path + "\"";
      fo.Start();
    }

    public static string ItemsCollectionToExcel(ItemCollection it,
        string outDir, string device, string friendlyName)
    {
      if (it.Count == 0) return "";
      string outFile = System.IO.Path.Combine(outDir, $"FS2020Controls_{device}_{friendlyName}.xlsx");

      DataTable dt = new();
      dt.Columns.Add("FriendlyName", typeof(string));
      dt.Columns.Add("Device", typeof(string));
      dt.Columns.Add("ContextName", typeof(string));
      dt.Columns.Add("ActionName", typeof(string));
      dt.Columns.Add("Actor", typeof(string));
      dt.Columns.Add("FriendlyAction", typeof(string));
      dt.Columns.Add("PrimaryKeys", typeof(string));
      dt.Columns.Add("PrimaryKeysCode", typeof(string));
      dt.Columns.Add("SecondaryKeys", typeof(string));
      dt.Columns.Add("SecondaryKeysCode", typeof(string));
      foreach (var fsc in it)
      {
        if (fsc is not FSControl fs) continue;
        dt.Rows.Add(
          fs.FSControlFile.FriendlyName,
          fs.FSControlFile.Device,
          fs.ContextName,
          fs.ActionName,
          fs.Actor,
          fs.FriendlyAction,
          fs.PrimaryKeys,
          fs.PrimaryKeysCode,
          fs.SecondaryKeys,
          fs.SecondaryKeysCode
        );
      }
      using var wb = new XLWorkbook();
      string fName = friendlyName[..Math.Min(10, friendlyName.Length)];
      string dName = device[..Math.Min(30 - fName.Length, device.Length)];
      var ws = wb.AddWorksheet($"{dName}-{fName}");
      ws.FirstCell().InsertTable(dt);
      ws.Columns("A:J").AdjustToContents();
      ws.Columns(11, LastColumn).Hide();
      wb.SaveAs(outFile);
      return outFile;
    }


    public static string ItemsCollectionToPdf(ItemCollection it,
        string outDir, string device, string friendlyName,
        int gray = 240, int fontSize = 12, int padding = 5)
    {
      string[] showColumns =
        { "ContextName", "Actor", "FriendlyAction", "PrimaryKeys", "SecondaryKeys" };

      if (it.Count == 0) return "";
      iLayout.Document document;
      string outFile = System.IO.Path.Combine(outDir, $"FS2020Controls_{device}_{friendlyName}.pdf");
      try
      {
        PdfWriter writer = new(outFile);
        PdfDocument pdf = new(writer);
        string title = $"{device} {friendlyName}";
        pdf.AddEventHandler(PdfDocumentEvent.END_PAGE, new TopEventHandler(title));
        document = new(pdf);
      }
      catch (IOException)
      {
        throw new FS2020Exception(
          $"File {outFile} cannot be written - do you have it open in a viewer?");
      }

      Table table = new(showColumns.Length);
      foreach (string c in showColumns)
      {
        Cell cell = new Cell()
          .Add(new Paragraph(c)
          .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
          .SetTextAlignment(TextAlignment.LEFT))
          .SetFontSize(fontSize)
          // Sequence is important
          .SetBorder(iBorders.Border.NO_BORDER)
          .SetPaddingLeft(padding)
          .SetPaddingRight(padding);
        table.AddHeaderCell(cell);
      }
      Color backColor = ColorConstants.WHITE;
      foreach (object? ct in it)
      {
        if (ct is not FSControl ft) 
          continue;
        foreach (string c in showColumns)
        {
          var x = ft.GetType().GetProperty(c);
          if (x == null) continue;
          if (x.GetValue(ft, null) is not string y) y = "";
          Cell cell = new Cell()
            .Add(new Paragraph(y))
            .SetBorder(iBorders.Border.NO_BORDER)
            .SetBackgroundColor(backColor)
            .SetFontSize(fontSize)
            .SetPaddingLeft(padding)
            .SetPaddingRight(padding)
            .SetTextAlignment(TextAlignment.LEFT);
          table.AddCell(cell);
        }
        backColor = backColor == ColorConstants.WHITE ?
          new DeviceRgb(gray, gray, gray) : ColorConstants.WHITE;
      }
      try
      {
        document.Add(table);
      }
      finally 
      {
        document.Close();
      }
      return outFile;
    }

  }
}
