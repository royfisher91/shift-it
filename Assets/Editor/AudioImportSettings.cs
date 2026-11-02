using UnityEditor;
using UnityEngine;

namespace KT.ShiftIt.EditorTools
{
    /// <summary>
    /// Forces the audio import settings on every clip in the project, so they are a committed,
    /// reproducible fact rather than something somebody set once in the inspector.
    ///
    /// The two settings that matter for a WebGL jam entry:
    ///   - MUSIC streams. A 44-second track decompressed on load costs several megabytes of memory
    ///     for no reason; streamed, it costs a buffer.
    ///   - SFX decompress on load. A half-second explosion must fire the instant it is asked for,
    ///     and decoding it on demand puts a stutter exactly where the game least wants one.
    ///
    /// Vorbis quality is kept modest on purpose: the whole audio set is about 1.3 MB, which matters
    /// when the build has to download in a browser before anyone can play.
    /// </summary>
    public sealed class AudioImportSettings : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            var importer = (AudioImporter)assetImporter;
            bool isMusic = assetPath.Contains("music_");

            var settings = importer.defaultSampleSettings;
            settings.loadType = isMusic ? AudioClipLoadType.Streaming
                                        : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = isMusic ? 0.45f : 0.70f;
            settings.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
            // preloadAudioData moved onto the per-platform sample settings in Unity 6; the importer
            // property is obsolete and is a compile error here.
            settings.preloadAudioData = !isMusic;
            importer.defaultSampleSettings = settings;

            importer.forceToMono = false;
            importer.loadInBackground = isMusic;

            Debug.Log("[SHIFTIT] audio import: " + assetPath +
                      (isMusic ? "  -> streaming, q0.45" : "  -> decompress on load, q0.70"));
        }
    }
}
