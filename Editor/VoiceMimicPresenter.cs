using System.Collections.Generic;

namespace VoiceMimic
{
    /// <summary>
    /// View からの操作を受け取り Model を呼び出すプレゼンター。
    /// </summary>
    public class VoiceMimicPresenter
    {
        private readonly VoiceMimicModel model;
        private readonly IVoiceMimicView view;

        public VoiceMimicPresenter(VoiceMimicModel model, IVoiceMimicView view)
        {
            this.model = model;
            this.view = view;
        }

        /// <summary>
        /// 作成ボタン押下処理。
        /// </summary>
        public void HandleExport()
        {
            var snap = view.SnapshotFromView();
            var validation = model.Validate(snap);
            if (!validation.isOk)
            {
                view.ShowError(validation.messages);
                return;
            }

            var ordered = model.OrderSections(snap);
            var pcm = model.Render(snap, ordered);
            view.Save(pcm);
        }

        /// <summary>
        /// 再生ボタン押下処理。
        /// </summary>
        public void HandlePlay()
        {
            var snap = view.SnapshotFromView();
            var validation = model.Validate(snap);
            if (!validation.isOk)
            {
                view.ShowError(validation.messages);
                return;
            }

            var ordered = model.OrderSections(snap);
            var pcm = model.Render(snap, ordered);
            view.Play(pcm);
        }
    }

    /// <summary>
    /// プレゼンターが依存する View のインターフェース。
    /// </summary>
    public interface IVoiceMimicView
    {
        VoiceMimicModel.SequenceSnapshot SnapshotFromView();
        void ShowError(List<VoiceMimicModel.Message> messages);
        void Save(VoiceMimicModel.PcmBuffer pcm);
        void Play(VoiceMimicModel.PcmBuffer pcm);
    }
}
