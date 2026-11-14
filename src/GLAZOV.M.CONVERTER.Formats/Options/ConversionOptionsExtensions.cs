using GLAZOV.M.CONVERTER.Converting;

namespace GLAZOV.M.CONVERTER.Formats.Options;

/// <summary>
/// C# 14 extension members on <see cref="ConversionOptions"/> for standard audio, image, and video settings.
/// </summary>
public static class ConversionOptionsExtensions
{
    extension(ConversionOptions options)
    {
        /// <summary>
        /// Gets or sets audio encoding parameters.
        /// </summary>
        public AudioOptions? Audio
        {
            get => options.Get<AudioOptions>();
            set
            {
                if (value is not null)
                {
                    options.Set(value);
                }
            }
        }

        /// <summary>
        /// Gets or sets image encoding parameters.
        /// </summary>
        public ImageOptions? Image
        {
            get => options.Get<ImageOptions>();
            set
            {
                if (value is not null)
                {
                    options.Set(value);
                }
            }
        }

        /// <summary>
        /// Gets or sets video encoding parameters.
        /// </summary>
        public VideoOptions? Video
        {
            get => options.Get<VideoOptions>();
            set
            {
                if (value is not null)
                {
                    options.Set(value);
                }
            }
        }
    }
}