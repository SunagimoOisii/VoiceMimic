namespace VoiceMimic
{
    using UnityEngine;

    public sealed class VoiceMimicAsset : ScriptableObject
    {
        [System.Serializable]
        public struct SectionLike
        {
            public AudioClip clip;
            public int startSample;
            public int endSample;
            public float pitchSemitone;
            public int fineCent;
            public int fadeMs;
        }

        public SectionLike[] sections;
        public int? randomSeed;
        public int sampleRate = 44100;
        public int version = 1;

        public SequenceSnapshot ToSnapshot()
        {
            var list = new System.Collections.Generic.List<Section>();
            if (sections != null)
            {
                foreach (var s in sections)
                {
                    list.Add(new Section
                    {
                        clipRef = s.clip,
                        startSample = s.startSample,
                        endSample = s.endSample,
                        pitchSemitone = s.pitchSemitone,
                        fineCent = s.fineCent,
                        fadeMs = s.fadeMs
                    });
                }
            }
            return new SequenceSnapshot
            {
                sections = list.ToArray(),
                randomSeed = randomSeed,
                sampleRate = sampleRate,
                mono = true
            };
        }

        public void FromSnapshot(SequenceSnapshot snap)
        {
            if (snap == null) return;
            var list = new System.Collections.Generic.List<SectionLike>();
            if (snap.sections != null)
            {
                foreach (var s in snap.sections)
                {
                    list.Add(new SectionLike
                    {
                        clip = s.clipRef as AudioClip,
                        startSample = s.startSample,
                        endSample = s.endSample,
                        pitchSemitone = s.pitchSemitone,
                        fineCent = s.fineCent,
                        fadeMs = s.fadeMs
                    });
                }
            }
            sections = list.ToArray();
            randomSeed = snap.randomSeed;
            sampleRate = snap.sampleRate;
        }
    }
}
