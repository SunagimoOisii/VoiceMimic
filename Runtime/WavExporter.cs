namespace VoiceMimic
{
    using System;
    using System.IO;
    using System.Text;
    using UnityEngine;

    /// <summary>
    /// PCM バッファを WAV 形式のファイルへ書き出すユーティリティ
    /// </summary>
    public static class WavExporter
    {
        public static void Export(VoiceMimicModel.PcmBuffer pcm, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("出力パスが指定されていません", nameof(path));
            }

            var dir = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(dir) == false && Directory.Exists(dir) == false)
            {
                Directory.CreateDirectory(dir);
            }

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
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

