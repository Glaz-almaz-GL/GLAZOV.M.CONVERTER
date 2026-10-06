using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Formats;

/// <summary>
/// The written form <c>FileFormat.Mp3</c> for the values of this package: after <c>using GLAZOV.M.CONVERTER.Formats;</c>
/// every value is read the way a member of <see cref="FileFormat"/> is. Each member returns the field of the matching class.
/// </summary>
public static class FileFormatMembers
{
    extension(FileFormat)
    {
        /// <summary>Same as <see cref="AudioFormats.Mp3"/>.</summary>
        public static FileFormat Mp3 => AudioFormats.Mp3;
        /// <summary>Same as <see cref="AudioFormats.Wav"/>.</summary>
        public static FileFormat Wav => AudioFormats.Wav;
        /// <summary>Same as <see cref="AudioFormats.Ogg"/>.</summary>
        public static FileFormat Ogg => AudioFormats.Ogg;
        /// <summary>Same as <see cref="AudioFormats.Flac"/>.</summary>
        public static FileFormat Flac => AudioFormats.Flac;
        /// <summary>Same as <see cref="AudioFormats.M4a"/>.</summary>
        public static FileFormat M4a => AudioFormats.M4a;
    }

    extension(FileFormat)
    {
        /// <summary>Same as <see cref="ImageFormats.Jpeg"/>.</summary>
        public static FileFormat Jpeg => ImageFormats.Jpeg;
        /// <summary>Same as <see cref="ImageFormats.Png"/>.</summary>
        public static FileFormat Png => ImageFormats.Png;
        /// <summary>Same as <see cref="ImageFormats.Webp"/>.</summary>
        public static FileFormat Webp => ImageFormats.Webp;
        /// <summary>Same as <see cref="ImageFormats.Bmp"/>.</summary>
        public static FileFormat Bmp => ImageFormats.Bmp;
        /// <summary>Same as <see cref="ImageFormats.Gif"/>.</summary>
        public static FileFormat Gif => ImageFormats.Gif;
        /// <summary>Same as <see cref="ImageFormats.Tiff"/>.</summary>
        public static FileFormat Tiff => ImageFormats.Tiff;
        /// <summary>Same as <see cref="ImageFormats.Tga"/>.</summary>
        public static FileFormat Tga => ImageFormats.Tga;
        /// <summary>Same as <see cref="ImageFormats.Dds"/>.</summary>
        public static FileFormat Dds => ImageFormats.Dds;
        /// <summary>Same as <see cref="ImageFormats.Psd"/>.</summary>
        public static FileFormat Psd => ImageFormats.Psd;
        /// <summary>Same as <see cref="ImageFormats.Svg"/>.</summary>
        public static FileFormat Svg => ImageFormats.Svg;
    }

    extension(FileFormat)
    {
        /// <summary>Same as <see cref="VideoFormats.Mp4"/>.</summary>
        public static FileFormat Mp4 => VideoFormats.Mp4;
        /// <summary>Same as <see cref="VideoFormats.Mkv"/>.</summary>
        public static FileFormat Mkv => VideoFormats.Mkv;
        /// <summary>Same as <see cref="VideoFormats.Webm"/>.</summary>
        public static FileFormat Webm => VideoFormats.Webm;
        /// <summary>Same as <see cref="VideoFormats.Mov"/>.</summary>
        public static FileFormat Mov => VideoFormats.Mov;
        /// <summary>Same as <see cref="VideoFormats.Avi"/>.</summary>
        public static FileFormat Avi => VideoFormats.Avi;
    }

    extension(FileFormat)
    {
        /// <summary>Same as <see cref="DocumentFormats.Pdf"/>.</summary>
        public static FileFormat Pdf => DocumentFormats.Pdf;
        /// <summary>Same as <see cref="DocumentFormats.Docx"/>.</summary>
        public static FileFormat Docx => DocumentFormats.Docx;
        /// <summary>Same as <see cref="DocumentFormats.Doc"/>.</summary>
        public static FileFormat Doc => DocumentFormats.Doc;
        /// <summary>Same as <see cref="DocumentFormats.Odt"/>.</summary>
        public static FileFormat Odt => DocumentFormats.Odt;
        /// <summary>Same as <see cref="DocumentFormats.Rtf"/>.</summary>
        public static FileFormat Rtf => DocumentFormats.Rtf;
        /// <summary>Same as <see cref="DocumentFormats.Xlsx"/>.</summary>
        public static FileFormat Xlsx => DocumentFormats.Xlsx;
        /// <summary>Same as <see cref="DocumentFormats.Xls"/>.</summary>
        public static FileFormat Xls => DocumentFormats.Xls;
        /// <summary>Same as <see cref="DocumentFormats.Ods"/>.</summary>
        public static FileFormat Ods => DocumentFormats.Ods;
        /// <summary>Same as <see cref="DocumentFormats.Pptx"/>.</summary>
        public static FileFormat Pptx => DocumentFormats.Pptx;
        /// <summary>Same as <see cref="DocumentFormats.Ppt"/>.</summary>
        public static FileFormat Ppt => DocumentFormats.Ppt;
        /// <summary>Same as <see cref="DocumentFormats.Odp"/>.</summary>
        public static FileFormat Odp => DocumentFormats.Odp;
        /// <summary>Same as <see cref="DocumentFormats.Html"/>.</summary>
        public static FileFormat Html => DocumentFormats.Html;
        /// <summary>Same as <see cref="DocumentFormats.Text"/>.</summary>
        public static FileFormat Text => DocumentFormats.Text;
    }
}
