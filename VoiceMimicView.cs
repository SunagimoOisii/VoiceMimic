#if UNITY_EDITOR
namespace VoiceMimic
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;

    public sealed class VoiceMimicWindow : EditorWindow, IVoiceMimicView
    {
        [Serializable]
        private class SectionRow
        {
            public AudioClip clip;
            public int startSample;
            public int endSample;
            public float pitchSemitone;
            public int fineCent;
            public int fadeMs = 40;
        }

        private List<SectionRow> rows = new List<SectionRow>();

        private ListView listView;
        private IntegerField sampleRateField;
        private IntegerField seedField;
        private Toggle seedUseToggle;
        private ProgressBar progressBar;
        private Label statusLabel;

        private AudioSource previewSource;
        private VoiceMimicPresenter presenter;

        public event Action OnExportRequested;
        public event Action OnPlayRequested;
        public event Action OnStopRequested;
        public event Action OnSaveRequested;

        [MenuItem("Tools/VoiceMimic")] private static void Open()
        {
            var w = GetWindow<VoiceMimicWindow>("VoiceMimic");
            w.minSize = new Vector2(820, 460);
            w.Show();
        }

        private void OnEnable()
        {
            ConstructUI();
            presenter = new VoiceMimicPresenter(this, new AudioSequenceEngine());
        }

        private void ConstructUI()
        {
            rootVisualElement.Clear();

            var toolbar = new Toolbar();
            var btnSave = new ToolbarButton(() => OnSaveRequested?.Invoke()) { text = "保存" };
            var btnExport = new ToolbarButton(() => OnExportRequested?.Invoke()) { text = "書き出し" };
            var btnPlay = new ToolbarButton(() => OnPlayRequested?.Invoke()) { text = "再生" };
            var btnStop = new ToolbarButton(() => OnStopRequested?.Invoke()) { text = "停止" };
            toolbar.Add(btnSave);
            toolbar.Add(btnExport);
            toolbar.Add(btnPlay);
            toolbar.Add(btnStop);
            rootVisualElement.Add(toolbar);

            // ここが画面分割の原因 → TwoPaneSplitView を削除し, 単一レイアウトに変更可能
            var container = new VisualElement();
            container.style.paddingLeft = 6;
            container.style.paddingTop = 6;
            rootVisualElement.Add(container);

            var addRow = new Button(() => { rows.Add(new SectionRow()); listView.Rebuild(); }) { text = "行追加" };
            container.Add(addRow);

            listView = new ListView();
            listView.makeItem = MakeRow;
            listView.bindItem = BindRow;
            listView.itemsSource = rows;
            listView.selectionType = SelectionType.Single;
            listView.reorderable = false;
            listView.showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly;
            listView.fixedItemHeight = 86;
            container.Add(listView);

            sampleRateField = new IntegerField("出力サンプルレート") { value = 44100, isDelayed = true };
            container.Add(sampleRateField);

            seedUseToggle = new Toggle("シード固定");
            container.Add(seedUseToggle);
            seedField = new IntegerField("シード値") { value = 123, isDelayed = true };
            container.Add(seedField);

            progressBar = new ProgressBar { value = 0, title = "待機中" };
            progressBar.style.height = 18;
            progressBar.style.marginTop = 8;
            container.Add(progressBar);

            statusLabel = new Label("");
            container.Add(statusLabel);
        }

        private VisualElement MakeRow()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            var clipField = new ObjectField("Clip") { objectType = typeof(AudioClip), allowSceneObjects = false };
            row.Add(clipField);

            var startField = new IntegerField("Start");
            row.Add(startField);

            var endField = new IntegerField("End");
            row.Add(endField);

            var pitchField = new FloatField("Semi");
            row.Add(pitchField);

            var centField = new IntegerField("Cent");
            row.Add(centField);

            var fadeField = new IntegerField("FadeMs") { value = 40 };
            row.Add(fadeField);

            var del = new Button() { text = "削除" };
            row.Add(del);

            row.userData = new VisualElement[] { clipField, startField, endField, pitchField, centField, fadeField, del };
            return row;
        }

        private void BindRow(VisualElement row, int index)
        {
            var arr = (VisualElement[])row.userData;
            var clipField = (ObjectField)arr[0];
            var startField = (IntegerField)arr[1];
            var endField = (IntegerField)arr[2];
            var pitchField = (FloatField)arr[3];
            var centField = (IntegerField)arr[4];
            var fadeField = (IntegerField)arr[5];
            var delBtn = (Button)arr[6];

            var data = rows[index];
            clipField.value = data.clip;
            startField.value = data.startSample;
            endField.value = Math.Max(data.endSample, data.startSample + 1);
            pitchField.value = data.pitchSemitone;
            centField.value = data.fineCent;
            fadeField.value = data.fadeMs;

            clipField.RegisterValueChangedCallback(e => data.clip = e.newValue as AudioClip);
            startField.RegisterValueChangedCallback(e => data.startSample = Math.Max(0, e.newValue));
            endField.RegisterValueChangedCallback(e => data.endSample = Math.Max(0, e.newValue));
            pitchField.RegisterValueChangedCallback(e => data.pitchSemitone = e.newValue);
            centField.RegisterValueChangedCallback(e => data.fineCent = e.newValue);
            fadeField.RegisterValueChangedCallback(e => data.fadeMs = Math.Max(0, e.newValue));
            delBtn.clicked += () => { rows.RemoveAt(index); listView.Rebuild(); };
        }

        public SequenceSnapshot CaptureSnapshot()
        {
            var list = new List<Section>();
            foreach (var r in rows)
            {
                list.Add(new Section
                {
                    clipRef = r.clip,
                    startSample = r.startSample,
                    endSample = r.endSample,
                    pitchSemitone = r.pitchSemitone,
                    fineCent = r.fineCent,
                    fadeMs = r.fadeMs
                });
            }
            return new SequenceSnapshot
            {
                sections = list.ToArray(),
                sampleRate = Math.Max(8000, sampleRateField.value),
                mono = true,
                randomSeed = seedUseToggle.value ? seedField.value : null as int?
            };
        }

        public ExportTarget GetExportTarget()
        {
            var path = EditorUtility.SaveFilePanel("音声を書き出し", Application.dataPath, "VoiceMimic", "wav");
            if (string.IsNullOrEmpty(path)) return null;
            return new ExportTarget { path = path };
        }

        public void SetBusyState(string label, float progress01)
        {
            progressBar.value = Mathf.Clamp01(progress01);
            progressBar.title = label;
            statusLabel.text = label;
        }

        public void ClearBusyState()
        {
            progressBar.value = 0f;
            progressBar.title = "待機中";
            statusLabel.text = "";
        }

        public void ShowWarning(string message) => ShowNotification(new GUIContent(message));
        public void ShowError(string message) => EditorUtility.DisplayDialog("VoiceMimic エラー", message, "OK");
        public void ShowInfo(string message) => ShowNotification(new GUIContent(message));

        public void PreviewPlay(PcmBuffer pcm)
        {
            StopAllPreviewSources();
            var clip = AudioClip.Create("VoiceMimicPreview", pcm.samples.Length, 1, pcm.sampleRate, false);
            clip.SetData(pcm.samples, 0);

            var go = new GameObject("VoiceMimicPreviewPlayer", typeof(AudioSource));
            go.hideFlags = HideFlags.HideAndDontSave;
            previewSource = go.GetComponent<AudioSource>();
            previewSource.playOnAwake = false;
            previewSource.clip = clip;
            previewSource.volume = 1f;
            previewSource.loop = false;
            previewSource.Play();
        }

        public void PreviewStop()
        {
            StopAllPreviewSources();
        }

        private void StopAllPreviewSources()
        {
            if (previewSource != null)
            {
                if (previewSource.isPlaying) previewSource.Stop();
                if (previewSource.clip != null) DestroyImmediate(previewSource.clip);
                DestroyImmediate(previewSource.gameObject);
                previewSource = null;
            }
        }
    }
}
#endif
