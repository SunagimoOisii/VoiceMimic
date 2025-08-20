using UnityEngine;
using VoiceMimic.Model;

namespace VoiceMimic
{
    /// <summary>
    /// 設定保存用アセット。
    /// </summary>
    [CreateAssetMenu(menuName = "VoiceMimic/Asset")]
    public class VoiceMimicAsset : ScriptableObject
    {
        public VoiceMimicModel.Section[] sections;
        public int? randomSeed;
        public int sampleRate = 44100;
        public int version = 1;
    }
}
