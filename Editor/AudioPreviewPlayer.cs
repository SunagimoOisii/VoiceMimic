namespace VoiceMimic
{
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// 音声シーケンス生成ウィンドウでのシーケンス試聴に用いる
    /// </summary>
    public static class AudioPreviewPlayer
    {
        private static GameObject  host;
        private static AudioSource source;

        public static void PlayClip(AudioClip clip)
        {
            StopAndDestroyItself();
            host = EditorUtility.CreateGameObjectWithHideFlags("AudioPreview",
                HideFlags.HideAndDontSave,　typeof(AudioSource));

            source             = host.GetComponent<AudioSource>();
            source.clip        = clip;
            source.playOnAwake = false;
            source.Play();
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            if (source == null || source.isPlaying == false) StopAndDestroyItself();
        }

        public static void StopAndDestroyItself()
        {
            EditorApplication.update -= Update;
            if (source != null) source.Stop();
            if (host != null)   Object.DestroyImmediate(host);

            host   = null;
            source = null;
        }
    }
}
