using System.Threading.Tasks;
using VoiceMimic.Model;

namespace VoiceMimic.Presenter
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
        public async Task HandleExportAsync()
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
            await view.SaveAsync(pcm);
        }

        /// <summary>
        /// 再生ボタン押下処理。
        /// </summary>
        public void HandlePlay()
        {
            var snap = view.SnapshotFromView();
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
        void ShowError(System.Collections.Generic.List<VoiceMimicModel.Message> messages);
        Task SaveAsync(VoiceMimicModel.PcmBuffer pcm);
        void Play(VoiceMimicModel.PcmBuffer pcm);
    }
}
