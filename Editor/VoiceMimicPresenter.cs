namespace VoiceMimic
{
    using System;
    using System.IO;
    using System.Text;
    using UnityEditor;
    using UnityEngine;

    public class VoiceMimicPresenter
    {
        private readonly VoiceMimicModel model;
        private readonly VoiceMimicView  view;
        private const int AssetPickerControlID = 123456;

        public VoiceMimicPresenter(VoiceMimicModel model, VoiceMimicView view)
        {
            this.model = model;
            this.view  = view;
        }

        public void HandleExport()
        {
            var snap = view.SnapshotFromView();
            var validation = model.Validate(snap);
            if (validation.isOk == false)
            {
                view.ShowError(validation.messages);
                return;
            }

            var ordered = model.OrderSections(snap);
            var pcm     = model.Render(snap, ordered);

            var path = EditorUtility.SaveFilePanel("書き出し", "", "output.wav", "wav");
            if (string.IsNullOrEmpty(path)) return;

            ExportWav(pcm, path);
            AssetDatabase.Refresh();
        }

        public void HandlePlay()
        {
            var snap = view.SnapshotFromView();
            var validation = model.Validate(snap);
            if (validation.isOk == false)
            {
                view.ShowError(validation.messages);
                return;
            }

            var ordered = model.OrderSections(snap);
            var pcm     = model.Render(snap, ordered);
            if (pcm == null || pcm.samples == null || pcm.samples.Length == 0)
            {
                view.ShowNotification(new GUIContent("再生可能な音声データがありません"));
                return;
            }
            view.Play(pcm);
        }

        public void HandleSaveToAsset()
        {
            var path = EditorUtility.SaveFilePanelInProject("保存先を選択", "VoiceMimicAsset",
                "asset", "保存アセットを指定してください");
            if (string.IsNullOrEmpty(path)) return;

            var snap  = view.SnapshotFromView();
            var asset = ScriptableObject.CreateInstance<VoiceMimicAsset>();
            model.WriteToAsset(snap, asset);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            view.ShowNotification(new GUIContent($"保存完了: {path}"));
        }

        public void HandleLoadFromAsset()
        {
            EditorGUIUtility.ShowObjectPicker<VoiceMimicAsset>(null, false, "", AssetPickerControlID);
        }

        private void ExportWav(VoiceMimicModel.PcmBuffer pcm, string path)
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
