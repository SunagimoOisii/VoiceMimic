using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace VoiceMimic
{
    /// <summary>
    /// VoiceMimic のエディタウィンドウ。
    /// </summary>
    public class VoiceMimicWindow : EditorWindow, IVoiceMimicView
    {
        private class SectionData
        {
            public AudioClip clip;
            public float startMs;
            public float endMs;
            public int pitchSemitone;
            public int fineCent;
            public int fadeMs = 40;
        }

        private const int PitchMin = -12;
        private const int PitchMax = 12;
        private const int CentMin = -50;
        private const int CentMax = 50;

        private VoiceMimicPresenter presenter;
        private VoiceMimicModel model;
        private readonly List<SectionData> sections = new List<SectionData>();
        private ListView sectionListView;
        private int selectedIndex = -1;

        private ObjectField clipField;
        private FloatField startMsField;
        private FloatField endMsField;
        private MinMaxSlider rangeSlider;
        private SliderInt pitchSlider;
        private SliderInt centSlider;
        private IntegerField pitchField;
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
            root.Clear();

            var split = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);
            root.Add(split);

            var leftPane = new VisualElement();
            split.Add(leftPane);

            sectionListView = new ListView();
            sectionListView.reorderable = true;
            sectionListView.itemsSource = sections;
            sectionListView.selectionType = SelectionType.Single;
            sectionListView.makeItem = () => new Label();
            sectionListView.bindItem = (e, i) =>
            {
                var label = (Label)e;
                var data = sections[i];
                label.text = data.clip != null ? data.clip.name : "未設定";
            };
            sectionListView.selectionChanged += _ =>
            {
                selectedIndex = sectionListView.selectedIndex;
                UpdateDetail();
            };
            leftPane.Add(sectionListView);

            var addButton = new Button(AddSection) { text = "追加" };
            leftPane.Add(addButton);

            var removeButton = new Button(RemoveSection) { text = "削除" };
            leftPane.Add(removeButton);

            var playButton = new Button(() => presenter.HandlePlay()) { text = "再生" };
            leftPane.Add(playButton);
            var exportButton = new Button(() => presenter.HandleExport()) { text = "書き出し" };
            leftPane.Add(exportButton);

            var rightPane = new VisualElement();
            split.Add(rightPane);

            clipField = new ObjectField("Clip") { objectType = typeof(AudioClip) };
            startMsField = new FloatField("開始(ms)");
            endMsField = new FloatField("終了(ms)");
            rangeSlider = new MinMaxSlider("範囲(ms)", 0f, 0f, 0f, 0f);
            pitchSlider = new SliderInt("Pitch Semitone", PitchMin, PitchMax);
            centSlider = new SliderInt("Fine Cent", CentMin, CentMax);
            pitchField = new IntegerField();
            centField = new IntegerField();
            fadeField = new IntegerField("Fade Ms") { value = 40 };

            var pitchContainer = new VisualElement();
            pitchContainer.style.flexDirection = FlexDirection.Row;
            pitchSlider.style.flexGrow = 1f;
            pitchField.style.width = 60f;
            pitchField.label = string.Empty;
            pitchContainer.Add(pitchSlider);
            pitchContainer.Add(pitchField);

            var centContainer = new VisualElement();
            centContainer.style.flexDirection = FlexDirection.Row;
            centSlider.style.flexGrow = 1f;
            centField.style.width = 60f;
            centField.label = string.Empty;
            centContainer.Add(centSlider);
            centContainer.Add(centField);

            rightPane.Add(clipField);
            rightPane.Add(startMsField);
            rightPane.Add(endMsField);
            rightPane.Add(rangeSlider);
            rightPane.Add(pitchContainer);
            rightPane.Add(centContainer);
            rightPane.Add(fadeField);

            clipField.RegisterValueChangedCallback(e =>
            {
                var data = CurrentSection();
                var clip = e.newValue as AudioClip;
                if (data != null)
                {
                    data.clip = clip;
                    if (clip != null)
                    {
                        float lengthMs = clip.length * 1000f;
                        data.startMs = 0f;
                        data.endMs = lengthMs;
                        rangeSlider.lowLimit = 0f;
                        rangeSlider.highLimit = lengthMs;
                        rangeSlider.SetValueWithoutNotify(new Vector2(0f, lengthMs));
                        startMsField.SetValueWithoutNotify(0f);
                        endMsField.SetValueWithoutNotify(lengthMs);
                    }
                }
                sectionListView.RefreshItems();
            });

            rangeSlider.RegisterValueChangedCallback(e =>
            {
                startMsField.SetValueWithoutNotify(e.newValue.x);
                endMsField.SetValueWithoutNotify(e.newValue.y);
                var data = CurrentSection();
                if (data != null)
                {
                    data.startMs = e.newValue.x;
                    data.endMs = e.newValue.y;
                }
            });

            startMsField.RegisterValueChangedCallback(e =>
            {
                float v = Mathf.Clamp(e.newValue, rangeSlider.lowLimit, rangeSlider.maxValue);
                rangeSlider.minValue = v;
                var data = CurrentSection();
                if (data != null)
                {
                    data.startMs = v;
                }
            });

            endMsField.RegisterValueChangedCallback(e =>
            {
                float v = Mathf.Clamp(e.newValue, rangeSlider.minValue, rangeSlider.highLimit);
                rangeSlider.maxValue = v;
                var data = CurrentSection();
                if (data != null)
                {
                    data.endMs = v;
                }
            });

            pitchSlider.RegisterValueChangedCallback(e =>
            {
                int v = Mathf.Clamp(e.newValue, PitchMin, PitchMax);
                pitchField.SetValueWithoutNotify(v);
                var data = CurrentSection();
                if (data != null)
                {
                    data.pitchSemitone = v;
                }
            });

            pitchField.RegisterValueChangedCallback(e =>
            {
                int v = Mathf.Clamp(e.newValue, PitchMin, PitchMax);
                pitchField.SetValueWithoutNotify(v);
                pitchSlider.SetValueWithoutNotify(v);
                var data = CurrentSection();
                if (data != null)
                {
                    data.pitchSemitone = v;
                }
            });

            centSlider.RegisterValueChangedCallback(e =>
            {
                int v = Mathf.Clamp(e.newValue, CentMin, CentMax);
                centField.SetValueWithoutNotify(v);
                var data = CurrentSection();
                if (data != null)
                {
                    data.fineCent = v;
                }
            });

            centField.RegisterValueChangedCallback(e =>
            {
                int v = Mathf.Clamp(e.newValue, CentMin, CentMax);
                centField.SetValueWithoutNotify(v);
                centSlider.SetValueWithoutNotify(v);
                var data = CurrentSection();
                if (data != null)
                {
                    data.fineCent = v;
                }
            });

            fadeField.RegisterValueChangedCallback(e =>
            {
                var data = CurrentSection();
                if (data != null)
                {
                    data.fadeMs = e.newValue;
                }
            });

            UpdateDetail();
        }

        private void AddSection()
        {
            sections.Add(new SectionData());
            sectionListView.RefreshItems();
            sectionListView.selectedIndex = sections.Count - 1;
        }

        private void RemoveSection()
        {
            sections.Remove(CurrentSection());
            sectionListView.RefreshItems();
            sectionListView.selectedIndex = sections.Count - 1;
        }

        private SectionData CurrentSection()
        {
            if (selectedIndex < 0 || selectedIndex >= sections.Count)
            {
                return null;
            }
            return sections[selectedIndex];
        }

        private void UpdateDetail()
        {
            var data = CurrentSection();
            bool has = data != null;
            clipField.SetEnabled(has);
            startMsField.SetEnabled(has);
            endMsField.SetEnabled(has);
            rangeSlider.SetEnabled(has);
            pitchSlider.SetEnabled(has);
            centSlider.SetEnabled(has);
            pitchField.SetEnabled(has);
            centField.SetEnabled(has);
            fadeField.SetEnabled(has);

            if (has)
            {
                clipField.SetValueWithoutNotify(data.clip);
                float lengthMs = data.clip != null ? data.clip.length * 1000f : 0f;
                rangeSlider.lowLimit = 0f;
                rangeSlider.highLimit = lengthMs;
                rangeSlider.SetValueWithoutNotify(new Vector2(data.startMs, data.endMs));
                startMsField.SetValueWithoutNotify(data.startMs);
                endMsField.SetValueWithoutNotify(data.endMs);
                int ps = Mathf.Clamp(data.pitchSemitone, PitchMin, PitchMax);
                int fc = Mathf.Clamp(data.fineCent, CentMin, CentMax);
                data.pitchSemitone = ps;
                data.fineCent = fc;
                pitchSlider.SetValueWithoutNotify(ps);
                pitchField.SetValueWithoutNotify(ps);
                centSlider.SetValueWithoutNotify(fc);
                centField.SetValueWithoutNotify(fc);
                fadeField.SetValueWithoutNotify(data.fadeMs);
            }
        }

        public VoiceMimicModel.SequenceSnapshot SnapshotFromView()
        {
            var list = new List<VoiceMimicModel.Section>();
            foreach (var s in sections)
            {
                var clip = s.clip;
                int sampleRate = clip != null ? clip.frequency : 44100;
                int startSample = Mathf.FloorToInt(s.startMs / 1000f * sampleRate);
                int endSample = Mathf.FloorToInt(s.endMs / 1000f * sampleRate);
                list.Add(new VoiceMimicModel.Section
                {
                    clipRef = clip,
                    startSample = startSample,
                    endSample = endSample,
                    pitchSemitone = s.pitchSemitone,
                    fineCent = s.fineCent,
                    fadeMs = s.fadeMs
                });
            }
            return new VoiceMimicModel.SequenceSnapshot { sections = list.ToArray() };
        }

        public void ShowError(List<VoiceMimicModel.Message> messages)
        {
            var text = string.Join("\n", messages.Select(m => $"{m.category}: {m.text}"));
            ShowNotification(new GUIContent(text));
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
                ShowNotification(new GUIContent("再生可能な音声データがありません"));
                return;
            }

            int sampleCount = pcm.samples.Length / pcm.channels;
            var clip = AudioClip.Create("preview", sampleCount, pcm.channels, pcm.sampleRate, false);
            clip.SetData(pcm.samples, 0);
            AudioPreviewPlayer.PlayClip(clip);
        }
    }
}

