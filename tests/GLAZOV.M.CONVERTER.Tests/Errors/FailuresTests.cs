using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Tests.Extension;

namespace GLAZOV.M.CONVERTER.Tests.Errors;

/// <summary>
/// Ошибки: у каждой из шести причин — верные этап, причина, текст; обрезка вывода программы.
/// </summary>
public class FailuresTests
{
    [Fact]
    public void TypeNotConfirmed_NamedByExtension_SaysWhatTheBytesShow()
    {
        var ex = Failures.TypeNotConfirmed("a.alpha", "ALPHA", TestFormats.Beta);

        Assert.Equal(ConversionStage.Detection, ex.Stage);
        Assert.Equal(ConversionFailureReason.TypeNotConfirmed, ex.Reason);
        Assert.Equal("Файл «a.alpha» не является ALPHA: по начальным байтам это BETA.", ex.Message);
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
        var ex = Failures.ToolNotFound("утилита", "Установите: winget install Утилита");

        Assert.Equal(ConversionStage.Preparation, ex.Stage);
        Assert.Equal(ConversionFailureReason.ToolNotFound, ex.Reason);
        Assert.Equal("утилита", ex.ToolName);
        Assert.Equal("Программа «утилита» не найдена. Установите: winget install Утилита", ex.Message);
    }

    [Theory]
    [InlineData("MP4", "MP3", "Действия не поддерживаются для MP4 → MP3.")]
    [InlineData("WAV", "FLAC", "Действия не поддерживаются для WAV → FLAC.")]
    [InlineData("PNG", "JPEG", "Действия не поддерживаются для PNG → JPEG.")]
    [InlineData("MKV", "MP4", "Действия не поддерживаются для MKV → MP4.")]
    public void UnsupportedOperations_SaysTheSameForEveryPair(string from, string to, string expected)
    {
        FileFormat[] all = [FileFormat.Mp3, FileFormat.Wav, FileFormat.Flac, FileFormat.Mp4, FileFormat.Mkv, FileFormat.Png, FileFormat.Jpeg];

        ConversionException ex = Failures.UnsupportedOperations(all.Single(f => f.Name == from), all.Single(f => f.Name == to));

        Assert.Equal(expected, ex.Message);
        Assert.Equal(ConversionFailureReason.UnsupportedConversion, ex.Reason);
        Assert.Equal(ConversionStage.Preparation, ex.Stage);
        Assert.DoesNotContain("изображени", ex.Message);
    }

    [Fact]
    public void UnsupportedOperations_ForAFormatNotInTheSoundSet_NamesNeitherSoundNorImage()
    {
        ConversionException ex = Failures.UnsupportedOperations(TestFormats.Alpha, TestFormats.Beta);

        Assert.Equal("Действия не поддерживаются для ALPHA → BETA.", ex.Message);
        Assert.Equal(ConversionFailureReason.UnsupportedConversion, ex.Reason);
        Assert.Equal(ConversionStage.Preparation, ex.Stage);
    }

    [Fact]
    public void UnsupportedConversion_NamesBothSides()
    {
        var ex = Failures.UnsupportedConversion(TestFormats.Gamma, TestFormats.Delta);

        Assert.Equal(ConversionStage.Preparation, ex.Stage);
        Assert.Equal(ConversionFailureReason.UnsupportedConversion, ex.Reason);
        Assert.Equal("Конвертация GAMMA → DELTA не поддерживается.", ex.Message);
    }

    [Fact]
    public void ToolFailed_KeepsExitCodeLastLinesAndShowsTheLastOneInMessage()
    {
        var ex = Failures.ToolFailed("утилита", 1, "версия...\nпредупреждение\nInvalid data found when processing input\n");

        Assert.Equal(ConversionStage.Conversion, ex.Stage);
        Assert.Equal(ConversionFailureReason.ToolFailed, ex.Reason);
        Assert.Equal("утилита", ex.ToolName);
        Assert.Equal(1, ex.ToolExitCode);
        Assert.Contains("Invalid data found", ex.Message);
        Assert.Contains("(код 1)", ex.Message);
        Assert.Equal("версия...\nпредупреждение\nInvalid data found when processing input", ex.ToolOutput);
    }

    [Fact]
    public void InvalidParameters_IsTheConversionStage_WithTheGivenMessage_AndRefusesAnEmptyOne()
    {
        ConversionException error = ConversionException.InvalidParameters("Сторона больше 256 пикселей.");

        Assert.Equal(ConversionFailureReason.InvalidParameters, error.Reason);
        Assert.Equal(ConversionStage.Conversion, error.Stage);
        Assert.Equal("Сторона больше 256 пикселей.", error.Message);
        Assert.Null(error.ToolName);
        Assert.Throws<ArgumentException>(() => ConversionException.InvalidParameters(" "));
    }

    [Fact]
    public void ToolFailed_WithoutOutput_StillSaysExitCode()
    {
        var ex = Failures.ToolFailed("утилита", 137, null);

        Assert.Equal("Программа «утилита» завершилась с ошибкой (код 137).", ex.Message);
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
        var ex = Failures.FileExists(Path.Combine("C:", "Users", "Секрет", "музыка", "песня.alpha"));

        Assert.Equal(ConversionStage.Preparation, ex.Stage);
        Assert.Equal(ConversionFailureReason.FileExists, ex.Reason);
        Assert.Contains("«песня.alpha»", ex.Message);
        Assert.DoesNotContain("Секрет", ex.Message);
    }

    [Fact]
    public void InputUnreadable_FromException_KeepsInnerAndName()
    {
        var inner = new IOException("занято");

        var ex = Failures.InputUnreadable("a.beta", inner);

        Assert.Equal(ConversionStage.Preparation, ex.Stage);
        Assert.Equal(ConversionFailureReason.InputUnreadable, ex.Reason);
        Assert.Same(inner, ex.InnerException);
        Assert.Equal("Не удалось прочитать вход «a.beta»: файл занят другой программой или недоступен.", ex.Message);
    }

    [Fact]
    public void InputUnreadable_NeverShowsTheFullPathOfTheSystemMessage()
    {
        string secret = Path.Combine("C:", "Users", "Секрет", "музыка", "a.alpha");

        foreach (Exception inner in new Exception[]
        {
            new FileNotFoundException($"Could not find file '{secret}'.", secret),
            new DirectoryNotFoundException($"Could not find a part of the path '{secret}'."),
            new UnauthorizedAccessException($"Access to the path '{secret}' is denied."),
            new IOException($"The process cannot access the file '{secret}' because it is being used."),
        })
        {
            ConversionException ex = Failures.InputUnreadable("a.alpha", inner);

            Assert.DoesNotContain("Секрет", ex.Message);
            Assert.DoesNotContain("Users", ex.Message);
            Assert.Same(inner, ex.InnerException);
        }
    }

    [Fact]
    public void InputUnreadable_FromDetail_SaysWhy()
    {
        var ex = Failures.InputUnreadable("поток", "поток не поддерживает чтение");

        Assert.Equal("Вход «поток» не читается: поток не поддерживает чтение", ex.Message);
    }
}
