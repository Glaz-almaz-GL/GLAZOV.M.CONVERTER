using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.Detection.Documents;

/// <summary>
/// Documents: PDF, RTF, packages of Office and OpenDocument, old Office files, HTML. When the start of the input does not settle an archive or a compound file,
/// the second look reads the structure where it lies (<see cref="ContainerProbe"/>).
/// </summary>
internal sealed class DocumentDetector() : FamilyDetector(
[
    (FileFormat.Pdf, PdfSignature.Matches),
    (FileFormat.Rtf, RtfSignature.Matches),
    (FileFormat.Docx, DocxSignature.Matches),
    (FileFormat.Xlsx, XlsxSignature.Matches),
    (FileFormat.Pptx, PptxSignature.Matches),
    (FileFormat.Odt, OdtSignature.Matches),
    (FileFormat.Ods, OdsSignature.Matches),
    (FileFormat.Odp, OdpSignature.Matches),
    (FileFormat.Doc, DocSignature.Matches),
    (FileFormat.Xls, XlsSignature.Matches),
    (FileFormat.Ppt, PptSignature.Matches),
    (FileFormat.Html, HtmlSignature.Matches),
]), IStreamFormatDetector
{
    /// <inheritdoc />
    public bool CanInspect(ReadOnlySpan<byte> head)
    {
        return ContainerProbe.IsContainer(head);
    }

    /// <inheritdoc />
    public FileFormat? Detect(Stream input)
    {
        return ContainerProbe.Match(input);
    }
}
