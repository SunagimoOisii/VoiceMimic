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
        private FloatField startMsField;
        private FloatField endMsField;
        private MinMaxSlider rangeSlider;
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
            startMsField = new FloatField("開始(ms)");
            endMsField = new FloatField("終了(ms)");
            rangeSlider = new MinMaxSlider("範囲(ms)", 0f, 0f, 0f, 0f);
            pitchField = new FloatField("Pitch Semitone");
            centField = new IntegerField("Fine Cent");
            fadeField = new IntegerField("Fade Ms") { value = 40 };

            root.Add(clipField);
            root.Add(startMsField);
            root.Add(endMsField);
            root.Add(rangeSlider);
            root.Add(pitchField);
            root.Add(centField);
            root.Add(fadeField);

            clipField.RegisterValueChangedCallback(e =>
            {
                var clip = e.newValue as AudioClip;
                if (clip != null)
                {
                    float lengthMs = clip.length * 1000f;
                    rangeSlider.lowLimit = 0f;
                    rangeSlider.highLimit = lengthMs;
                    rangeSlider.lowValue = 0f;
                    rangeSlider.highValue = lengthMs;
                    startMsField.SetValueWithoutNotify(0f);
                    endMsField.SetValueWithoutNotify(lengthMs);
                }
            });

            rangeSlider.RegisterValueChangedCallback(e =>
            {
                startMsField.SetValueWithoutNotify(e.newValue.x);
                endMsField.SetValueWithoutNotify(e.newValue.y);
            });

            startMsField.RegisterValueChangedCallback(e =>
            {
                rangeSlider.lowValue = Mathf.Clamp(e.newValue, rangeSlider.lowLimit, rangeSlider.highValue);
            });

            endMsField.RegisterValueChangedCallback(e =>
            {
                rangeSlider.highValue = Mathf.Clamp(e.newValue, rangeSlider.lowValue, rangeSlider.highLimit);
            });

            var exportButton = new Button(() => presenter.HandleExport()) { text = "書き出し" };
            root.Add(exportButton);
            var playButton = new Button(() => presenter.HandlePlay()) { text = "再生" };
            root.Add(playButton);
        }

        public VoiceMimicModel.SequenceSnapshot SnapshotFromView()
        {
            var clip = clipField.value as AudioClip;
            int sampleRate = clip != null ? clip.frequency : 44100;
            int startSample = Mathf.FloorToInt(startMsField.value / 1000f * sampleRate);
            int endSample = Mathf.FloorToInt(endMsField.value / 1000f * sampleRate);

            var section = new VoiceMimicModel.Section
            {
                clipRef = clip,
                startSample = startSample,
                endSample = endSample,
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
