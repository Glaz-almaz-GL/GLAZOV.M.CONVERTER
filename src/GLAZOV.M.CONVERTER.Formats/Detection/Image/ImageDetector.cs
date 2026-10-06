using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Formats.Detection.Image;

/// <summary>
/// Images: PNG, JPEG, GIF, TIFF, PSD, DDS, WebP, BMP. TGA has no sign in the start of the file and is not detected by content.
/// </summary>
internal sealed class ImageDetector() : FamilyDetector(
[
    (FileFormat.Png, PngSignature.Matches),
    (FileFormat.Jpeg, JpegSignature.Matches),
    (FileFormat.Gif, GifSignature.Matches),
    (FileFormat.Tiff, TiffSignature.Matches),
    (FileFormat.Psd, PsdSignature.Matches),
    (FileFormat.Dds, DdsSignature.Matches),
    (FileFormat.Webp, WebpSignature.Matches),
    (FileFormat.Bmp, BmpSignature.Matches),
]);
