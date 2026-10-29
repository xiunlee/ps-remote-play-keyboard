using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PSKeyboard
{
    internal static class KeyboardUi
    {
        internal static readonly Color Background = Color.FromArgb(245, 247, 250);
        internal static readonly Color Ink = Color.FromArgb(28, 39, 58);
        internal static readonly Color Muted = Color.FromArgb(100, 113, 132);
        internal static readonly Color Border = Color.FromArgb(224, 230, 237);
        internal static readonly Color Accent = Color.FromArgb(41, 88, 218);
        internal static readonly Color Green = Color.FromArgb(28, 151, 105);
        internal static readonly Color Amber = Color.FromArgb(196, 131, 20);
        internal static readonly Color Red = Color.FromArgb(199, 57, 64);
        internal static readonly Color Gray = Color.FromArgb(146, 157, 173);

        internal static Font Font(float size, FontStyle style)
        {
            string tag = UiText.LanguageTag;
            string family = tag == "zh-Hant" ? "Microsoft JhengHei UI" :
                tag == "zh-Hans" ? "Microsoft YaHei UI" :
                tag == "ja" ? "Yu Gothic UI" : tag == "ko" ? "Malgun Gothic" : "Segoe UI";
            return new Font(family, size, style, GraphicsUnit.Point);
        }

        internal static Label Label(string text, float size, Color color)
        {
            return new Label { Text = text, ForeColor = color, Font = Font(size, FontStyle.Regular),
                AutoSize = false, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty };
        }

        internal static void Button(Button button, string text, bool primary)
        {
            button.Text = text; button.AutoSize = true; button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            button.MinimumSize = new Size(130, 42); button.Padding = new Padding(16, 7, 16, 7);
            button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = primary ? 0 : 1;
            button.FlatAppearance.BorderColor = Border;
            button.BackColor = primary ? Accent : Color.White;
            button.ForeColor = primary ? Color.White : Ink;
            button.Font = Font(10, FontStyle.Bold); button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false; button.Margin = new Padding(0, 0, 12, 0);
        }

        internal static TableLayoutPanel Table(int columns)
        {
            return new TableLayoutPanel { ColumnCount = columns, RowCount = 1, Dock = DockStyle.Fill,
                Margin = Padding.Empty, Padding = Padding.Empty, BackColor = Color.Transparent };
        }
    }

    internal sealed class StatusDot : Control
    {
        private Color color = KeyboardUi.Gray;
        internal Color DotColor { get { return color; } set { if (color != value) { color = value; Invalidate(); } } }
        internal StatusDot()
        {
            Size = new Size(18, 18); Margin = new Padding(0, 9, 10, 0);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Brush outer = new SolidBrush(Color.FromArgb(24, color)))
                e.Graphics.FillEllipse(outer, 0, 0, Width - 1, Height - 1);
            using (Brush inner = new SolidBrush(color))
                e.Graphics.FillEllipse(inner, Width / 4, Height / 4, Width / 2, Height / 2);
        }
    }
}
