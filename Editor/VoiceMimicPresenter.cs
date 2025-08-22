namespace VoiceMimic
{
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
            var snap = view.SnapshotFromView();
            var validation = model.Validate(snap);
            if (validation.isOk == false)
            {
                view.ShowError(validation.messages);
                return;
            }

            var ordered = model.OrderSections(snap);
            var pcm     = model.Render(snap, ordered);
            view.Save(pcm);
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
            view.Play(pcm);
        }
    }
}
