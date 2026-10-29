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
        private bool capturing;
        private int consumedKey;
        internal BindingProfile Result { get { return draft; } }

        internal BindingEditor(BindingProfile current) : this(current, false) { }

        internal BindingEditor(BindingProfile current, bool preview)
        {
            renderingPreview = preview;
            draft = current;
            Text = UiText.T("BindingsTitle"); Font = KeyboardUi.Font(10, FontStyle.Regular);
            ForeColor = KeyboardUi.Ink; BackColor = KeyboardUi.Background;
            AutoScaleMode = AutoScaleMode.Dpi; ClientSize = new Size(760, 760);
            MinimumSize = new Size(720, 600); StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false; MinimizeBox = false; MaximizeBox = false; KeyPreview = true;

            TableLayoutPanel root = KeyboardUi.Table(1);
            root.Padding = new Padding(24); root.RowCount = 5;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
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
            grid.ColumnHeadersDefaultCellStyle.Font = KeyboardUi.Font(9, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(12, 0, 0, 0);
            grid.DefaultCellStyle.Padding = new Padding(12, 0, 0, 0);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(230, 237, 255);
            grid.DefaultCellStyle.SelectionForeColor = KeyboardUi.Accent;
            grid.RowTemplate.Height = 29;
            grid.Columns.Add("action", UiText.T("ActionColumn"));
            grid.Columns.Add("key", UiText.T("KeyColumn"));
            grid.Columns[0].FillWeight = 64; grid.Columns[1].FillWeight = 36;
            grid.Columns[0].SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.Columns[1].SortMode = DataGridViewColumnSortMode.NotSortable;
            for (int i = 0; i < BindingProfile.ActionCount; i++)
                grid.Rows.Add(UiText.ActionName(i), UiText.KeyName(draft.KeyForAction(i)));
            grid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) BeginCapture(); };
            grid.SelectionChanged += delegate { if (capturing) EndCapture(); };
            root.Controls.Add(grid, 0, 1);

            FlowLayoutPanel commands = new FlowLayoutPanel { Dock = DockStyle.Fill,
                Padding = new Padding(0, 12, 0, 0), Margin = Padding.Empty, WrapContents = false };
            KeyboardUi.Button(capture, UiText.T("CaptureKey"), false); capture.Click += delegate { BeginCapture(); };
            Button clear = new Button(), reset = new Button();
            KeyboardUi.Button(clear, UiText.T("ClearBinding"), false);
            KeyboardUi.Button(reset, UiText.T("RestoreDefaults"), false);
            clear.Click += delegate { SetBinding(0); };
            reset.Click += delegate { draft = BindingProfile.Default(); EndCapture(); RefreshRows(); };
            commands.Controls.AddRange(new Control[] { capture, clear, reset }); root.Controls.Add(commands, 0, 2);
            hint.Text = UiText.T("BindingsPausedHint"); hint.ForeColor = KeyboardUi.Muted;
            hint.Dock = DockStyle.Fill; hint.TextAlign = ContentAlignment.MiddleLeft; hint.Margin = Padding.Empty;
            root.Controls.Add(hint, 0, 3);

            FlowLayoutPanel footer = new FlowLayoutPanel { Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft, Margin = Padding.Empty, WrapContents = false };
            Button save = new Button(), cancel = new Button();
            KeyboardUi.Button(save, UiText.T("Save"), true); KeyboardUi.Button(cancel, UiText.T("Cancel"), false);
            save.Margin = new Padding(12, 7, 0, 0); cancel.Margin = new Padding(0, 7, 0, 0);
            save.Click += delegate { EndCapture(); DialogResult = DialogResult.OK; Close(); };
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            footer.Controls.AddRange(new Control[] { save, cancel }); root.Controls.Add(footer, 0, 4);
            CancelButton = cancel;
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
            for (int i = 0; i < BindingProfile.ActionCount; i++) grid.Rows[i].Cells[1].Value = UiText.KeyName(draft.KeyForAction(i));
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
