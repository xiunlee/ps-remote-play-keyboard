using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace PSKeyboard
{
    internal static class KeyboardUi
    {
        internal static readonly Color Background = Color.FromArgb(255, 247, 250);
        internal static readonly Color Surface = Color.White;
        internal static readonly Color Ink = Color.FromArgb(41, 23, 34);
        internal static readonly Color Muted = Color.FromArgb(115, 89, 105);
        internal static readonly Color Border = Color.FromArgb(234, 219, 226);
        internal static readonly Color Accent = Color.FromArgb(214, 35, 85); // #D62355
        internal static readonly Color AccentSoft = Color.FromArgb(251, 232, 239);
        internal static readonly Color AccentDark = Color.FromArgb(170, 23, 65);
        internal static readonly Color Green = Color.FromArgb(28, 151, 105);
        internal static readonly Color Amber = Color.FromArgb(196, 131, 20);
        internal static readonly Color Red = Color.FromArgb(199, 57, 64);
        internal static readonly Color Gray = Color.FromArgb(151, 129, 141);

        internal static Font Font(float size, FontStyle style)
        {
            string tag = UiText.LanguageTag;
            string family = tag == "zh-Hant" ? "Microsoft JhengHei UI" :
                tag == "zh-Hans" ? "Microsoft YaHei UI" :
                tag == "ja" ? "Yu Gothic UI" : tag == "ko" ? "Malgun Gothic" : "Segoe UI";
            return new Font(family, size, style, GraphicsUnit.Point);
        }

        internal static Font CodeFont(float size, FontStyle style)
        { return new Font("Consolas", size, style, GraphicsUnit.Point); }

        // Resources are embedded in the branded build. Older research builds
        // may omit them; images returned here are independent of their stream.
        internal static Icon LoadAppIcon()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PSKeyboard.BrandIcon"))
            {
                if (stream == null) return null;
                using (Icon source = new Icon(stream)) return (Icon)source.Clone();
            }
        }

        internal static Image LoadBrandMark()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PSKeyboard.BrandMark"))
            {
                if (stream == null) return null;
                using (Image source = Image.FromStream(stream)) return new Bitmap(source);
            }
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
            button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(226, 45, 97) : AccentSoft;
            button.FlatAppearance.MouseDownBackColor = primary ? AccentDark : Color.FromArgb(245, 215, 226);
            button.Font = Font(10, FontStyle.Bold); button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false; button.Margin = new Padding(0, 0, 12, 0);
            ApplyButtonColors(button, primary);
            button.EnabledChanged += delegate { ApplyButtonColors(button, primary); };
        }

        private static void ApplyButtonColors(Button button, bool primary)
        {
            button.BackColor = !button.Enabled ? Color.FromArgb(240, 229, 235) : primary ? Accent : Surface;
            button.ForeColor = !button.Enabled ? Color.FromArgb(137, 114, 127) : primary ? Color.White : Ink;
            button.FlatAppearance.BorderColor = !button.Enabled ? Border : primary ? Accent : Border;
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
