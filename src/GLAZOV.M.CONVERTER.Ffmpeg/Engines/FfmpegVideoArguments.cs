using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Formats.Options;
using System.Globalization;

namespace GLAZOV.M.CONVERTER.Ffmpeg.Engines;

/// <summary>
/// FFmpeg arguments for video to video: a pure function, no process started.
/// The first video track and the first audio track (when there is one) are re-encoded with the codecs of the target container.
/// </summary>
internal static class FfmpegVideoArguments
{
    // Quality 1..100 of VideoOptions maps linearly onto the codec scale: (value at 1, value at 100).
    // A constant: this defines what the shared quality parameter means; another mapping would be another contract.
    private static readonly (int AtLowest, int AtHighest) H264Crf = (45, 12);
    private static readonly (int AtLowest, int AtHighest) Vp9Crf = (55, 15);
    private static readonly (int AtLowest, int AtHighest) Mpeg4Qscale = (31, 2);

    private enum Codec
    {
        H264,
        Vp9,
        Mpeg4,
    }

    /// <summary>
    /// Builds FFmpeg command-line arguments for video-to-video conversion.
    /// </summary>
    /// <param name="to">Target container format (MP4, MKV, WebM, MOV, or AVI).</param>
    /// <param name="options">Per-call conversion options containing video settings, audio settings, and visual operations.</param>
    /// <param name="inputPath">Path to the input file.</param>
    /// <param name="outputPath">Path to the output file.</param>
    /// <param name="plan">Stream copy plan per track; <c>null</c> encodes both tracks.</param>
    /// <param name="codecs">Probed input track codecs; used to apply required container tags when copying HEVC.</param>
    /// <param name="settings">Default encoding settings used when <paramref name="options"/> specifies none.</param>
    internal static IReadOnlyList<string> Build(
        FileFormat to,
        ConversionOptions? options,
        string inputPath,
        string outputPath,
        CopyPlan? plan = null,
        TrackCodecs? codecs = null,
        FfmpegSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(to);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        settings ??= new FfmpegSettings();

        VideoOptions? video = options?.Video;
        AudioOptions? audio = options?.Audio;
        IEnumerable<IConversionOperation>? operations = options?.Operations;

        Codec codec = VideoCodec(to);
        List<string> args =
        [
            "-hide_banner", "-loglevel", "info", "-nostats",
            "-i", inputPath,
            "-map", "0:v:0", "-map", "0:a:0?",
        ];

        if (plan?.CopyVideo == true)
        {
            args.AddRange(["-c:v", "copy"]);
            if (codecs?.Video == "hevc" && (to.Equals(FileFormat.Mp4) || to.Equals(FileFormat.Mov)))
            {
                args.AddRange(["-tag:v", "hvc1"]);
            }
        }
        else
        {
            args.AddRange(["-pix_fmt", "yuv420p"]);
            AddVideoCodec(args, codec, video, settings);
            AddFilters(args, codec, video, operations);
        }

        if (plan?.CopyAudio == true)
        {
            args.AddRange(["-c:a", "copy"]);
        }
        else
        {
            AddAudio(args, codec, audio, settings);
        }

        if (to.Equals(FileFormat.Mp4) || to.Equals(FileFormat.Mov))
        {
            args.AddRange(["-movflags", "+faststart"]);
        }

        args.AddRange(["-f", Muxer(to), "-y", outputPath]);
        return args;
    }


    /// <summary>
    /// Name of the video encoder for a container (also used to explain a missing encoder).
    /// </summary>
    /// <param name="to">Target container.</param>
    internal static string VideoEncoder(FileFormat to)
    {
        return VideoEncoderName(VideoCodec(to));
    }

    private static Codec VideoCodec(FileFormat to)
    {
        return to switch
        {
            _ when to.Equals(FileFormat.Webm) => Codec.Vp9,
            _ when to.Equals(FileFormat.Avi) => Codec.Mpeg4,
            _ when to.Equals(FileFormat.Mp4) ||
                   to.Equals(FileFormat.Mov) ||
                   to.Equals(FileFormat.Mkv) => Codec.H264,
            _ => throw new ArgumentException($"Видео-движок не умеет формат «{to.Name}».", nameof(to))
        };
    }

    private static string Muxer(FileFormat to)
    {
        return to.Equals(FileFormat.Mkv) ? "matroska" : to.Extension;
    }

