using System;
using System.Drawing;
using System.Windows.Forms;

namespace PSKeyboard
{
    internal sealed class BindingEditor : Form
    {
        private BindingProfile draft;
        private readonly bool renderingPreview;
        private readonly DataGridView grid = new DataGridView();
        private readonly Button capture = new Button();
        private readonly Label hint = new Label();
        private readonly ComboBox repeatMode = new ComboBox();
        private readonly NumericUpDown repeatInterval = new NumericUpDown();
        private readonly Label repeatLabel = new Label();
        private bool capturing, updatingRepeatControls;
        private int consumedKey;
        internal BindingProfile Result { get { return draft; } }

        internal BindingEditor(BindingProfile current) : this(current, false) { }

        internal BindingEditor(BindingProfile current, bool preview)
        {
            renderingPreview = preview;
            draft = current;
            Text = UiText.T("BindingsTitle"); Font = KeyboardUi.Font(10, FontStyle.Regular);
            ForeColor = KeyboardUi.Ink; BackColor = KeyboardUi.Background;
            AutoScaleMode = AutoScaleMode.Dpi; ClientSize = new Size(840, 820);
            MinimumSize = new Size(800, 720); StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false; MinimizeBox = false; MaximizeBox = false; KeyPreview = true;

            TableLayoutPanel root = KeyboardUi.Table(1);
            root.Padding = new Padding(24); root.RowCount = 6;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            Controls.Add(root);

            TableLayoutPanel heading = KeyboardUi.Table(1); heading.RowCount = 2;
            heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Label title = KeyboardUi.Label(UiText.T("BindingsTitle"), 20, KeyboardUi.Ink);
            title.Font = KeyboardUi.Font(20, FontStyle.Bold); title.Dock = DockStyle.Fill;
            Label subtitle = KeyboardUi.Label(UiText.T("BindingsSubtitle"), 10, KeyboardUi.Muted);
            subtitle.Dock = DockStyle.Fill;
            heading.Controls.Add(title, 0, 0); heading.Controls.Add(subtitle, 0, 1); root.Controls.Add(heading, 0, 0);

            grid.Dock = DockStyle.Fill; grid.Margin = Padding.Empty;
            grid.BackgroundColor = Color.White; grid.BorderStyle = BorderStyle.None;
            grid.AllowUserToAddRows = grid.AllowUserToDeleteRows = grid.AllowUserToResizeRows = false;
            grid.AllowUserToOrderColumns = false; grid.MultiSelect = false;
            grid.ReadOnly = true; grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = KeyboardUi.Border; grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = 36;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 240, 248);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = KeyboardUi.Muted;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(235, 240, 248);
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = KeyboardUi.Muted;
            grid.ColumnHeadersDefaultCellStyle.Font = KeyboardUi.Font(9, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(12, 0, 0, 0);
            grid.DefaultCellStyle.Padding = new Padding(12, 0, 0, 0);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(230, 237, 255);
            grid.DefaultCellStyle.SelectionForeColor = KeyboardUi.Accent;
            grid.RowTemplate.Height = 29;
            grid.Columns.Add("action", UiText.T("ActionColumn"));
            grid.Columns.Add("key", UiText.T("KeyColumn"));
            grid.Columns.Add("repeat", UiText.T("RepeatColumn"));
            grid.Columns.Add("interval", UiText.T("RepeatIntervalLabel"));
            grid.Columns[0].FillWeight = 36; grid.Columns[1].FillWeight = 21;
            grid.Columns[2].FillWeight = 27; grid.Columns[3].FillWeight = 16;
            grid.Columns[2].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.RowTemplate.MinimumHeight = 29; grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            foreach (DataGridViewColumn column in grid.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
            for (int i = 0; i < BindingProfile.ActionCount; i++)
                grid.Rows.Add(UiText.ActionName(i), UiText.KeyName(draft.KeyForAction(i)),
                    RepeatModeName(draft.RapidFireModeForAction(i)), draft.IntervalMillisecondsForAction(i));
            grid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) BeginCapture(); };
            // SelectionChanged can run before CurrentCell points at the new
            // action. Load only after that pointer has changed.
            grid.CurrentCellChanged += delegate { if (capturing) EndCapture(); LoadRepeatSettings(); };
            root.Controls.Add(grid, 0, 1);

