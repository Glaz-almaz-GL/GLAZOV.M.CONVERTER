using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Formats;

/// <summary>
/// The document formats of the standard set, and plain text.
/// </summary>
public static class DocumentFormats
{
    /// <summary>
    /// PDF: the result of documents and pictures.
    /// </summary>
    public static FileFormat Pdf { get; } = FileFormat.Create("PDF", "application/pdf", "pdf");

    /// <summary>
    /// Word document, Office Open XML.
    /// </summary>
    public static FileFormat Docx { get; } = FileFormat.Create("DOCX", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "docx");

    /// <summary>
    /// Word document, Office 97-2003.
    /// </summary>
    public static FileFormat Doc { get; } = FileFormat.Create("DOC", "application/msword", "doc");

    /// <summary>
    /// OpenDocument text.
    /// </summary>
    public static FileFormat Odt { get; } = FileFormat.Create("ODT", "application/vnd.oasis.opendocument.text", "odt");

    /// <summary>
    /// Rich Text Format.
    /// </summary>
    public static FileFormat Rtf { get; } = FileFormat.Create("RTF", "application/rtf", "rtf");

    /// <summary>
    /// Excel workbook, Office Open XML.
    /// </summary>
    public static FileFormat Xlsx { get; } = FileFormat.Create("XLSX", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx");

    /// <summary>
    /// Excel workbook, Office 97-2003.
    /// </summary>
    public static FileFormat Xls { get; } = FileFormat.Create("XLS", "application/vnd.ms-excel", "xls");

    /// <summary>
    /// OpenDocument spreadsheet.
    /// </summary>
    public static FileFormat Ods { get; } = FileFormat.Create("ODS", "application/vnd.oasis.opendocument.spreadsheet", "ods");

    /// <summary>
    /// PowerPoint presentation, Office Open XML.
    /// </summary>
    public static FileFormat Pptx { get; } = FileFormat.Create("PPTX", "application/vnd.openxmlformats-officedocument.presentationml.presentation", "pptx");

    /// <summary>
    /// PowerPoint presentation, Office 97-2003.
    /// </summary>
    public static FileFormat Ppt { get; } = FileFormat.Create("PPT", "application/vnd.ms-powerpoint", "ppt");

    /// <summary>
    /// OpenDocument presentation.
    /// </summary>
    public static FileFormat Odp { get; } = FileFormat.Create("ODP", "application/vnd.oasis.opendocument.presentation", "odp");

    /// <summary>
    /// HTML page.
    /// </summary>
    public static FileFormat Html { get; } = FileFormat.Create("HTML", "text/html", "html");

    /// <summary>
    /// Plain text. Confirmed by the content (valid text without control bytes); a name ending in .md only hints that it is laid out as Markdown and never proves the type.
    /// </summary>
    public static FileFormat Text { get; } = FileFormat.Create("Text", "text/plain", "txt");
}
