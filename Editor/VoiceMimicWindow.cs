using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VoiceMimic.Model;
using VoiceMimic.Presenter;

namespace VoiceMimic.View
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

            var exportButton = new Button(async () => await presenter.HandleExportAsync()) { text = "書き出し" };
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
            foreach (var m in messages)
            {
                Debug.LogError($"{m.category}: {m.text}");
            }
        }

        public async Task SaveAsync(VoiceMimicModel.PcmBuffer pcm)
        {
            var path = EditorUtility.SaveFilePanel("書き出し", "", "output.wav", "wav");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            await Task.Run(() => model.ExportWav(pcm, new VoiceMimicModel.ExportTarget { path = path }));
            AssetDatabase.Refresh();
        }

        public void Play(VoiceMimicModel.PcmBuffer pcm)
        {
            var clip = AudioClip.Create("preview", pcm.samples.Length, 1, pcm.sampleRate, false);
            clip.SetData(pcm.samples, 0);
            var audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            var playMethod = audioUtil.GetMethod("PlayClip", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(AudioClip) }, null);
            playMethod.Invoke(null, new object[] { clip });
        }
    }
}
