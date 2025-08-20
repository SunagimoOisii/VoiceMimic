using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VoiceMimic
{
    /// <summary>
    /// VoiceMimic のエディタウィンドウ。
    /// </summary>
    public class VoiceMimicWindow : EditorWindow, IVoiceMimicView
    {
        private VoiceMimicPresenter presenter;
        private VoiceMimicModel model;
        private ObjectField clipField;
        private IntegerField startField;
        private IntegerField endField;
        private FloatField pitchField;
        private IntegerField centField;
        private IntegerField fadeField;

        [MenuItem("Tools/VoiceMimic")]
        public static void ShowWindow()
        {
            var window = GetWindow<VoiceMimicWindow>();
            window.titleContent = new GUIContent("Voice Mimic");
        }

        private void OnEnable()
        {
            model = new VoiceMimicModel();
            presenter = new VoiceMimicPresenter(model, this);
            var root = rootVisualElement;
            root.Add(new Label("Voice Mimic"));

            clipField = new ObjectField("Clip") { objectType = typeof(AudioClip) };
            startField = new IntegerField("Start Sample");
            endField = new IntegerField("End Sample");
            pitchField = new FloatField("Pitch Semitone");
            centField = new IntegerField("Fine Cent");
            fadeField = new IntegerField("Fade Ms") { value = 40 };

            root.Add(clipField);
            root.Add(startField);
            root.Add(endField);
            root.Add(pitchField);
            root.Add(centField);
            root.Add(fadeField);

            var exportButton = new Button(() => presenter.HandleExport()) { text = "書き出し" };
            root.Add(exportButton);
            var playButton = new Button(() => presenter.HandlePlay()) { text = "再生" };
            root.Add(playButton);
        }

        public VoiceMimicModel.SequenceSnapshot SnapshotFromView()
        {
            var section = new VoiceMimicModel.Section
            {
                clipRef = clipField.value as AudioClip,
                startSample = startField.value,
                endSample = endField.value,
                pitchSemitone = pitchField.value,
                fineCent = centField.value,
                fadeMs = fadeField.value
            };
            return new VoiceMimicModel.SequenceSnapshot { sections = new[] { section } };
        }

        public void ShowError(System.Collections.Generic.List<VoiceMimicModel.Message> messages)
        {
            var text = string.Join("\n", messages.Select(m => $"{m.category}: {m.text}"));
            EditorUtility.DisplayDialog("入力エラー", text, "OK");
        }

        public void Save(VoiceMimicModel.PcmBuffer pcm)
        {
            var path = EditorUtility.SaveFilePanel("書き出し", "", "output.wav", "wav");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            model.ExportWav(pcm, new VoiceMimicModel.ExportTarget { path = path });
            AssetDatabase.Refresh();
        }

        public void Play(VoiceMimicModel.PcmBuffer pcm)
        {
            if (pcm == null || pcm.samples == null || pcm.samples.Length == 0)
            {
                EditorUtility.DisplayDialog("再生エラー", "再生可能な音声データがありません", "OK");
                return;
            }

            int sampleCount = pcm.samples.Length / pcm.channels;
            var clip = AudioClip.Create("preview", sampleCount, pcm.channels, pcm.sampleRate, false);
            clip.SetData(pcm.samples, 0);
            AudioPreviewPlayer.PlayClip(clip);
        }
    }
}
