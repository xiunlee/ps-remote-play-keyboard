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
            if (key == Keys.Enter) original = original.WithBinding(17, 0);
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
                Check(DispatchCommand(editor, Keys.K, false), "A conflicting key must be consumed in capture mode.");
                Check(Object.ReferenceEquals(editor.Result, original), "A conflicting key must keep the draft unchanged.");
                Check(Field<Label>(editor, "hint").Text == UiText.T("BindingConflict"), "Conflict feedback must be shown.");
                editor.ReservedHotkeyPressed();
                Check(Field<Label>(editor, "hint").Text == UiText.T("BindingInvalid"), "F8 must show reserved-key feedback.");
                Check(Object.ReferenceEquals(editor.Result, original), "F8 must not change the profile.");
                Check(DispatchCommand(editor, Keys.Control | Keys.B, false), "Modified combinations must be consumed while editing.");
                Check(Object.ReferenceEquals(editor.Result, original), "Unsupported combinations must keep the draft unchanged.");
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