            FlowLayoutPanel commands = new FlowLayoutPanel { Dock = DockStyle.Fill,
                Padding = new Padding(0, 12, 0, 0), Margin = Padding.Empty, WrapContents = false };
            KeyboardUi.Button(capture, UiText.T("CaptureKey"), false); capture.Click += delegate { BeginCapture(); };
            Button clear = new Button(), reset = new Button();
            KeyboardUi.Button(clear, UiText.T("ClearBinding"), false);
            KeyboardUi.Button(reset, UiText.T("RestoreDefaults"), false);
            clear.Click += delegate { SetBinding(0); };
            reset.Click += delegate { ResetDraft(); };
            commands.Controls.AddRange(new Control[] { capture, clear, reset }); root.Controls.Add(commands, 0, 2);

            TableLayoutPanel repetition = KeyboardUi.Table(2); repetition.BackColor = Color.White;
            repetition.Padding = new Padding(14, 8, 14, 8); repetition.RowCount = 3;
            repetition.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66));
            repetition.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            repetition.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            repetition.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            repetition.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            repeatLabel.Dock = DockStyle.Fill; repeatLabel.ForeColor = KeyboardUi.Muted;
            repeatLabel.Font = KeyboardUi.Font(9, FontStyle.Regular); repeatLabel.Margin = Padding.Empty;
            Label intervalLabel = KeyboardUi.Label(UiText.T("RepeatIntervalLabel"), 9, KeyboardUi.Muted);
            intervalLabel.Dock = DockStyle.Fill;
            repetition.Controls.Add(repeatLabel, 0, 0); repetition.Controls.Add(intervalLabel, 1, 0);
            repeatMode.DropDownStyle = ComboBoxStyle.DropDownList;
            repeatMode.Items.AddRange(new object[] { UiText.T("RepeatOff"), UiText.T("RepeatHold"), UiText.T("RepeatToggle") });
            repeatMode.Dock = DockStyle.Fill; repeatMode.Margin = new Padding(0, 0, 16, 0);
            repeatInterval.Minimum = BindingProfile.MinRapidFireIntervalMilliseconds;
            repeatInterval.Maximum = BindingProfile.MaxRapidFireIntervalMilliseconds;
            repeatInterval.Value = BindingProfile.DefaultRapidFireIntervalMilliseconds;
            repeatInterval.Increment = 10; repeatInterval.Dock = DockStyle.Fill; repeatInterval.Margin = Padding.Empty;
            repeatMode.SelectedIndexChanged += delegate { ApplyRepeatSettings(); };
            repeatInterval.ValueChanged += delegate { ApplyRepeatSettings(); };
            repeatMode.Enter += delegate { if (capturing) EndCapture(); };
            repeatInterval.Enter += delegate { if (capturing) EndCapture(); };
            repetition.Controls.Add(repeatMode, 0, 1); repetition.Controls.Add(repeatInterval, 1, 1);
            Label repeatHint = KeyboardUi.Label(UiText.T("RepeatHint"), 9, KeyboardUi.Muted); repeatHint.Dock = DockStyle.Fill;
            repetition.Controls.Add(repeatHint, 0, 2); repetition.SetColumnSpan(repeatHint, 2);
            root.Controls.Add(repetition, 0, 3);
            hint.Text = UiText.T("BindingsPausedHint"); hint.ForeColor = KeyboardUi.Muted;
            hint.Dock = DockStyle.Fill; hint.TextAlign = ContentAlignment.MiddleLeft; hint.Margin = Padding.Empty;
            root.Controls.Add(hint, 0, 4);

            FlowLayoutPanel footer = new FlowLayoutPanel { Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft, Margin = Padding.Empty, WrapContents = false };
            Button save = new Button(), cancel = new Button();
            KeyboardUi.Button(save, UiText.T("Save"), true); KeyboardUi.Button(cancel, UiText.T("Cancel"), false);
            save.Margin = new Padding(12, 7, 0, 0); cancel.Margin = new Padding(0, 7, 0, 0);
            save.Click += delegate { ApplyRepeatSettings(); EndCapture(); DialogResult = DialogResult.OK; Close(); };
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            footer.Controls.AddRange(new Control[] { save, cancel }); root.Controls.Add(footer, 0, 5);
            CancelButton = cancel;
            if (grid.Rows.Count > 0) grid.CurrentCell = grid.Rows[0].Cells[1];
            LoadRepeatSettings();
        }

        protected override bool ShowWithoutActivation { get { return renderingPreview; } }

        private void BeginCapture()
        {
            if (grid.CurrentCell == null) return;
            capturing = true; capture.Text = UiText.T("CapturePrompt");
            hint.Text = UiText.T("CapturePrompt"); hint.ForeColor = KeyboardUi.Accent;
            capture.Focus();
        }
        private void EndCapture()
        {
            capturing = false; capture.Text = UiText.T("CaptureKey");
            hint.Text = UiText.T("BindingsPausedHint"); hint.ForeColor = KeyboardUi.Muted;
        }
        private void RefreshRows()
        {
            for (int i = 0; i < BindingProfile.ActionCount; i++)
            {
                grid.Rows[i].Cells[1].Value = UiText.KeyName(draft.KeyForAction(i));
                grid.Rows[i].Cells[2].Value = RepeatModeName(draft.RapidFireModeForAction(i));
                grid.Rows[i].Cells[3].Value = draft.IntervalMillisecondsForAction(i);
            }
            LoadRepeatSettings();
        }
        private static string RepeatModeName(RapidFireMode mode)
        {
            return UiText.T(mode == RapidFireMode.Hold ? "RepeatHold" : mode == RapidFireMode.Toggle ? "RepeatToggle" : "RepeatOff");
        }
        private void LoadRepeatSettings()
        {
            if (repeatMode.Items.Count == 0) return;
            updatingRepeatControls = true;
            try
            {
                bool selected = grid.CurrentCell != null;
                repeatMode.Enabled = selected; repeatInterval.Enabled = selected;
                if (!selected) return;
                int action = grid.CurrentCell.RowIndex;
                repeatLabel.Text = UiText.T("RepeatModeLabel") + " · " + UiText.ActionName(action);
                repeatMode.SelectedIndex = (int)draft.RapidFireModeForAction(action);
                repeatInterval.Value = draft.IntervalMillisecondsForAction(action);
            }
            finally { updatingRepeatControls = false; }
        }
        private void ApplyRepeatSettings()
        {
            if (updatingRepeatControls || grid.CurrentCell == null || repeatMode.SelectedIndex < 0) return;
            if (capturing) EndCapture();
            int action = grid.CurrentCell.RowIndex;
            draft = draft.WithRapidFire(action, (RapidFireMode)repeatMode.SelectedIndex, Decimal.ToInt32(repeatInterval.Value));
            grid.Rows[action].Cells[2].Value = RepeatModeName(draft.RapidFireModeForAction(action));
            grid.Rows[action].Cells[3].Value = draft.IntervalMillisecondsForAction(action);
        }
        private void ResetDraft()
        {
            draft = BindingProfile.Default(); EndCapture(); RefreshRows();
        }
        private void SetBinding(int key)
        {
            if (grid.CurrentCell == null) return;
            int index = grid.CurrentCell.RowIndex;
            if (!BindingProfile.IsAllowedKey(key)) { ShowBindingError(false); return; }
            uint action = draft.ActionForKey(key);
            if (key != 0 && action != 0 && draft.KeyForAction(index) != key) { ShowBindingError(true); return; }
            try { draft = draft.WithBinding(index, key); EndCapture(); RefreshRows(); }
            catch (ArgumentException) { ShowBindingError(false); }
        }
        private void ShowBindingError(bool conflict)
        {
            hint.Text = UiText.T(conflict ? "BindingConflict" : "BindingInvalid"); hint.ForeColor = KeyboardUi.Red;
        }
        internal void ReservedHotkeyPressed()
        {
            if (capturing) ShowBindingError(false);
        }
        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            int key = (int)(keyData & Keys.KeyCode);
            if (key == consumedKey && (message.LParam.ToInt64() & 0x40000000L) != 0) return true;
            if (!capturing) return base.ProcessCmdKey(ref message, keyData);
            consumedKey = key;
            if ((keyData & Keys.Modifiers) != Keys.None) { ShowBindingError(false); return true; }
            SetBinding(key); return true;
        }
        protected override void OnKeyUp(KeyEventArgs e)
        {
            if ((int)e.KeyCode == consumedKey) { consumedKey = 0; e.Handled = true; e.SuppressKeyPress = true; }
            base.OnKeyUp(e);
        }
    }
}
