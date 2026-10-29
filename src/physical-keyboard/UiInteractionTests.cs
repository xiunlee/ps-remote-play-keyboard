using System;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;

namespace PSKeyboard
{
    // Pure managed message-dispatch regressions. No Show(), keyboard hook,
    // SendInput, device/relay access or profile file I/O is used by these tests.
    internal static class UiInteractionTests
    {
        private static int checks;
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [STAThread]
        private static int Main()
        {
            try
            {
                UiText.Initialize(CultureInfo.GetCultureInfo("en-US"));
                VerifyStableActionRows();
                VerifyHeldCaptureKey(Keys.Escape);
                VerifyHeldCaptureKey(Keys.Enter);
                VerifyHeldCaptureKey(Keys.Space);
                VerifyRejectedKeys();
                VerifyPerActionRepeat();
                Console.WriteLine("UI interaction tests PASS (" + checks + " checks). No live input or devices used.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex.ToString()); return 1; }
        }

        private static void VerifyStableActionRows()
        {
            using (BindingEditor editor = new BindingEditor(BindingProfile.Default()))
            {
                DataGridView grid = Field<DataGridView>(editor, "grid");
                Check(grid.Rows.Count == BindingProfile.ActionCount, "All actions must be editable.");
                foreach (DataGridViewColumn column in grid.Columns)
                    Check(column.SortMode == DataGridViewColumnSortMode.NotSortable,
                        "Sorting changes row indices and could assign a key to the wrong controller action.");
                for (int i = 0; i < BindingProfile.ActionCount; i++)
                    Check((string)grid.Rows[i].Cells[0].Value == UiText.ActionName(i), "Native action order must stay stable.");
                Check(!editor.Visible, "Tests must never show the editor.");
            }
        }

        private static void VerifyHeldCaptureKey(Keys key)
        {
            BindingProfile original = BindingProfile.Default();
            for (int action = 0; action < BindingProfile.ActionCount; action++)
                if (original.KeyForAction(action) == (int)key) original = original.WithBinding(action, 0);
            using (BindingEditor editor = new BindingEditor(original))
            {
                SelectAction(editor, 0);
                SetField(editor, "capturing", true);
                Check(DispatchCommand(editor, key, false), "First capture keydown must be consumed.");
                Check(editor.Result.KeyForAction(0) == (int)key, "Captured key must be assigned.");
                Check(original.KeyForAction(0) == (int)Keys.W, "Draft edits must not mutate the active profile.");
                for (int i = 0; i < 3; i++)
                {
                    Check(DispatchCommand(editor, key, true), "Held key repeats must remain consumed after capture ends.");
                    Check(editor.DialogResult == DialogResult.None, "Held Escape/Enter must not close the editor.");
                    Check(!Field<bool>(editor, "capturing"), "Held Space/Enter must not reopen capture.");
                }
                KeyEventArgs release = new KeyEventArgs(key);
                typeof(BindingEditor).GetMethod("OnKeyUp", PrivateInstance).Invoke(editor, new object[] { release });
                Check(release.Handled && release.SuppressKeyPress, "Captured key release must not activate a button.");
                Check(Field<int>(editor, "consumedKey") == 0, "Release must clear consumed-key state.");
                Check(!editor.Visible, "Message-dispatch tests must stay unshown.");
            }
        }

        private static void VerifyRejectedKeys()
        {
            BindingProfile original = BindingProfile.Default();
            using (BindingEditor editor = new BindingEditor(original))
            {
                SelectAction(editor, 0); SetField(editor, "capturing", true);
                Check(DispatchCommand(editor, (Keys)original.KeyForAction(8), false), "A conflicting key must be consumed in capture mode.");
                Check(Object.ReferenceEquals(editor.Result, original), "A conflicting key must keep the draft unchanged.");
                Check(Field<Label>(editor, "hint").Text == UiText.T("BindingConflict"), "Conflict feedback must be shown.");
                editor.ReservedHotkeyPressed();
                Check(Field<Label>(editor, "hint").Text == UiText.T("BindingInvalid"), "F8 must show reserved-key feedback.");
                Check(Object.ReferenceEquals(editor.Result, original), "F8 must not change the profile.");
                Check(DispatchCommand(editor, Keys.Control | Keys.B, false), "Modified combinations must be consumed while editing.");
                Check(Object.ReferenceEquals(editor.Result, original), "Unsupported combinations must keep the draft unchanged.");
            }
        }

        private static void VerifyPerActionRepeat()
        {
            BindingProfile original = BindingProfile.Default();
            using (BindingEditor editor = new BindingEditor(original))
            {
                ComboBox mode = Field<ComboBox>(editor, "repeatMode");
                NumericUpDown interval = Field<NumericUpDown>(editor, "repeatInterval");
                DataGridView grid = Field<DataGridView>(editor, "grid");
                Check(mode.Items.Count == 3, "Exactly Off, Hold and Toggle modes must be offered.");
                Check(interval.Minimum == 20 && interval.Maximum == 5000, "Repeat interval must use the model bounds.");
                SelectAction(editor, 8); mode.SelectedIndex = (int)RapidFireMode.Hold; interval.Value = 20;
                Check(editor.Result.RapidFireModeForAction(8) == RapidFireMode.Hold, "Selected action must use Hold mode.");
                Check(editor.Result.IntervalMillisecondsForAction(8) == 20, "Minimum interval must be accepted.");
                Check(editor.Result.RapidFireModeForAction(9) == RapidFireMode.Off, "An adjacent action must stay unchanged.");
                BindingProfile beforeSelection = editor.Result;
                SelectAction(editor, 9);
                Check(Object.ReferenceEquals(beforeSelection, editor.Result), "Changing rows must not write displayed settings into another action.");
                Check(mode.SelectedIndex == (int)RapidFireMode.Off && interval.Value == 100,
                    "Selected action controls must load that action's own defaults.");
                mode.SelectedIndex = (int)RapidFireMode.Toggle; interval.Value = 5000;
                Check(editor.Result.RapidFireModeForAction(9) == RapidFireMode.Toggle, "Another action can use independent Toggle mode.");
                Check(editor.Result.IntervalMillisecondsForAction(9) == 5000, "Maximum interval must be accepted.");
                SelectAction(editor, 8);
                Check(mode.SelectedIndex == (int)RapidFireMode.Hold && interval.Value == 20, "Returning to an action must restore its own settings.");
                mode.SelectedIndex = (int)RapidFireMode.Off;
                Check(editor.Result.IntervalMillisecondsForAction(8) == 20, "Turning repeat off must preserve its chosen interval.");
                SetField(editor, "capturing", true); mode.SelectedIndex = (int)RapidFireMode.Hold;
                Check(!Field<bool>(editor, "capturing"), "Editing repeat settings must leave key capture mode.");
                typeof(BindingEditor).GetMethod("SetBinding", PrivateInstance).Invoke(editor, new object[] { (int)Keys.V });
                Check(editor.Result.KeyForAction(8) == (int)Keys.V, "Remapping must still change the chosen key.");
                Check(editor.Result.RapidFireModeForAction(8) == RapidFireMode.Hold && editor.Result.IntervalMillisecondsForAction(8) == 20,
                    "Remapping a key must preserve that action's repeat settings.");
                Check((string)grid.Rows[8].Cells[2].Value == UiText.T("RepeatHold") && (int)grid.Rows[8].Cells[3].Value == 20,
                    "Grid summary must reflect the selected repeat settings.");
                Check(original.KeyForAction(8) == (int)Keys.Down && original.RapidFireModeForAction(8) == RapidFireMode.Off,
                    "Editor changes must not mutate the live profile before Save.");
                typeof(BindingEditor).GetMethod("ResetDraft", PrivateInstance).Invoke(editor, new object[0]);
                for (int action = 0; action < BindingProfile.ActionCount; action++)
                    Check(editor.Result.RapidFireModeForAction(action) == RapidFireMode.Off &&
                        editor.Result.IntervalMillisecondsForAction(action) == 100 &&
                        editor.Result.KeyForAction(action) == original.KeyForAction(action),
                        "Restore defaults must reset keys and repeat settings together.");
                Check(mode.SelectedIndex == (int)RapidFireMode.Off && interval.Value == 100, "Reset must refresh selected-action controls.");
                SetField(editor, "capturing", true);
                typeof(Control).GetMethod("OnEnter", PrivateInstance).Invoke(interval, new object[] { EventArgs.Empty });
                Check(!Field<bool>(editor, "capturing"), "Entering the interval field must cancel key capture before typing numbers.");
                SetField(editor, "capturing", true);
                typeof(Control).GetMethod("OnEnter", PrivateInstance).Invoke(mode, new object[] { EventArgs.Empty });
                Check(!Field<bool>(editor, "capturing"), "Entering the mode list must cancel key capture before using arrows.");
                Check(!editor.Visible, "Repeat configuration tests must remain unshown.");
            }
        }

        private static void SelectAction(BindingEditor editor, int action)
        {
            DataGridView grid = Field<DataGridView>(editor, "grid");
            grid.CurrentCell = grid.Rows[action].Cells[1];
        }
        private static bool DispatchCommand(BindingEditor editor, Keys key, bool repeat)
        {
            Message message = Message.Create(IntPtr.Zero, 0x100, new IntPtr((int)(key & Keys.KeyCode)),
                new IntPtr(repeat ? 0x40000001 : 1));
            object[] arguments = { message, key };
            return (bool)typeof(BindingEditor).GetMethod("ProcessCmdKey", PrivateInstance).Invoke(editor, arguments);
        }
        private static T Field<T>(BindingEditor editor, string name)
        { return (T)typeof(BindingEditor).GetField(name, PrivateInstance).GetValue(editor); }
        private static void SetField(BindingEditor editor, string name, object value)
        { typeof(BindingEditor).GetField(name, PrivateInstance).SetValue(editor, value); }
        private static void Check(bool condition, string message)
        { checks++; if (!condition) throw new InvalidOperationException(message); }
    }
}
