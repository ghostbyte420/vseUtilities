using Whisper.net;
using Whisper.net.Ggml;

namespace vseMediaSearch.Services;

/// <summary>
/// Runs real, local, offline speech-to-text over an audio file using a Whisper model
/// (via Whisper.net). The model file is downloaded once and cached under the user's
/// local app-data folder (a reusable ML asset, not per-session media), then reused
/// for every future transcription — no API key, no per-call cost, works offline.
/// </summary>
internal static class WhisperTranscriptionService
{
    private static readonly string ModelFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "vseMediaSearch",
        "models");

    // "base.en" is a much faster CPU inference tradeoff than "small.en" -- long videos were
    // exceeding the MCP tool-call timeout during transcription. Still solid accuracy for
    // clearly-spoken English content like tutorials/recipes.
    private const GgmlType ModelType = GgmlType.BaseEn;
    private static readonly string ModelFileName = "ggml-base.en.bin";

    /// <summary>One transcribed speech segment with its start/end time.</summary>
    public sealed record TranscriptSegment(TimeSpan Start, TimeSpan End, string Text);

    /// <summary>
    /// Transcribes the given WAV audio file, downloading the Whisper model on first use.
    /// </summary>
    public static async Task<List<TranscriptSegment>> TranscribeAsync(
        string wavFilePath,
        CancellationToken cancellationToken = default)
    {
        var modelPath = await EnsureModelDownloadedAsync(cancellationToken).ConfigureAwait(false);

        using var whisperFactory = WhisperFactory.FromPath(modelPath);
        using var processor = whisperFactory.CreateBuilder()
            .WithLanguage("en")
            .WithThreads(Math.Max(2, Environment.ProcessorCount))
            .Build();

        var segments = new List<TranscriptSegment>();
        using var audioStream = File.OpenRead(wavFilePath);

        await foreach (var result in processor.ProcessAsync(audioStream, cancellationToken).ConfigureAwait(false))
        {
            segments.Add(new TranscriptSegment(result.Start, result.End, result.Text.Trim()));
        }

        return segments;
    }

    private static async Task<string> EnsureModelDownloadedAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(ModelFolder);
        var modelPath = Path.Combine(ModelFolder, ModelFileName);

        if (!File.Exists(modelPath))
        {
            using var modelStream = await WhisperGgmlDownloader.Default
                .GetGgmlModelAsync(ModelType, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            using var fileStream = File.Create(modelPath);
            await modelStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
        }

        return modelPath;
    }

    /// <summary>Formats transcript segments into a single timestamped transcript block of text.</summary>
    public static string FormatTranscript(List<TranscriptSegment> segments)
    {
        var lines = segments.Select(s =>
            $"[{s.Start.ToString(@"hh\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture)}] {s.Text}");
        return string.Join("\n", lines);
    }
}
