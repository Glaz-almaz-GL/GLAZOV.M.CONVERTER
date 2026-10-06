using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Detection.Audio;

/// <summary>
/// Audio: OGG, FLAC, WAV, M4A and, as the weakest sign, MP3.
/// </summary>
internal sealed class AudioDetector() : FamilyDetector(
[
    (FileFormat.Ogg, OggSignature.Matches),
    (FileFormat.Flac, FlacSignature.Matches),
    (FileFormat.Wav, WavSignature.Matches),
    (FileFormat.M4a, M4aSignature.Matches),
    (FileFormat.Mp3, Mp3Signature.Matches),
]);
