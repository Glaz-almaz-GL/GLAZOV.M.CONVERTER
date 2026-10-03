using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Errors;

namespace GLAZOV.M.CONVERTER.Tests.Errors;

/// <summary>
/// Ошибки: у каждой из шести причин — верные этап, причина, текст; обрезка вывода программы.
/// </summary>
public class FailuresTests
{
    [Fact]
    public void TypeNotConfirmed_NamedByExtension_SaysWhatTheBytesShow()
    {
        var ex = Failures.TypeNotConfirmed("a.mp3", "MP3", FileFormat.Wav);

        Assert.Equal(ConversionStage.Detection, ex.Stage);
        Assert.Equal(ConversionFailureReason.TypeNotConfirmed, ex.Reason);
        Assert.Equal("Файл «a.mp3» не является MP3: по начальным байтам это WAV.", ex.Message);
    }

    [Fact]
    public void TypeNotConfirmed_Unknown_SaysNotRecognized()
    {
        var ex = Failures.TypeNotConfirmed("мусор.bin", null, FileFormat.Unknown);

        Assert.Equal("Тип входа «мусор.bin» не подтвердился: по начальным байтам тип не распознан.", ex.Message);
    }

    [Fact]
    public void ToolNotFound_CarriesToolNameAndInstallHint()
    {
        var ex = Failures.ToolNotFound("ffmpeg", "Установите: winget install Gyan.FFmpeg");

        Assert.Equal(ConversionStage.Preparation, ex.Stage);
        Assert.Equal(ConversionFailureReason.ToolNotFound, ex.Reason);
        Assert.Equal("ffmpeg", ex.ToolName);
        Assert.Equal("Программа «ffmpeg» не найдена. Установите: winget install Gyan.FFmpeg", ex.Message);
    }

    [Fact]
    public void UnsupportedConversion_NamesBothSides()
    {
        var ex = Failures.UnsupportedConversion(FileFormat.Ogg, FileFormat.Jpeg);

        Assert.Equal(ConversionStage.Preparation, ex.Stage);
        Assert.Equal(ConversionFailureReason.UnsupportedConversion, ex.Reason);
        Assert.Equal("Конвертация OGG → JPEG не поддерживается.", ex.Message);
    }

    [Fact]
    public void ToolFailed_KeepsExitCodeLastLinesAndShowsTheLastOneInMessage()
    {
        var ex = Failures.ToolFailed("ffmpeg", 1, "версия...\nпредупреждение\nInvalid data found when processing input\n");

        Assert.Equal(ConversionStage.Conversion, ex.Stage);
        Assert.Equal(ConversionFailureReason.ToolFailed, ex.Reason);
        Assert.Equal("ffmpeg", ex.ToolName);
        Assert.Equal(1, ex.ToolExitCode);
        Assert.Contains("Invalid data found", ex.Message);
        Assert.Contains("(код 1)", ex.Message);
        Assert.Equal("версия...\nпредупреждение\nInvalid data found when processing input", ex.ToolOutput);
    }

    [Fact]
    public void ToolFailed_WithoutOutput_StillSaysExitCode()
    {
        var ex = Failures.ToolFailed("ffmpeg", 137, null);

        Assert.Equal("Программа «ffmpeg» завершилась с ошибкой (код 137).", ex.Message);
        Assert.Equal(string.Empty, ex.ToolOutput);
    }

    [Fact]
    public void TrimOutput_KeepsLastTwentyLines()
    {
        var output = string.Join('\n', Enumerable.Range(1, 100).Select(i => $"строка {i}"));

        var tail = Failures.TrimOutput(output).Split('\n');

        Assert.Equal(Failures.ToolOutputLines, tail.Length);
        Assert.Equal("строка 81", tail[0]);
        Assert.Equal("строка 100", tail[^1]);
    }

    [Fact]
    public void TrimOutput_KeepsAtMostTwoThousandChars_FromTheEnd()
    {
        var output = new string('x', 5000) + "КОНЕЦ";

        var tail = Failures.TrimOutput(output);

        Assert.Equal(Failures.ToolOutputChars, tail.Length);
        Assert.EndsWith("КОНЕЦ", tail);
    }

    [Fact]
    public void FileExists_ShowsOnlyFileName_NotTheFullPath()
    {
        var ex = Failures.FileExists(Path.Combine("C:", "Users", "Секрет", "музыка", "песня.mp3"));

        Assert.Equal(ConversionStage.Preparation, ex.Stage);
        Assert.Equal(ConversionFailureReason.FileExists, ex.Reason);
        Assert.Contains("«песня.mp3»", ex.Message);
        Assert.DoesNotContain("Секрет", ex.Message);
    }

    [Fact]
    public void InputUnreadable_FromException_KeepsInnerAndName()
    {
        var inner = new IOException("занято");

        var ex = Failures.InputUnreadable("a.wav", inner);

        Assert.Equal(ConversionStage.Preparation, ex.Stage);
        Assert.Equal(ConversionFailureReason.InputUnreadable, ex.Reason);
        Assert.Same(inner, ex.InnerException);
        Assert.Equal("Не удалось прочитать вход «a.wav»: занято", ex.Message);
    }

    [Fact]
    public void InputUnreadable_FromDetail_SaysWhy()
    {
        var ex = Failures.InputUnreadable("поток", "поток не поддерживает чтение");

        Assert.Equal("Вход «поток» не читается: поток не поддерживает чтение", ex.Message);
    }
}
