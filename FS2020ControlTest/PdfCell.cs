//Imports
using iText.Layout.Element;

namespace FS2020Control
{
  internal class PdfCell
  {
    private Paragraph paragraph;

    public PdfCell(Paragraph paragraph)
    {
      this.paragraph = paragraph;
    }
  }
}