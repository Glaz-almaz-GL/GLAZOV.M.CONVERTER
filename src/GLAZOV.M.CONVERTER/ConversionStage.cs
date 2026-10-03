namespace GLAZOV.M.CONVERTER;

/// <summary>Этап, на котором произошла ошибка.</summary>
public enum ConversionStage
{
    /// <summary>Определение типа входа.</summary>
    Detection,

    /// <summary>Подготовка: проверка входа и выхода, поиск программы.</summary>
    Preparation,

    /// <summary>Сама конвертация.</summary>
    Conversion,
}
