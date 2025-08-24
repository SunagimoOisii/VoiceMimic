namespace VoiceMimic
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// VoiceMimic のエディタウィンドウ生成, 管理を担当
    /// </summary>
    public class VoiceMimicView : EditorWindow
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
        private const int CentMin  = -50;
        private const int CentMax  = 50;

        private VoiceMimicPresenter presenter;

        private readonly List<SectionData> sections = new();
        private ListView sectionListView;
        private int selectedIndex = -1;

        private ObjectField  clipField;
        private FloatField   startMsField;
        private FloatField   endMsField;
        private MinMaxSlider rangeSlider;
        private SliderInt    pitchSlider;
        private SliderInt    centSlider;
        private IntegerField pitchField;
        private IntegerField centField;
        private IntegerField fadeField;



        [MenuItem("Tools/VoiceMimic")]
        public static void ShowWindow()
        {
            var w = GetWindow<VoiceMimicView>();
            w.titleContent = new GUIContent("Voice Mimic");
        }

        private void OnEnable()
        {
            presenter = new VoiceMimicPresenter(new VoiceMimicModel(), this);

            //UI ToolKit で GUI 作成
            var root  = rootVisualElement;
            var split = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);
            root.Clear();
            root.Add(split);

            BuildLeftUI(split);
            BuildRightUI(split);

            UpdateDetail();
        }

        private void BuildLeftUI(TwoPaneSplitView split)
        {
            var leftPanel = new VisualElement();
            split.Add(leftPanel);

            var assetBar = new Toolbar();
            assetBar.Add(new ToolbarButton(() => presenter.HandleSaveToAsset()) { text = "設定保存" });
            assetBar.Add(new ToolbarButton(() => presenter.HandleLoadFromAsset()) { text = "設定読込" });
            leftPanel.Add(assetBar);

            //各種ボタン
            var bar = new Toolbar();
            bar.Add(new ToolbarButton(AddSection)    { text = "追加" });
            bar.Add(new ToolbarButton(RemoveSection) { text = "削除" });
            bar.Add(new ToolbarButton(() => presenter.HandlePlay())   { text = "再生" });
            bar.Add(new ToolbarButton(() => presenter.HandleExport()) { text = "書き出し" });
            leftPanel.Add(bar);

            //セクションリスト(リスト中の要素クリックでウィンドウ右に対応の内容を表示)
            sectionListView = new ListView();
            sectionListView.reorderable = true;
            sectionListView.itemsSource = sections;
            sectionListView.selectionType = SelectionType.Single;
            sectionListView.makeItem = () => new Label();
            sectionListView.bindItem = (e, i) =>
            {
                var label = (Label)e;
                var data  = sections[i];
                label.text = data.clip != null ? data.clip.name : "未設定";
            };
            sectionListView.selectionChanged += _ =>
            {
                selectedIndex = sectionListView.selectedIndex;
                UpdateDetail();
            };
            leftPanel.Add(sectionListView);
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

        private void BuildRightUI(TwoPaneSplitView split)
        {
            var rightPanel = new VisualElement();
            split.Add(rightPanel);

            //使用音声指定項目
            clipField    = new ObjectField("Clip") { objectType = typeof(AudioClip) };
            clipField.RegisterValueChangedCallback(e =>
            {
                var data = CurrentSection();
                var clip = e.newValue as AudioClip;

                if (data == null || clip == null) return;
                data.clip = clip;

                var lengthMs = clip.length * 1000f;
                data.startMs = 0f;
                data.endMs = lengthMs;
                rangeSlider.lowLimit  = 0f;
                rangeSlider.highLimit = lengthMs;
                rangeSlider.SetValueWithoutNotify(new Vector2(0f, lengthMs));
                startMsField.SetValueWithoutNotify(0f);
                endMsField.SetValueWithoutNotify(lengthMs);

                sectionListView.RefreshItems();
            });
            rightPanel.Add(clipField);

            //音声の使用区間指定フィールド, スライダー
            startMsField = new FloatField("開始(ms)");
            startMsField.RegisterValueChangedCallback(e =>
            {
                float v = Mathf.Clamp(e.newValue, rangeSlider.lowLimit, rangeSlider.maxValue);
                rangeSlider.minValue = v;
                var data = CurrentSection();
                if (data != null) data.startMs = v;
            });
            endMsField = new FloatField("終了(ms)");
            endMsField.RegisterValueChangedCallback(e =>
            {
                float v = Mathf.Clamp(e.newValue, rangeSlider.minValue, rangeSlider.highLimit);
                rangeSlider.maxValue = v;
                var data = CurrentSection();
                if (data != null) data.endMs = v;
            });
            rangeSlider = new MinMaxSlider("範囲(ms)", 0f, 0f, 0f, 0f);
            rangeSlider.RegisterValueChangedCallback(e =>
            {
                startMsField.SetValueWithoutNotify(e.newValue.x);
                endMsField.SetValueWithoutNotify(e.newValue.y);
                var data = CurrentSection();
                if (data != null)
                {
                    data.startMs = e.newValue.x;
                    data.endMs   = e.newValue.y;
                }
            });
            rightPanel.Add(startMsField);
            rightPanel.Add(endMsField);
            rightPanel.Add(rangeSlider);

            //ピッチ調整フィールド, スライダー
            var pitchContainer = new VisualElement();
            pitchContainer.style.flexDirection = FlexDirection.Row;
            pitchSlider                = new SliderInt("Pitch Semitone", PitchMin, PitchMax);
            pitchSlider.style.flexGrow = 1f;
            pitchSlider.RegisterValueChangedCallback(e =>
            {
                int v = Mathf.Clamp(e.newValue, PitchMin, PitchMax);
                pitchField.SetValueWithoutNotify(v);
                var data = CurrentSection();
                if (data != null) data.pitchSemitone = v;
            });
            pitchField             = new IntegerField();
            pitchField.style.width = 60f;
            pitchField.RegisterValueChangedCallback(e =>
            {
                int v = Mathf.Clamp(e.newValue, PitchMin, PitchMax);
                pitchField.SetValueWithoutNotify(v);
                pitchSlider.SetValueWithoutNotify(v);
                var data = CurrentSection();
                if (data != null) data.pitchSemitone = v;
            });
            pitchContainer.Add(pitchSlider);
            pitchContainer.Add(pitchField);
            rightPanel.Add(pitchContainer);
            
            //セント調整フィールド, スライダー
            var centContainer = new VisualElement();
            centContainer.style.flexDirection = FlexDirection.Row;
            centSlider                = new SliderInt("Fine Cent", CentMin, CentMax);
            centSlider.style.flexGrow = 1f;
            centSlider.RegisterValueChangedCallback(e =>
            {
                int v = Mathf.Clamp(e.newValue, CentMin, CentMax);
                centField.SetValueWithoutNotify(v);
                var data = CurrentSection();
                if (data != null) data.fineCent = v;
            });
            centField = new IntegerField();
            centField.style.width = 60f;
            centField.RegisterValueChangedCallback(e =>
            {
                int v = Mathf.Clamp(e.newValue, CentMin, CentMax);
                centField.SetValueWithoutNotify(v);
                centSlider.SetValueWithoutNotify(v);
                var data = CurrentSection();
                if (data != null) data.fineCent = v;
            });
            centContainer.Add(centSlider);
            centContainer.Add(centField);
            rightPanel.Add(centContainer);

            //フェード調整フィールド
            fadeField = new IntegerField("Fade Ms") { value = 40 };   
            fadeField.RegisterValueChangedCallback(e =>
            {
                var data = CurrentSection();
                if (data != null) data.fadeMs = e.newValue;
            });
            rightPanel.Add(fadeField);
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

            if (has == false) return;

            clipField.SetValueWithoutNotify(data.clip);
            float lengthMs = data.clip != null ? data.clip.length * 1000f : 0f;
            rangeSlider.lowLimit = 0f;
            rangeSlider.highLimit = lengthMs;
            rangeSlider.SetValueWithoutNotify(new Vector2(data.startMs, data.endMs));
            startMsField.SetValueWithoutNotify(data.startMs);
            endMsField.SetValueWithoutNotify(data.endMs);
            int ps = Mathf.Clamp(data.pitchSemitone, PitchMin, PitchMax);
            int fc = Mathf.Clamp(data.fineCent, CentMin, CentMax);
            data.fineCent      = fc;
            data.pitchSemitone = ps;
            pitchSlider.SetValueWithoutNotify(ps);
            pitchField.SetValueWithoutNotify(ps);
            centSlider.SetValueWithoutNotify(fc);
            centField.SetValueWithoutNotify(fc);
            fadeField.SetValueWithoutNotify(data.fadeMs);
        }

        private SectionData CurrentSection()
        {
            if (selectedIndex < 0 ||  selectedIndex >= sections.Count) return null;
            return sections[selectedIndex];
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

        public void Play(VoiceMimicModel.PcmBuffer pcm)
        {
            int sampleCount = pcm.samples.Length / pcm.channels;
            var clip = AudioClip.Create("preview", sampleCount, pcm.channels, pcm.sampleRate, false);
            clip.SetData(pcm.samples, 0);
            AudioPreviewPlayer.PlayClip(clip);
        }
    }
}