    private static void AddVideoCodec(List<string> args, Codec codec, VideoOptions? video, FfmpegSettings settings)
    {
        args.AddRange(["-c:v", VideoEncoderName(codec)]);

        if (video?.Bitrate is int bitrate)
        {
            // A bare -b:v is only a hint for these encoders and overshoots on short clips; a ceiling (VP9: a floor too) keeps it close.
            args.AddRange(["-b:v", Text(bitrate), "-maxrate", Text(bitrate)]);
            args.AddRange(codec == Codec.Vp9 ? ["-minrate", Text(bitrate)] : ["-bufsize", ((long)bitrate * 2).ToString(CultureInfo.InvariantCulture)]);
            return;
        }

        int? quality = video?.Quality;
        switch (codec)
        {
            case Codec.H264:
                args.AddRange(["-crf", Text(quality is int q ? Scale(q, H264Crf) : settings.H264Crf)]);
                break;
            case Codec.Vp9:
                // CRF mode of VP9 needs a zero bitrate; row-mt is always a gain, so it is not a setting.
                args.AddRange(["-crf", Text(quality is int v ? Scale(v, Vp9Crf) : settings.Vp9Crf), "-b:v", "0", "-row-mt", "1", "-deadline", settings.Vp9Deadline, "-cpu-used", Text(settings.Vp9CpuUsed)]);
                break;
            default:
                args.AddRange(["-q:v", Text(quality is int m ? Scale(m, Mpeg4Qscale) : settings.Mpeg4Qscale)]);
                break;
        }
    }

    private static string VideoEncoderName(Codec codec)
    {
        return codec switch
        {
            Codec.H264 => "libx264",
            Codec.Vp9 => "libvpx-vp9",
            _ => "mpeg4",
        };
    }

    // Quality 1 gives the first value, 100 the second; the scale may run down (CRF is better when smaller).
    private static int Scale(int quality, (int AtLowest, int AtHighest) range)
    {
        return (int)Math.Round(range.AtLowest + ((quality - 1) * (range.AtHighest - range.AtLowest) / 99.0));
    }

    private static void AddFilters(List<string> args, Codec codec, VideoOptions? video, IEnumerable<IConversionOperation>? operations)
    {
        List<string> filters = [];
        if (video?.Width is not null || video?.Height is not null)
        {
            string width = video.Width is int w ? Text(Even(w)) : "-2";
            string height = video.Height is int h ? Text(Even(h)) : "-2";
            filters.Add($"scale={width}:{height}");
        }
        else if (codec == Codec.H264)
        {
            // H.264 in yuv420p cannot take an odd frame size; the size is rounded down to even like a requested one.
            filters.Add("scale=trunc(iw/2)*2:trunc(ih/2)*2");
        }

        foreach (IConversionOperation operation in operations ?? [])
        {
            filters.Add(OperationFilter(operation));
        }

        if (video?.FrameRate is double rate)
        {
            filters.Add($"fps={rate.ToString("R", CultureInfo.InvariantCulture)}");
        }

        if (filters.Count > 0)
        {
            args.AddRange(["-vf", string.Join(',', filters)]);
        }
    }

    // Rotate right is a transpose; a percent scale rounds each side down to even, never below 2.
    private static string OperationFilter(IConversionOperation operation)
    {
        return operation switch
        {
            RotateOperation { Angle: RotationAngle.Degrees90 } => "transpose=1",
            RotateOperation { Angle: RotationAngle.Degrees180 } => "transpose=1,transpose=1",
            RotateOperation { Angle: RotationAngle.Degrees270 } => "transpose=2",
            ScalePercentOperation { Percent: int percent } =>
                $"scale=w='max(2,trunc(iw*{Text(percent)}/200)*2)':h='max(2,trunc(ih*{Text(percent)}/200)*2)'",
            _ => throw new ArgumentException($"Действие «{operation.Name}» видео-движок не умеет.", nameof(operation)),
        };
    }


    // Sizes are rounded down to even numbers, never below 2.
    private static int Even(int value)
    {
        return Math.Max(2, value - (value % 2));
    }

    private static void AddAudio(List<string> args, Codec codec, AudioOptions? audio, FfmpegSettings settings)
    {
        switch (codec)
        {
            case Codec.H264:
                args.AddRange(["-c:a", "aac", "-b:a", Text(audio?.Bitrate ?? settings.AacBitrate)]);
                break;
            case Codec.Vp9:
                args.AddRange(["-c:a", "libopus"]);
                if (audio?.Bitrate is int opusBitrate)
                {
                    args.AddRange(["-b:a", Text(opusBitrate)]);
                }

                break;
            default:
                args.AddRange(["-c:a", "libmp3lame"]);
                args.AddRange(audio?.Bitrate is int mp3Bitrate ? ["-b:a", Text(mp3Bitrate)] : ["-q:a", Text(settings.Mp3Quality)]);
                break;
        }

        if (audio?.SampleRate is int sampleRate)
        {
            args.AddRange(["-ar", Text(sampleRate)]);
        }

        if (audio?.Channels is int channels)
        {
            args.AddRange(["-ac", Text(channels)]);
        }
    }

    private static string Text(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
