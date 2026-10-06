using System.Text;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;

namespace GLAZOV.M.CONVERTER.Tests.Detection;

/// <summary>
/// Plain text must never be taken for a QuickTime file just because bytes 4..8 spell an atom type.
/// </summary>
public class VideoSignatureTextTests
{
    [Theory]
    [InlineData("The wide world of sports, a short note.")]
    [InlineData("The free text of a short letter.")]
    [InlineData("Our free and open notes, nothing else.")]
    [InlineData("Old moov file notes for the team.")]
    [InlineData("Raw mdat dump description, no video.")]
    public void Match_TextWhoseBytesFourToEightSpellAnAtomType_IsUnknown(string text)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);

        Assert.Same(FileFormat.Unknown, Sniff.Match(bytes));
    }
}
