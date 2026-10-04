using GLAZOV.M.CONVERTER.Converting;
using SkiaSharp;

namespace GLAZOV.M.CONVERTER.Svg.Rendering;

/// <summary>
/// Размер результата и преобразование рисунка SVG в него: естественный размер документа,
/// затем действия по порядку. Масштаб и поворот — не пересчёт пикселей, а перерисовка вектора.
/// </summary>
/// <param name="Width">Ширина результата в пикселях.</param>
/// <param name="Height">Высота результата в пикселях.</param>
/// <param name="Matrix">Преобразование координат рисунка в координаты результата.</param>
internal sealed record RasterPlan(int Width, int Height, SKMatrix Matrix)
{
    /// <summary>
    /// Строит план: размер документа округляется вверх до целого пикселя, масштаб округляет вниз
    /// и не даёт меньше одного пикселя — так же, как движок изображений основной библиотеки.
    /// </summary>
    /// <param name="bounds">Границы рисунка SVG (его естественный размер).</param>
    /// <param name="operations">Действия по порядку.</param>
    /// <param name="maxPixels">Наибольшее число пикселей результата.</param>
    /// <exception cref="InvalidDataException">
    /// У документа нет размера, результат больше предела или одна сторона больше предела на каком-либо шаге.
    /// </exception>
    internal static RasterPlan Build(SKRect bounds, IEnumerable<Operation> operations, long maxPixels)
    {
        ArgumentNullException.ThrowIfNull(operations);

        if (!(bounds.Width > 0 && bounds.Height > 0) || float.IsInfinity(bounds.Width) || float.IsInfinity(bounds.Height))
        {
            throw new InvalidDataException("У документа SVG нет размера: ширина и высота должны быть больше нуля.");
        }

        long width = ToPixels(Math.Ceiling((double)bounds.Width), maxPixels);
        long height = ToPixels(Math.Ceiling((double)bounds.Height), maxPixels);
        SKMatrix matrix = SKMatrix.CreateTranslation(-bounds.Left, -bounds.Top);

        foreach (Operation operation in operations)
        {
            switch (operation.Kind)
            {
                case OperationKind.RotateRight:
                    // По часовой стрелке: точка (x, y) переходит в (h − y, x), ширина и высота меняются местами.
                    SKMatrix rotate = new(0, -1, height, 1, 0, 0, 0, 0, 1);
                    matrix = SKMatrix.Concat(rotate, matrix);
                    (width, height) = (height, width);
                    break;

                case OperationKind.ScalePercent when operation.Percent is int percent:
                    long scaledWidth = ToPixels(Math.Floor((double)width * percent / 100), maxPixels);
                    long scaledHeight = ToPixels(Math.Floor((double)height * percent / 100), maxPixels);
                    SKMatrix scale = SKMatrix.CreateScale((float)scaledWidth / width, (float)scaledHeight / height);
                    matrix = SKMatrix.Concat(scale, matrix);
                    (width, height) = (scaledWidth, scaledHeight);
                    break;

                default:
                    throw new NotSupportedException($"Действие «{operation.Kind}» движок SVG не умеет.");
            }
        }

        if (width * height > maxPixels)
        {
            throw new InvalidDataException(
                $"Результат {width}×{height} пикселей больше предела {maxPixels} (настройка SvgSettings.MaxPixels).");
        }

        return new RasterPlan((int)width, (int)height, matrix);
    }

    // Сторона больше предела пикселей не поместится ни при какой второй стороне: отказ сразу, на любом шаге, до переполнения.
    private static long ToPixels(double size, long maxPixels)
    {
        if (size > Math.Min(maxPixels, int.MaxValue))
        {
            throw new InvalidDataException(
                $"Сторона результата {size:0} пикселей больше предела {maxPixels} (настройка SvgSettings.MaxPixels).");
        }

        return Math.Max(1, (long)size);
    }
}
