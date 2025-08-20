namespace VoiceMimic
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// 区間定義(スナップショットの要素)
    /// 不変条件: startSample ≤ endSample, fadeMs ≥ 0
    /// </summary>
    [Serializable]
    public class Section
    {
        public UnityEngine.Object clipRef; // AudioClip を参照
        public int startSample;            // 入力クリップ基準のサンプル開始(含む)
        public int endSample;              // 入力クリップ基準のサンプル終了(含まない)
        public float pitchSemitone;        // 半音単位のピッチ変化(±12 程度)
        public int fineCent;               // セント単位の微調整
        public int fadeMs = 40;            // クロスフェード長さ
    }

    /// <summary>
    /// エクスポート先
    /// </summary>
    [Serializable]
    public class ExportTarget
    {
        public string path; // 拡張子は .wav を推奨
    }

    /// <summary>
    /// スナップショット(押下時点のビュー状態の不変束)
    /// </summary>
    [Serializable]
    public class SequenceSnapshot
    {
        public Section[] sections;
        public int? randomSeed;    // 並びのシード(任意)
        public int sampleRate = 44100; // 出力サンプリングレート
        public bool mono = true;        // 本実装は常にモノラル
    }

    public enum MessageSeverity { Error, Warning }
    public enum MessageCategory { Input, Config, IO }

    [Serializable]
    public class Message
    {
        public MessageSeverity severity;
        public MessageCategory category;
        public string path;   // 例 sections[i].startSample
        public string text;

        public Message(MessageSeverity sev, MessageCategory cat, string path, string text)
        {
            this.severity = sev;
            this.category = cat;
            this.path = path;
            this.text = text;
        }
    }

    [Serializable]
    public class ValidationResult
    {
        public bool isOk;
        public List<Message> messages = new List<Message>();
    }

    /// <summary>
    /// 出力PCM
    /// </summary>
    [Serializable]
    public class PcmBuffer
    {
        public int sampleRate;
        public float[] samples; // モノラル規定
    }

    /// <summary>
    /// 音声合成エンジン
    /// 責務: 入力検証, 並び確定, 合成, 正規化, クロスフェード, エクスポート
    /// 依存: UI, Undo に依存しない
    /// </summary>
    public sealed class AudioSequenceEngine
    {
        /// <summary>
        /// 契約: snap は null ではないこと, sections が null ではないこと
        /// 事後: messages に検出内容を格納, 致命的エラーが無ければ isOk=true
        /// </summary>
        public ValidationResult Validate(SequenceSnapshot snap)
        {
            var vr = new ValidationResult { isOk = true };
            if (snap == null)
            {
                vr.isOk = false;
                vr.messages.Add(new Message(MessageSeverity.Error, MessageCategory.Config, nameof(snap), "スナップショットが未指定"));
                return vr;
            }
            if (snap.sections == null)
            {
                vr.isOk = false;
                vr.messages.Add(new Message(MessageSeverity.Error, MessageCategory.Input, nameof(snap.sections), "区間配列が未設定"));
                return vr;
            }
            if (snap.sampleRate <= 0)
            {
                vr.isOk = false;
                vr.messages.Add(new Message(MessageSeverity.Error, MessageCategory.Config, nameof(snap.sampleRate), "サンプルレートが不正"));
            }

            for (int i = 0; i < snap.sections.Length; i++)
            {
                var s = snap.sections[i];
                string basePath = $"sections[{i}]";
                if (s == null)
                {
                    vr.isOk = false;
                    vr.messages.Add(new Message(MessageSeverity.Error, MessageCategory.Input, basePath, "区間が未設定"));
                    continue;
                }
                if (s.clipRef == null)
                {
                    vr.isOk = false;
                    vr.messages.Add(new Message(MessageSeverity.Error, MessageCategory.Input, basePath + ".clipRef", "AudioClip が未指定"));
                }
                if (s.startSample > s.endSample)
                {
                    vr.isOk = false;
                    vr.messages.Add(new Message(MessageSeverity.Error, MessageCategory.Input, basePath + ".range", "startSample ≤ endSample を満たしていない"));
                }
                if (s.startSample == s.endSample)
                {
                    // プロダクト方針: 0長はエラーとして扱う
                    vr.isOk = false;
                    vr.messages.Add(new Message(MessageSeverity.Error, MessageCategory.Input, basePath + ".range", "区間長が0"));
                }
                if (Mathf.Abs(s.pitchSemitone) > 12.001f)
                {
                    vr.isOk = false;
                    vr.messages.Add(new Message(MessageSeverity.Error, MessageCategory.Input, basePath + ".pitchSemitone", "ピッチ変化は±12以内"));
                }
                if (s.fadeMs < 0)
                {
                    vr.isOk = false;
                    vr.messages.Add(new Message(MessageSeverity.Error, MessageCategory.Input, basePath + ".fadeMs", "フェード長が負"));
                }
            }
            return vr;
        }

        /// <summary>
        /// 並び確定。randomSeed があれば決定的シャッフル
        /// 前提: Validate 済み
        /// 事後: 元配列を破壊しない
        /// </summary>
        public Section[] OrderSections(SequenceSnapshot snap)
        {
            var src = snap.sections;
            var arr = new List<Section>(src);
            if (snap.randomSeed.HasValue)
            {
                var rng = new System.Random(snap.randomSeed.Value);
                for (int i = arr.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (arr[i], arr[j]) = (arr[j], arr[i]);
                }
            }
            return arr.ToArray();
        }

        /// <summary>
        /// 合成本体
        /// 事前: Validate の isOk が真, ordered は snap.sections の派生
        /// 事後: クリップ0件, 出力長は許容誤差内
        /// 例外: 入力不良で IOException は投げない
        /// </summary>
        public PcmBuffer Render(SequenceSnapshot snap, Section[] ordered)
        {
            var outBuf = new List<float>(1024);
            int sr = Mathf.Max(1, snap.sampleRate);
            int prevLen = 0;
            float[] last = Array.Empty<float>();

            foreach (var s in ordered)
            {
                var clip = s.clipRef as AudioClip;
                if (clip == null) continue; // Validate 済みのため通常到達しない

                // 切り出しとモノラル化
                var win = ReadWindowMono(clip, s.startSample, s.endSample);

                // ピッチとレートを一括で単純リサンプリング
                double pitchRatio = Math.Pow(2.0, s.pitchSemitone / 12.0 + s.fineCent / 1200.0);
                double step = pitchRatio * clip.frequency / (double)sr; // 入力インデックス増分
                var pitched = Resample(win, step);

                // ピーク正規化(目標 -3 dBFS)
                PeakNormalizeInPlace(pitched, -3.0f, out bool clipped);
                // clipped は常に false になるよう係数を計算しているため使用しない

                // クロスフェードで連結
                int fadeSamples = Mathf.Max(0, MsToSamples(s.fadeMs, sr));
                AppendWithCrossfade(ref outBuf, last, pitched, fadeSamples);

                // 次回用に末尾を保持
                last = pitched;
                prevLen = outBuf.Count;
            }

            return new PcmBuffer { sampleRate = sr, samples = outBuf.ToArray() };
        }

        /// <summary>
        /// WAV エクスポート(16-bit PCM, モノラル)
        /// 事前: pcm.samples は正規化済み, 出力先ディレクトリは存在
        /// 事後: path に有効な WAV が書き出される
        /// 例外: IO 例外は上位で扱う
        /// </summary>
        public void ExportWav(PcmBuffer pcm, ExportTarget target)
        {
            if (pcm == null) throw new ArgumentNullException(nameof(pcm));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (string.IsNullOrEmpty(target.path)) throw new ArgumentException("出力先パスが空", nameof(target.path));

            Directory.CreateDirectory(Path.GetDirectoryName(target.path));
            using (var fs = new FileStream(target.path, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                int channels = 1;
                int bitsPerSample = 16;
                int byteRate = pcm.sampleRate * channels * bitsPerSample / 8;
                short blockAlign = (short)(channels * bitsPerSample / 8);
                short audioFormat = 1; // PCM
                int dataLen = pcm.samples.Length * channels * bitsPerSample / 8;

                // RIFF ヘッダ
                WriteAscii(bw, "RIFF");
                bw.Write(36 + dataLen);
                WriteAscii(bw, "WAVE");
                // fmt チャンク
                WriteAscii(bw, "fmt ");
                bw.Write(16);
                bw.Write(audioFormat);
                bw.Write((short)channels);
                bw.Write(pcm.sampleRate);
                bw.Write(byteRate);
                bw.Write(blockAlign);
                bw.Write((short)bitsPerSample);
                // data チャンク
                WriteAscii(bw, "data");
                bw.Write(dataLen);

                // 本体
                for (int i = 0; i < pcm.samples.Length; i++)
                {
                    float v = Mathf.Clamp(pcm.samples[i], -1f, 1f);
                    short s = (short)Mathf.RoundToInt(v * short.MaxValue);
                    bw.Write(s);
                }
            }
        }

        // ---------- 内部ユーティリティ ----------

        private static void WriteAscii(BinaryWriter bw, string s)
        {
            var bytes = System.Text.Encoding.ASCII.GetBytes(s);
            bw.Write(bytes);
        }

        private static int MsToSamples(int ms, int sr)
        {
            return (int)Math.Round(ms * 0.001 * sr);
        }

        /// <summary>
        /// 指定クリップから [start, end) を取り出し, 全チャンネル平均でモノラル化
        /// 出力は入力クリップのサンプルレート基準
        /// </summary>
        private static float[] ReadWindowMono(AudioClip clip, int start, int end)
        {
            int total = clip.samples;
            int ch = clip.channels;
            start = Mathf.Clamp(start, 0, total);
            end = Mathf.Clamp(end, start, total);
            int len = Mathf.Max(0, end - start);
            if (len == 0) return Array.Empty<float>();

            var temp = new float[len * ch];
            clip.GetData(temp, start);
            var mono = new float[len];
            if (ch == 1)
            {
                Array.Copy(temp, mono, len);
            }
            else
            {
                for (int i = 0; i < len; i++)
                {
                    double sum = 0;
                    for (int c = 0; c < ch; c++) sum += temp[i * ch + c];
                    mono[i] = (float)(sum / ch);
                }
            }
            return mono;
        }

        /// <summary>
        /// 入力インデックス増分 step で単純リサンプリング。線形補間
        /// step 大きいほど短くなる
        /// </summary>
        private static float[] Resample(float[] input, double step)
        {
            if (input.Length == 0) return Array.Empty<float>();
            if (step <= 0) step = 1.0; // フォールバック

            int outLen = (int)Math.Max(1, Math.Floor(input.Length / step));
            var output = new float[outLen];
            double x = 0.0;
            for (int i = 0; i < outLen; i++)
            {
                int i0 = (int)x;
                int i1 = Math.Min(i0 + 1, input.Length - 1);
                float t = (float)(x - i0);
                output[i] = Mathf.Lerp(input[i0], input[i1], t);
                x += step;
            }
            return output;
        }

        /// <summary>
        /// 目標ピークを dBFS で指定して正規化
        /// 例: targetDb=-3 → 振幅約 0.7079
        /// </summary>
        private static void PeakNormalizeInPlace(float[] buf, float targetDb, out bool clipped)
        {
            clipped = false;
            if (buf.Length == 0) return;
            float peak = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float a = Mathf.Abs(buf[i]);
                if (a > peak) peak = a;
            }
            if (peak <= 0f) return;
            float targetAmp = DbToAmp(targetDb);
            float g = targetAmp / peak;
            for (int i = 0; i < buf.Length; i++)
            {
                float v = buf[i] * g;
                if (Mathf.Abs(v) > 1f) clipped = true;
                buf[i] = Mathf.Clamp(v, -1f, 1f);
            }
        }

        private static float DbToAmp(float db)
        {
            return Mathf.Pow(10f, db / 20f);
        }

        /// <summary>
        /// 等電力クロスフェードで連結
        /// </summary>
        private static void AppendWithCrossfade(ref List<float> dst, float[] prev, float[] cur, int fadeSamples)
        {
            if (dst.Count == 0)
            {
                dst.AddRange(cur);
                return;
            }
            int n = Math.Min(fadeSamples, Math.Min(prev.Length, cur.Length));
            int keep = dst.Count - (prev.Length);
            keep = Math.Max(0, keep);
            // dst の末尾 prev を取り出し
            // まず重ならない先頭部分はそのまま
            // その後, オーバーラップ領域を上書き混合
            // 最後に残りの cur を追記

            // 先頭はそのまま(keep サンプル)
            // 以降を再構築
            var rebuilt = new List<float>(keep + Math.Max(prev.Length, n) + Math.Max(0, cur.Length - n));
            for (int i = 0; i < keep; i++) rebuilt.Add(dst[i]);

            // 非オーバーラップの prev 残部
            int prevHead = Math.Max(0, prev.Length - n);
            for (int i = 0; i < prevHead; i++) rebuilt.Add(prev[i]);

            // オーバーラップ
            for (int i = 0; i < n; i++)
            {
                float t = (n <= 1) ? 1f : i / (float)(n - 1);
                // 等電力: cos と sin
                float a = Mathf.Cos(0.5f * Mathf.PI * t); // prev 比重
                float b = Mathf.Sin(0.5f * Mathf.PI * t); // cur 比重
                float v = prev[prev.Length - n + i] * a + cur[i] * b;
                rebuilt.Add(v);
            }

            // 残りの cur
            for (int i = n; i < cur.Length; i++) rebuilt.Add(cur[i]);

            dst = rebuilt;
        }
    }
}