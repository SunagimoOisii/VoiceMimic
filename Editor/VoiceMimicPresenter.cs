namespace VoiceMimic
{
    using UnityEditor;
    using UnityEngine;

    public class VoiceMimicPresenter
    {
        private readonly VoiceMimicModel model;
        private readonly VoiceMimicView  view;

        public VoiceMimicPresenter(VoiceMimicModel model, VoiceMimicView view)
        {
            this.model = model;
            this.view  = view;
        }

        public void HandleExport()
        {
            if (TryBuildPcm(out var pcm) == false) return;

            var path = view.PickExportPath();
            if (string.IsNullOrEmpty(path)) return;

            WavExporter.Export(pcm, path);
            AssetDatabase.Refresh();
        }

        public void HandlePlay()
        {
            if (TryBuildPcm(out var pcm) == false) return;
            if (pcm == null || pcm.samples == null || pcm.samples.Length == 0)
            {
                view.Notify("再生可能な音声データがありません");
                return;
            }
            view.Play(pcm);
        }

        public void HandleSaveToAsset()
        {
            var path = view.PickAssetPath();
            if (string.IsNullOrEmpty(path)) return;

            var snap  = view.SnapshotFromView();
            var asset = ScriptableObject.CreateInstance<VoiceMimicAsset>();
            model.WriteToAsset(snap, asset);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            view.Notify($"保存完了: {path}");
        }

        public void HandleLoadFromAsset()
        {
            view.ShowAssetPicker();
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
