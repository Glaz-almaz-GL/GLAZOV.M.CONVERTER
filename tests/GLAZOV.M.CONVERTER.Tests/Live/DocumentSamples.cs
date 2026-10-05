using System.Text;

namespace GLAZOV.M.CONVERTER.Tests.Live;

/// <summary>
/// Flat ODF sources (.fodt, .fods, .fodp) for the document run: LibreOffice turns them into the real files of every format.
/// A text with a heading, a table, a picture, a header and a footer on four pages; a workbook with two sheets and formulas;
/// a presentation of three slides; all in Russian.
/// </summary>
internal static class DocumentSamples
{
    // A 1x1 PNG, stretched by the frame.
    private const string PngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private const string Namespaces =
        """xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0" xmlns:draw="urn:oasis:names:tc:opendocument:xmlns:drawing:1.0" xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0" xmlns:xlink="http://www.w3.org/1999/xlink" xmlns:svg="urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0" xmlns:presentation="urn:oasis:names:tc:opendocument:xmlns:presentation:1.0" xmlns:chart="urn:oasis:names:tc:opendocument:xmlns:chart:1.0" office:version="1.2" """;

    private const string PageLayout =
        """<style:page-layout style:name="pm1"><style:page-layout-properties fo:page-width="21cm" fo:page-height="29.7cm" fo:margin-top="1.5cm" fo:margin-bottom="1.5cm" fo:margin-left="2cm" fo:margin-right="2cm"/><style:header-style/><style:footer-style/></style:page-layout>""";

    /// <summary>
    /// Four pages: heading, paragraphs, a table, a picture, a header and a footer with the page number.
    /// </summary>
    internal static string Text(int extraPages = 0)
    {
        StringBuilder body = new();
        body.Append("""<text:h text:style-name="Head" text:outline-level="1">Отчёт о проверке системы</text:h>""");
        body.Append("""<text:p>Первая страница: съешь ещё этих мягких французских булок, да выпей чаю. Цена 1 234,50 руб.</text:p>""");
        body.Append("""<table:table table:name="Таблица1" table:style-name="Tbl"><table:table-column table:number-columns-repeated="3"/>""");
        body.Append("""<table:table-row><table:table-cell table:style-name="Cell" office:value-type="string"><text:p>Узел</text:p></table:table-cell><table:table-cell table:style-name="Cell" office:value-type="string"><text:p>Состояние</text:p></table:table-cell><table:table-cell table:style-name="Cell" office:value-type="string"><text:p>Срок</text:p></table:table-cell></table:table-row>""");
        body.Append("""<table:table-row><table:table-cell table:style-name="Cell" office:value-type="string"><text:p>Контракт</text:p></table:table-cell><table:table-cell table:style-name="Cell" office:value-type="string"><text:p>Принят</text:p></table:table-cell><table:table-cell table:style-name="Cell" office:value-type="float" office:value="12"><text:p>12</text:p></table:table-cell></table:table-row></table:table>""");
        body.Append($"""<text:p>Картинка:<draw:frame draw:name="Рисунок1" text:anchor-type="as-char" svg:width="3cm" svg:height="3cm"><draw:image><office:binary-data>{PngBase64}</office:binary-data></draw:image></draw:frame></text:p>""");
        for (int page = 2; page <= 4 + extraPages; page++)
        {
            body.Append($"""<text:p text:style-name="Brk">Страница номер {page}: текст для проверки переносов и кириллицы.</text:p>""");
        }

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document {Namespaces} office:mimetype="application/vnd.oasis.opendocument.text">
            <office:font-face-decls><style:font-face style:name="Liberation Sans" svg:font-family="&apos;Liberation Sans&apos;"/></office:font-face-decls>
            <office:automatic-styles>
            <style:style style:name="Brk" style:family="paragraph"><style:paragraph-properties fo:break-before="page"/></style:style>
            <style:style style:name="Head" style:family="paragraph"><style:text-properties style:font-name="Liberation Sans" fo:font-size="18pt" fo:font-weight="bold" fo:color="#800000"/></style:style>
            <style:style style:name="Tbl" style:family="table"><style:table-properties style:width="14cm" table:align="left"/></style:style>
            <style:style style:name="Cell" style:family="table-cell"><style:table-cell-properties fo:padding="0.15cm" fo:border="0.75pt solid #000000"/></style:style>
            {PageLayout}
            </office:automatic-styles>
            <office:master-styles><style:master-page style:name="Standard" style:page-layout-name="pm1"><style:header><text:p>Верхний колонтитул отчёта</text:p></style:header><style:footer><text:p>Страница <text:page-number text:select-page="current">1</text:page-number></text:p></style:footer></style:master-page></office:master-styles>
            <office:body><office:text>{body}</office:text></office:body>
            </office:document>
            """;
    }

    /// <summary>
    /// Two sheets: sales with a sum, and totals that read the first sheet.
    /// </summary>
    internal static string Sheet()
    {
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document {Namespaces} office:mimetype="application/vnd.oasis.opendocument.spreadsheet">
            <office:automatic-styles><style:style style:name="Ce" style:family="table-cell"><style:table-cell-properties fo:border="0.75pt solid #000000"/></style:style></office:automatic-styles>
            <office:body><office:spreadsheet>
            <table:table table:name="Продажи"><table:table-column table:number-columns-repeated="3"/>
            <table:table-row><table:table-cell table:style-name="Ce" office:value-type="string"><text:p>Товар</text:p></table:table-cell><table:table-cell table:style-name="Ce" office:value-type="string"><text:p>Количество</text:p></table:table-cell><table:table-cell table:style-name="Ce" office:value-type="string"><text:p>Цена</text:p></table:table-cell></table:table-row>
            <table:table-row><table:table-cell table:style-name="Ce" office:value-type="string"><text:p>Яблоки</text:p></table:table-cell><table:table-cell table:style-name="Ce" office:value-type="float" office:value="10"><text:p>10</text:p></table:table-cell><table:table-cell table:style-name="Ce" office:value-type="float" office:value="25.5"><text:p>25,5</text:p></table:table-cell></table:table-row>
            <table:table-row><table:table-cell table:style-name="Ce" office:value-type="string"><text:p>Груши</text:p></table:table-cell><table:table-cell table:style-name="Ce" office:value-type="float" office:value="20"><text:p>20</text:p></table:table-cell><table:table-cell table:style-name="Ce" office:value-type="float" office:value="30"><text:p>30</text:p></table:table-cell></table:table-row>
            <table:table-row><table:table-cell table:style-name="Ce" office:value-type="string"><text:p>Итого штук</text:p></table:table-cell><table:table-cell table:style-name="Ce" table:formula="=SUM(B2:B3)" office:value-type="float" office:value="30"><text:p>30</text:p></table:table-cell><table:table-cell table:style-name="Ce" table:formula="=SUMPRODUCT(B2:B3;C2:C3)" office:value-type="float" office:value="855"><text:p>855</text:p></table:table-cell></table:table-row>
            </table:table>
            <table:table table:name="Итоги"><table:table-column table:number-columns-repeated="2"/>
            <table:table-row><table:table-cell table:style-name="Ce" office:value-type="string"><text:p>Сумма продаж</text:p></table:table-cell><table:table-cell table:style-name="Ce" table:formula="=$Продажи.C4" office:value-type="float" office:value="855"><text:p>855</text:p></table:table-cell></table:table-row>
            </table:table>
            </office:spreadsheet></office:body>
            </office:document>
            """;
    }

