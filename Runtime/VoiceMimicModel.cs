namespace VoiceMimic
{
    using System;
    using System.Text;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEngine;

    /// <summary>
    /// 音声シーケンス合成, 書き出し、設定用アセット書き出しを担当
    /// </summary>
    public class VoiceMimicModel
    {
        public class Section
        {
            public UnityEngine.Object clipRef;
            public int startSample;
            public int endSample;
            public int pitchSemitone;
            public int fineCent;
            public int fadeMs;
        }

        public class SequenceSnapshot
        {
            public Section[] sections;
            public int? randomSeed;
            public int sampleRate = 44100;
            public bool mono = true;
        }

        public class ExportTarget
        {
            public string path;
        }

        public class PcmBuffer
        {
            public int sampleRate;
            public int channels = 1;
            public float[] samples = Array.Empty<float>();
        }

        public enum Severity { Warning, Error }
        public enum Category { Input, Config, IO }

        public class Message
        {
            public Severity severity;
            public Category category;
            public string path;
            public string text;
        }

        public class ValidationResult
        {
            public bool isOk;
            public List<Message> messages = new();
        }

        public ValidationResult Validate(SequenceSnapshot snap)
        {
            var result = new ValidationResult { isOk = true };
            if (snap.sections == null || snap.sections.Length == 0)
            {
                result.isOk = false;
                result.messages.Add(new Message
                {
                    severity = Severity.Error,
                    category = Category.Input,
                    path     = "sections",
                    text     = "区間が指定されていません"
                });
                return result;
            }

            for (int i = 0; i < snap.sections.Length; i++)
            {
                var s = snap.sections[i];
                if (s.clipRef == null)
                {
                    result.isOk = false;
                    result.messages.Add(new Message
                    {
                        severity = Severity.Error,
                        category = Category.Input,
                        path = $"sections[{i}].clipRef",
                        text = "クリップが設定されていません"
                    });
                }

                if (s.startSample > s.endSample)
                {
                    result.isOk = false;
                    result.messages.Add(new Message
                    {
                        severity = Severity.Error,
                        category = Category.Input,
                        path = $"sections[{i}].startSample",
                        text = "開始サンプルが終了より大きいです"
                    });
                }

                if (Mathf.Abs(s.pitchSemitone) > 12f)
                {
                    result.isOk = false;
                    result.messages.Add(new Message
                    {
                        severity = Severity.Error,
                        category = Category.Input,
                        path = $"sections[{i}].pitchSemitone",
                        text = "ピッチは±12以内である必要があります"
                    });
                }

                if (s.fadeMs < 0)
                {
                    result.isOk = false;
                    result.messages.Add(new Message
                    {
                        severity = Severity.Error,
                        category = Category.Input,
                        path = $"sections[{i}].fadeMs",
                        text = "フェード長が負の値です"
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// 区間を並び替える。randomSeed が設定されていれば決定的シャッフル。
        /// </summary>
        public Section[] OrderSections(SequenceSnapshot snap)
        {
            var list = snap.sections.ToList();
            if (snap.randomSeed.HasValue)
            {
                var rng = new System.Random(snap.randomSeed.Value);
                for (int i = list.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (list[i], list[j]) = (list[j], list[i]);
                }
            }
            return list.ToArray();
        }

        public PcmBuffer Render(SequenceSnapshot snap, Section[] ordered)
        {
            int channels = snap.mono ? 1 : 2;
            var outputs = new List<float>[channels];
            for (int c = 0; c < channels; c++)
            {
                outputs[c] = new List<float>();
            }

            foreach (var s in ordered)
            {
                var clip = s.clipRef as AudioClip;
                if (clip == null) continue;

                int start  = Mathf.Clamp(s.startSample, 0, clip.samples);
                int end    = Mathf.Clamp(s.endSample, 0, clip.samples);
                int length = Math.Max(0, end - start);
                var src    = new float[length * clip.channels];
                clip.GetData(src, start);

                var pitchRatio      = Mathf.Pow(2f, (s.pitchSemitone + s.fineCent / 100f) / 12f);
                var resampledLength = Mathf.CeilToInt(length / pitchRatio);
                var pitched         = new float[channels][];
                for (int c = 0; c < channels; c++)
                {
                    pitched[c] = new float[resampledLength];
                    for (int i = 0; i < resampledLength; i++)
                    {
                        var pos   = i * pitchRatio;
                        var idx   = Mathf.FloorToInt(pos);
                        var frac  = pos - idx;
                        var srcCh = Mathf.Min(c, clip.channels - 1);
                        var a = src[Mathf.Clamp(idx, 0, length - 1) * clip.channels + srcCh];
                        var b = src[Mathf.Clamp(idx + 1, 0, length - 1) * clip.channels + srcCh];
                        pitched[c][i] = a + (b - a) * frac;
                    }

                    var peak = pitched[c].Length > 0 ? pitched[c].Max(x => Mathf.Abs(x)) : 0f;
                    if (peak <= 0f) continue;

                    var targetLevel = Mathf.Pow(10f, -3f / 20f);
                    var gain        = targetLevel / peak;
                    for (int i = 0; i < pitched[c].Length; i++)
                    {
                        pitched[c][i] *= gain;
                    }
                }

                int fade = Mathf.CeilToInt(s.fadeMs * snap.sampleRate / 1000f);
                for (int c = 0; c < channels; c++)
                {
                    var buf    = pitched[c];
                    var output = outputs[c];
                    if (output.Count >= fade && fade > 0)
                    {
                        for (int i = 0; i < fade && i < buf.Length; i++)
                        {
                            var t        = i / (float)fade;
                            var outIndex = output.Count - fade + i;
                            output[outIndex] = output[outIndex] * (1f - t) + buf[i] * t;
                        }
                        for (int i = fade; i < buf.Length; i++)
                        {
                            output.Add(buf[i]);
                        }
                    }
                    else output.AddRange(buf);
                }
            }

            int sampleCount = outputs[0].Count;
            var interleaved = new float[sampleCount * channels];
            for (int i = 0; i < sampleCount; i++)
            {
                for (int c = 0; c < channels; c++)
                {
                    interleaved[i * channels + c] = outputs[c][i];
                }
            }

            return new PcmBuffer { sampleRate = snap.sampleRate, channels = channels, samples = interleaved };
        }

        public void ExportWav(PcmBuffer pcm, ExportTarget target)
        {
            if (string.IsNullOrEmpty(target.path))
            {
                throw new ArgumentException("出力パスが指定されていません", nameof(target));
            }

            var dir = Path.GetDirectoryName(target.path);
            if (string.IsNullOrEmpty(dir) == false && Directory.Exists(dir) == false)
            {
                Directory.CreateDirectory(dir);
            }

            using var fs = new FileStream(target.path, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);
            var dataLength = pcm.samples.Length * 2;
            var riffLength = 36 + dataLength;

            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(riffLength);
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));

            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1);
            bw.Write((short)pcm.channels);
            bw.Write(pcm.sampleRate);
            bw.Write(pcm.sampleRate * pcm.channels * 2);
            bw.Write((short)(pcm.channels * 2));
            bw.Write((short)16);

            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(dataLength);

            for (int i = 0; i < pcm.samples.Length; i++)
            {
                var s = (short)Mathf.Clamp(pcm.samples[i] * 32767f, -32768f, 32767f);
                bw.Write(s);
            }
        }
    }
}

