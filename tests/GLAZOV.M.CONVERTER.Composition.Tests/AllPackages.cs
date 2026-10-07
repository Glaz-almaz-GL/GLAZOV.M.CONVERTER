using GLAZOV.M.CONVERTER.Archives;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Ffmpeg;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Office;
using GLAZOV.M.CONVERTER.Pdf;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Svg;

namespace GLAZOV.M.CONVERTER.Composition.Tests;

/// <summary>
/// The six extension packages and every order they can be plugged in.
/// </summary>
internal static class AllPackages
{
    /// <summary>
    /// The names of the packages, in the order of the plug-in sketch.
    /// </summary>
    internal static readonly string[] Names = ["Formats", "Ffmpeg", "Office", "Svg", "Pdf", "Archives"];

    /// <summary>
    /// A fresh extension of the package.
    /// </summary>
    /// <param name="name">Name from <see cref="Names"/>.</param>
    internal static IConverterExtension Create(string name)
    {
        return name switch
        {
            "Formats" => new FormatsExtension(),
            "Ffmpeg" => new FfmpegExtension(),
            "Office" => new OfficeExtension(),
            "Svg" => new SvgExtension(),
            "Pdf" => new PdfExtension(),
            "Archives" => new ArchivesExtension(),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };
    }

    /// <summary>
    /// Every order of the six packages, as strings like <c>Formats,Ffmpeg,Office,Svg,Pdf,Archives</c>.
    /// </summary>
    internal static IEnumerable<string> Orders()
    {
        return Permute(Names, 0).Select(order => string.Join(',', order)).Distinct();
    }

    /// <summary>
    /// A converter with the packages of the order plugged in one after another.
    /// </summary>
    /// <param name="order">Order from <see cref="Orders"/>.</param>
    /// <param name="settings">Settings to use; default settings when <c>null</c>.</param>
    internal static Converter Converter(string order, ConverterSettings? settings = null)
    {
        settings ??= new ConverterSettings();
        foreach (string name in order.Split(','))
        {
            settings.Extensions.Add(Create(name));
        }

        return new Converter(settings);
    }

    private static IEnumerable<string[]> Permute(string[] items, int start)
    {
        if (start == items.Length - 1)
        {
            yield return [.. items];
            yield break;
        }

        for (int index = start; index < items.Length; index++)
        {
            string[] copy = [.. items];
            (copy[start], copy[index]) = (copy[index], copy[start]);
            foreach (string[] order in Permute(copy, start + 1))
            {
                yield return order;
            }
        }
    }
}
