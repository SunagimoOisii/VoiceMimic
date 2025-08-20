using UnityEditor;
using UnityEngine;

namespace VoiceMimic
{
    /// <summary>
    /// AudioSource を用いたプレビュー再生ユーティリティ。
    /// </summary>
    internal static class AudioPreviewPlayer
    {
        private static GameObject host;
        private static AudioSource source;

        /// <summary>
        /// 指定されたクリップを再生する。
        /// </summary>
        public static void PlayClip(AudioClip clip)
        {
            Stop();
            host = EditorUtility.CreateGameObjectWithHideFlags(
                "AudioPreview",
                HideFlags.HideAndDontSave,
                typeof(AudioSource));
            source = host.GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.clip = clip;
            source.Play();
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            if (source == null || !source.isPlaying)
            {
                Stop();
            }
        }

        /// <summary>
        /// 再生中のクリップを停止しオブジェクトを破棄する。
        /// </summary>
        public static void Stop()
        {
            EditorApplication.update -= Update;
            if (source != null)
            {
                source.Stop();
            }
            if (host != null)
            {
                Object.DestroyImmediate(host);
            }
            source = null;
            host = null;
        }
    }
}
