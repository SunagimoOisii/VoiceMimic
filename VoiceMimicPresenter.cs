namespace VoiceMimic
{
    using System;
    using UnityEditor;
    using UnityEngine;

    public interface IVoiceMimicView
    {
        event Action OnExportRequested;
        event Action OnPlayRequested;
        event Action OnStopRequested;
        event Action OnSaveRequested;

        SequenceSnapshot CaptureSnapshot();
        ExportTarget GetExportTarget();

        void SetBusyState(string label, float progress01);
        void ClearBusyState();
        void ShowWarning(string message);
        void ShowError(string message);
        void ShowInfo(string message);

        void PreviewPlay(PcmBuffer pcm);
        void PreviewStop();
    }

    public sealed class VoiceMimicPresenter
    {
        private readonly IVoiceMimicView view;
        private readonly AudioSequenceEngine engine;
        private PcmBuffer lastRendered;

        public VoiceMimicPresenter(IVoiceMimicView view, AudioSequenceEngine engine)
        {
            this.view = view;
            this.engine = engine;

            view.OnExportRequested += HandleExport;
            view.OnPlayRequested += HandlePlay;
            view.OnStopRequested += HandleStop;
            view.OnSaveRequested += HandleSave;
        }

        private void HandleExport()
        {
            try
            {
                view.SetBusyState("レンダリング中", 0f);
                var snap = view.CaptureSnapshot();

                var vr = engine.Validate(snap);
                if (!vr.isOk)
                {
                    foreach (var m in vr.messages)
                    {
                        if (m.severity == MessageSeverity.Error) view.ShowError($"{m.category}: {m.text} ({m.path})");
                        else view.ShowWarning($"{m.category}: {m.text} ({m.path})");
                    }
                    return;
                }

                var ordered = engine.OrderSections(snap);
                view.SetBusyState("合成中", 0.3f);
                lastRendered = engine.Render(snap, ordered);

                var target = view.GetExportTarget();
                if (target == null || string.IsNullOrEmpty(target.path))
                {
                    view.ShowWarning("出力先が未指定のためエクスポートを中止");
                    return;
                }
                view.SetBusyState("書き出し中", 0.7f);
                engine.ExportWav(lastRendered, target);
                view.ShowInfo($"書き出し完了: {target.path}");
            }
            catch (Exception ex)
            {
                view.ShowError($"エクスポート失敗: {ex.Message}");
            }
            finally
            {
                view.ClearBusyState();
            }
        }

        private void HandlePlay()
        {
            var snap = view.CaptureSnapshot();
            var vr = engine.Validate(snap);
            if (!vr.isOk)
            {
                foreach (var m in vr.messages)
                {
                    if (m.severity == MessageSeverity.Error) view.ShowError($"{m.category}: {m.text} ({m.path})");
                    else view.ShowWarning($"{m.category}: {m.text} ({m.path})");
                }
                return;
            }
            var ordered = engine.OrderSections(snap);
            lastRendered = engine.Render(snap, ordered);
            view.PreviewPlay(lastRendered);
        }

        private void HandleStop()
        {
            view.PreviewStop();
        }

        private void HandleSave()
        {
            var snap = view.CaptureSnapshot();
            var asset = ScriptableObject.CreateInstance<VoiceMimicAsset>();
            asset.FromSnapshot(snap);
            var path = EditorUtility.SaveFilePanelInProject("保存", "VoiceMimicAsset", "asset", "保存先を選択");
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
                view.ShowInfo("設定を保存しました");
            }
        }
    }
}
