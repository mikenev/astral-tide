using UnityEditor;
using UnityEngine;

/// <summary>Import settings for Audio Dev's clips (they arrive without .meta files): music streams (the 80 s loop and the arrival
/// swell), SFX and the two beds decompress on load; no normalisation (the levels are mixed by hand, see Assets/Audio/README.md).
/// Looping is set on the AudioSource, not here.</summary>
public class AudioImport : AssetPostprocessor {
  void OnPreprocessAudio(){
    if (!assetPath.StartsWith("Assets/Audio/")) return;
    var ai = (AudioImporter)assetImporter; bool music = assetPath.StartsWith("Assets/Audio/Music/");
    var s = ai.defaultSampleSettings;
    s.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
    s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = music ? 0.6f : 0.7f; s.preloadAudioData = !music;
    ai.defaultSampleSettings = s; ai.forceToMono = false; ai.loadInBackground = music;
    var so = new SerializedObject(ai); var n = so.FindProperty("m_Normalize"); if (n != null){ n.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); }
  }
}
