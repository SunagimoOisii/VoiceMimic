namespace VoiceMimic
{
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
            if (TryBuildPcm(out var pcm) == false) return;

            var path = EditorUtility.SaveFilePanel("書き出し", "", "output.wav", "wav");
            if (string.IsNullOrEmpty(path)) return;

            WavExporter.Export(pcm, path);
            AssetDatabase.Refresh();
        }

        public void HandlePlay()
        {
            if (TryBuildPcm(out var pcm) == false) return;
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

        private bool TryBuildPcm(out VoiceMimicModel.PcmBuffer pcm)
        {
            var snap = view.SnapshotFromView();
            var validation = model.Validate(snap);
            if (validation.isOk == false)
            {
                view.ShowError(validation.messages);
                pcm = null;
                return false;
            }

            var ordered = model.OrderSections(snap);
            pcm = model.Render(snap, ordered);
            return true;
        }

    }
}
