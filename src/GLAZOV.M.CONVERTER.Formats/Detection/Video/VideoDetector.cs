using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Formats.Detection.Video;

/// <summary>
/// Video containers: MP4, MOV, MKV, WebM, AVI. The signs exclude each other, so the order does not matter.
/// </summary>
internal sealed class VideoDetector() : FamilyDetector(
[
    (FileFormat.Mp4, Mp4Signature.Matches),
    (FileFormat.Mov, MovSignature.Matches),
    (FileFormat.Mkv, MkvSignature.Matches),
    (FileFormat.Webm, WebmSignature.Matches),
    (FileFormat.Avi, AviSignature.Matches),
]);
