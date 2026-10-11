using System.Text;
using Tesseract;

namespace vseMediaSearch.Services;

/// <summary>
/// Runs local, offline optical character recognition (OCR) over still image frames using
/// Tesseract (via the `Tesseract` NuGet package). This reads on-screen text (code, slides,
/// diagrams) that never gets spoken aloud, complementing Whisper's audio transcription.
///
/// The English trained-data file ("eng.traineddata") is downloaded once from Tesseract's
/// official tessdata_fast release (Apache-2.0 licensed) and cached under the user's local
/// app-data folder, then reused for every future OCR call — no API key, no per-call cost,
/// works fully offline after the first run.
///
/// Native platform notes: on Windows, the `Tesseract` NuGet package ships the required
/// native libtesseract/libleptonica binaries automatically. On Linux/macOS, the system's
/// native Tesseract/Leptonica libraries must already be installed (e.g. via apt/brew),
/// since there is no equivalent bundled native binary for those platforms in this package.
/// </summary>
internal static class OcrService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

    private static readonly string TessDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "vseMediaSearch",
        "tessdata");

    private const string EnglishTrainedDataUrl =
        "https://github.com/tesseract-ocr/tessdata_fast/raw/main/eng.traineddata";

    /// <summary>
    /// Runs OCR over each image in <paramref name="frameFilePaths"/> (in order) and returns
    /// the raw per-frame recognized text (frames with no readable text yield an empty string).
    /// </summary>
    public static async Task<List<string>> RecognizeFramesAsync(
        IReadOnlyList<string> frameFilePaths,
        CancellationToken cancellationToken = default)
    {
        await EnsureTrainedDataDownloadedAsync(cancellationToken).ConfigureAwait(false);

        var results = new List<string>(frameFilePaths.Count);

        using var engine = new TesseractEngine(TessDataFolder, "eng", EngineMode.Default);

        foreach (var framePath in frameFilePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var img = Pix.LoadFromFile(framePath);
            using var page = engine.Process(img);
            results.Add(page.GetText().Trim());
        }

        return results;
    }

    private static async Task EnsureTrainedDataDownloadedAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(TessDataFolder);
        var trainedDataPath = Path.Combine(TessDataFolder, "eng.traineddata");

        if (File.Exists(trainedDataPath))
        {
            return;
        }

        try
        {
            await using var responseStream = await Http.GetStreamAsync(EnglishTrainedDataUrl, cancellationToken).ConfigureAwait(false);
            await using var fileStream = File.Create(trainedDataPath);
            await responseStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Could not auto-download the Tesseract English trained-data file from {EnglishTrainedDataUrl}. " +
                "Check your network connection and try again.", ex);
        }
    }

    /// <summary>
    /// Aggregates a sequence of per-frame OCR results into a single readable block of
    /// on-screen text, removing empty frames and collapsing consecutive duplicate/near-duplicate
    /// frame text (common when the same slide/code is shown across many sampled frames).
    /// </summary>
    public static string AggregateAndDeduplicate(IReadOnlyList<string> perFrameText)
    {
        var meaningfulEntries = perFrameText
            .Select(NormalizeWhitespace)
            .Where(text => text.Length > 0)
            .ToList();

        if (meaningfulEntries.Count == 0)
        {
            return string.Empty;
        }

        var deduped = new List<string>();
        string? previous = null;

        foreach (var entry in meaningfulEntries)
        {
            if (previous is not null && IsNearDuplicate(previous, entry))
            {
                continue;
            }

            deduped.Add(entry);
            previous = entry;
        }

        var builder = new StringBuilder();
        foreach (var entry in deduped)
        {
            builder.AppendLine(entry);
            builder.AppendLine("---");
        }

        return builder.ToString().TrimEnd('-', '\n', '\r');
    }

    private static string NormalizeWhitespace(string text)
    {
        return string.Join(' ', text.Split(
            [' ', '\t', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Two OCR results are treated as near-duplicates if one contains the other, or if they
    /// are identical after normalization — a cheap heuristic that catches the common case of
    /// the same on-screen content being sampled across several consecutive frames.
    /// </summary>
    private static bool IsNearDuplicate(string previous, string current)
    {
        if (string.Equals(previous, current, StringComparison.Ordinal))
        {
            return true;
        }

        return previous.Contains(current, StringComparison.Ordinal) ||
               current.Contains(previous, StringComparison.Ordinal);
    }
}