    /// <summary>
    /// Three slides.
    /// </summary>
    internal static string Slides()
    {
        StringBuilder pages = new();
        string[] titles = ["Слайд первый: вступление", "Слайд второй: результаты", "Слайд третий: выводы"];
        foreach (string title in titles)
        {
            pages.Append($"""<draw:page draw:name="{title}" draw:style-name="Bg" draw:master-page-name="Default"><draw:frame svg:width="24cm" svg:height="3cm" svg:x="2cm" svg:y="2cm"><draw:text-box><text:p text:style-name="Title">{title}</text:p></draw:text-box></draw:frame><draw:frame svg:width="24cm" svg:height="8cm" svg:x="2cm" svg:y="6cm"><draw:text-box><text:p text:style-name="Body">Пункт один, пункт два и ещё один пункт.</text:p></draw:text-box></draw:frame></draw:page>""");
        }

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document {Namespaces} office:mimetype="application/vnd.oasis.opendocument.presentation">
            <office:automatic-styles><style:style style:name="Bg" style:family="drawing-page"><style:drawing-page-properties draw:fill="solid" draw:fill-color="#ffffcc"/></style:style><style:style style:name="Title" style:family="paragraph"><style:text-properties fo:font-size="32pt" fo:font-weight="bold" fo:color="#003399"/></style:style><style:style style:name="Body" style:family="paragraph"><style:text-properties fo:font-size="20pt" fo:color="#cc3300"/></style:style><style:page-layout style:name="PM1"><style:page-layout-properties fo:page-width="28cm" fo:page-height="15.75cm" style:print-orientation="landscape"/></style:page-layout></office:automatic-styles>
            <office:master-styles><style:master-page style:name="Default" style:page-layout-name="PM1"/></office:master-styles>
            <office:body><office:presentation>{pages}</office:presentation></office:body>
            </office:document>
            """;
    }

    internal const string Html = """<!DOCTYPE html><html><head><meta charset="utf-8"><title>Страница</title><style>body{font-family:serif} h1{color:#a00} td{border:1px solid #444;padding:4px} .big{font-size:20pt}</style></head><body><h1>Заголовок страницы</h1><p class="big">Абзац со стилем и <b>жирным</b> текстом.</p><table border="1" cellspacing="0" cellpadding="4"><tr><td>Левая ячейка</td><td>Правая ячейка</td></tr><tr><td>1</td><td>2</td></tr></table><ul><li>первый</li><li>второй</li></ul></body></html>""";

    internal const string Markdown = "# Заголовок записки\n\nПервый абзац с **жирным** словом.\n\n- пункт один\n- пункт два\n\n| имя | значение |\n|---|---|\n| альфа | 1 |\n| бета | 2 |\n";
}
