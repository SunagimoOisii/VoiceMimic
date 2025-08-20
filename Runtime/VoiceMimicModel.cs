using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace VoiceMimic.Model
{
    /// <summary>
    /// 音声シーケンスの合成を行うモデルクラス。
    /// </summary>
    public class VoiceMimicModel
    {
        /// <summary>
        /// 入力区間情報。
        /// </summary>
        public class Section
        {
            public UnityEngine.Object clipRef;
            public int startSample;
            public int endSample;
            public float pitchSemitone;
            public int fineCent;
            public int fadeMs;
        }

        /// <summary>
        /// スナップショット。
        /// </summary>
        public class SequenceSnapshot
        {
            public Section[] sections;
            public int? randomSeed;
            public int sampleRate = 44100;
            public bool mono = true;
        }

        /// <summary>
        /// 書き出し先。
        /// </summary>
        public class ExportTarget
        {
            public string path;
        }

        /// <summary>
        /// PCMバッファ。
        /// </summary>
        public class PcmBuffer
        {
            public int sampleRate;
            public float[] samples = Array.Empty<float>();
        }

        public enum Severity
        {
            Error,
            Warning
        }

        public enum Category
        {
            Input,
            Config,
            IO
        }

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
            public List<Message> messages = new List<Message>();
        }

        /// <summary>
        /// 入力スナップショットの検証を行う。
        /// </summary>
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
                    path = "sections",
                    text = "区間が指定されていません"
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

        /// <summary>
        /// PCMバッファを生成する。
        /// </summary>
        public PcmBuffer Render(SequenceSnapshot snap, Section[] ordered)
        {
            var output = new List<float>();
            int lastFade = 0;
            foreach (var s in ordered)
            {
                var clip = s.clipRef as AudioClip;
                if (clip == null)
                {
                    continue;
                }

                int start = Mathf.Clamp(s.startSample, 0, clip.samples);
                int end = Mathf.Clamp(s.endSample, 0, clip.samples);
                int length = Math.Max(0, end - start);
                var src = new float[length];
                clip.GetData(src, start);

                float pitchRatio = Mathf.Pow(2f, (s.pitchSemitone + s.fineCent / 100f) / 12f);
                int resampledLength = Mathf.CeilToInt(length / pitchRatio);
                var pitched = new float[resampledLength];
                for (int i = 0; i < resampledLength; i++)
                {
                    float pos = i * pitchRatio;
                    int idx = Mathf.FloorToInt(pos);
                    float frac = pos - idx;
                    float a = src[Mathf.Clamp(idx, 0, length - 1)];
                    float b = src[Mathf.Clamp(idx + 1, 0, length - 1)];
                    pitched[i] = a + (b - a) * frac;
                }

                float peak = pitched.Length > 0 ? pitched.Max(x => Mathf.Abs(x)) : 0f;
                float targetLevel = Mathf.Pow(10f, -3f / 20f);
                if (peak > 0f)
                {
                    float gain = targetLevel / peak;
                    for (int i = 0; i < pitched.Length; i++)
                    {
                        pitched[i] *= gain;
                    }
                }

                int fade = Mathf.CeilToInt(s.fadeMs * snap.sampleRate / 1000f);
                if (output.Count >= fade && fade > 0)
                {
                    for (int i = 0; i < fade && i < pitched.Length; i++)
                    {
                        int outIndex = output.Count - fade + i;
                        float t = i / (float)fade;
                        output[outIndex] = output[outIndex] * (1f - t) + pitched[i] * t;
                    }
                    for (int i = fade; i < pitched.Length; i++)
                    {
                        output.Add(pitched[i]);
                    }
                }
                else
                {
                    output.AddRange(pitched);
                }

                lastFade = fade;
            }

            return new PcmBuffer { sampleRate = snap.sampleRate, samples = output.ToArray() };
        }

        /// <summary>
        /// WAVファイルとして書き出す。
        /// </summary>
        public void ExportWav(PcmBuffer pcm, ExportTarget target)
        {
            if (string.IsNullOrEmpty(target.path))
            {
                throw new ArgumentException("出力パスが指定されていません", nameof(target));
            }

            var dir = Path.GetDirectoryName(target.path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using var fs = new FileStream(target.path, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);
            int dataLength = pcm.samples.Length * 2;
            int riffLength = 36 + dataLength;

            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(riffLength);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1);
            bw.Write((short)1);
            bw.Write(pcm.sampleRate);
            bw.Write(pcm.sampleRate * 2);
            bw.Write((short)2);
            bw.Write((short)16);

            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(dataLength);

            for (int i = 0; i < pcm.samples.Length; i++)
            {
                short s = (short)Mathf.Clamp(pcm.samples[i] * 32767f, -32768f, 32767f);
                bw.Write(s);
            }
        }
    }
}

