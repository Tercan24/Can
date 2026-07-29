using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TercanOptimizer
{
    internal static class ModernScrollChrome
    {
        private const int SbBoth = 3;
        private const int WsHScroll = 0x00100000;
        private const int WsVScroll = 0x00200000;
        private const int WmStyleChanging = 0x007C;
        private const int GwlStyle = -16;

        [DllImport("user32.dll")]
        private static extern bool ShowScrollBar(IntPtr window, int bar, bool show);

        [StructLayout(LayoutKind.Sequential)]
        private struct StyleStruct
        {
            public int StyleOld;
            public int StyleNew;
        }

        public static void HideNativeBars(ScrollableControl control)
        {
            if (control == null || control.IsDisposed || !control.IsHandleCreated) return;
            ShowScrollBar(control.Handle, SbBoth, false);
        }

        public static void HideNativeBarsLater(ScrollableControl control)
        {
            if (control == null || control.IsDisposed || !control.IsHandleCreated) return;
            try
            {
                control.BeginInvoke(new Action(delegate { HideNativeBars(control); }));
            }
            catch
            {
            }
        }

        public static int RemoveNativeStyles(int style)
        {
            return style & ~WsHScroll & ~WsVScroll;
        }

        public static void SuppressNativeStyles(ref Message message)
        {
            if (message.Msg != WmStyleChanging ||
                message.WParam.ToInt32() != GwlStyle ||
                message.LParam == IntPtr.Zero)
            {
                return;
            }

            StyleStruct styles = (StyleStruct)Marshal.PtrToStructure(
                message.LParam,
                typeof(StyleStruct));
            styles.StyleNew = RemoveNativeStyles(styles.StyleNew);
            Marshal.StructureToPtr(styles, message.LParam, false);
        }

        public static bool IsScrollChromeMessage(int message)
        {
            return message == 0x0005 ||
                   message == 0x000F ||
                   message == 0x007C ||
                   message == 0x007D ||
                   message == 0x0083 ||
                   message == 0x0085 ||
                   message == 0x0114 ||
                   message == 0x0115 ||
                   message == 0x020A ||
                   message == 0x020E;
        }
    }

    internal sealed class ModernScrollFlowPanel : FlowLayoutPanel
    {
        public ModernScrollFlowPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.Style = ModernScrollChrome.RemoveNativeStyles(parameters.Style);
                return parameters;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ModernScrollChrome.HideNativeBars(this);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            ModernScrollChrome.HideNativeBars(this);
            ModernScrollChrome.HideNativeBarsLater(this);
        }

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            ModernScrollChrome.HideNativeBars(this);
            ModernScrollChrome.HideNativeBarsLater(this);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ModernScrollChrome.HideNativeBars(this);
            base.OnMouseWheel(e);
            ModernScrollChrome.HideNativeBars(this);
            ModernScrollChrome.HideNativeBarsLater(this);
        }

        protected override void WndProc(ref Message message)
        {
            ModernScrollChrome.SuppressNativeStyles(ref message);
            bool suppress = ModernScrollChrome.IsScrollChromeMessage(message.Msg);
            if (suppress) ModernScrollChrome.HideNativeBars(this);
            base.WndProc(ref message);
            if (suppress) ModernScrollChrome.HideNativeBars(this);
        }
    }

    internal sealed class ModernScrollPanel : Panel
    {
        public ModernScrollPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.Style = ModernScrollChrome.RemoveNativeStyles(parameters.Style);
                return parameters;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ModernScrollChrome.HideNativeBars(this);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            ModernScrollChrome.HideNativeBars(this);
            ModernScrollChrome.HideNativeBarsLater(this);
        }

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            ModernScrollChrome.HideNativeBars(this);
            ModernScrollChrome.HideNativeBarsLater(this);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ModernScrollChrome.HideNativeBars(this);
            base.OnMouseWheel(e);
            ModernScrollChrome.HideNativeBars(this);
            ModernScrollChrome.HideNativeBarsLater(this);
        }

        protected override void WndProc(ref Message message)
        {
            ModernScrollChrome.SuppressNativeStyles(ref message);
            bool suppress = ModernScrollChrome.IsScrollChromeMessage(message.Msg);
            if (suppress) ModernScrollChrome.HideNativeBars(this);
            base.WndProc(ref message);
            if (suppress) ModernScrollChrome.HideNativeBars(this);
        }
    }

    internal static class UiMotion
    {
        public static bool AnimationsEnabled = true;

        public static Color Blend(Color from, Color to, float amount)
        {
            amount = Math.Max(0f, Math.Min(1f, amount));
            return Color.FromArgb(
                (int)(from.A + (to.A - from.A) * amount),
                (int)(from.R + (to.R - from.R) * amount),
                (int)(from.G + (to.G - from.G) * amount),
                (int)(from.B + (to.B - from.B) * amount));
        }
    }

    internal static class BrandAssets
    {
        private const string LogoResource = "TercanOptimizer.Assets.tercan-brand-256.png";
        private const string IconResource = "TercanOptimizer.Assets.tercan.ico";

        public static Bitmap LoadLogo()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(LogoResource))
            {
                if (stream == null) return null;
                using (Image image = Image.FromStream(stream))
                {
                    return new Bitmap(image);
                }
            }
        }

        public static Icon LoadAppIcon()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(IconResource))
            {
                return stream == null ? null : new Icon(stream);
            }
        }

        public static PictureBox CreateLogoBox(int width, int height)
        {
            PictureBox picture = new BrandPictureBox();
            picture.Size = new Size(width, height);
            picture.BackColor = Color.Transparent;
            picture.SizeMode = PictureBoxSizeMode.Zoom;
            picture.Image = LoadLogo();
            return picture;
        }
    }

    internal static class SoftwareIconAssets
    {
        private static readonly Dictionary<string, string> ResourceKeys =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "RARLab.WinRAR", "winrar" },
                { "7zip.7zip", "7zip" },
                { "Google.Chrome", "chrome" },
                { "Mozilla.Firefox", "firefox" },
                { "Brave.Brave", "brave" },
                { "VideoLAN.VLC", "vlc" },
                { "Spotify.Spotify", "spotify" },
                { "Notepad++.Notepad++", "notepadplusplus" },
                { "voidtools.Everything", "everything" },
                { "Microsoft.PowerToys", "powertoys" },
                { "Valve.Steam", "steam" },
                { "EpicGames.EpicGamesLauncher", "epicgames" },
                { "Discord.Discord", "discord" },
                { "ElectronicArts.EADesktop", "ea" },
                { "Ubisoft.Connect", "ubisoft" },
                { "GOG.Galaxy", "gog" },
                { "OBSProject.OBSStudio", "obs" },
                { "Audacity.Audacity", "audacity" },
                { "ShareX.ShareX", "sharex" },
                { "qBittorrent.qBittorrent", "qbittorrent" }
            };

        private static readonly Dictionary<string, Image> Cache =
            new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        public static Image Load(string packageId)
        {
            string key;
            if (!ResourceKeys.TryGetValue(packageId ?? string.Empty, out key))
            {
                return CreateFallback(packageId);
            }

            Image cached;
            if (Cache.TryGetValue(key, out cached)) return cached;

            string resourceName = "TercanOptimizer.Assets.Software." + key + ".png";
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null) return CreateFallback(packageId);
                using (Image source = Image.FromStream(stream))
                {
                    cached = new Bitmap(source);
                }
            }
            Cache[key] = cached;
            return cached;
        }

        private static Image CreateFallback(string packageId)
        {
            Bitmap bitmap = new Bitmap(96, 96);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (SolidBrush fill = new SolidBrush(AppTheme.Accent))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            using (Font font = new Font("Segoe UI Semibold", 34f, FontStyle.Bold))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                graphics.FillEllipse(fill, 4, 4, 88, 88);
                string letter = string.IsNullOrWhiteSpace(packageId) ? "?" : packageId.Substring(0, 1).ToUpperInvariant();
                SizeF size = graphics.MeasureString(letter, font);
                graphics.DrawString(
                    letter,
                    font,
                    textBrush,
                    (96f - size.Width) / 2f,
                    (96f - size.Height) / 2f - 2f);
            }
            return bitmap;
        }
    }

    internal sealed class BrandPictureBox : PictureBox
    {
        private Timer glowTimer;
        private float glowPhase;

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!UiMotion.AnimationsEnabled || !Visible)
            {
                if (glowTimer != null) glowTimer.Stop();
                return;
            }
            if (glowTimer == null)
            {
                glowTimer = new Timer();
                glowTimer.Interval = 160;
                glowTimer.Tick += delegate
                {
                    Form owner = FindForm();
                    if (!Visible || owner == null || !owner.Visible || owner.WindowState == FormWindowState.Minimized)
                    {
                        glowTimer.Stop();
                        return;
                    }
                    glowPhase += 0.035f;
                    if (glowPhase >= 1f) glowPhase -= 1f;
                    Invalidate();
                };
            }
            glowTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int alpha = UiMotion.AnimationsEnabled
                ? 20 + (int)(10d * (0.5d + 0.5d * Math.Sin(glowPhase * Math.PI * 2d)))
                : 20;
            using (Pen glow = new Pen(Color.FromArgb(alpha, AppTheme.Cyan), 2f))
            {
                pe.Graphics.DrawEllipse(glow, 3, 3, Math.Max(1, Width - 7), Math.Max(1, Height - 7));
            }
            base.OnPaint(pe);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (glowTimer != null)
                {
                    glowTimer.Stop();
                    glowTimer.Dispose();
                    glowTimer = null;
                }
                if (Image != null)
                {
                    Image.Dispose();
                    Image = null;
                }
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class BrandSplashForm : Form
    {
        private Timer animationTimer;
        private System.Threading.Timer safetyCloseTimer;
        private int elapsed;
        private Panel progressLine;
        private PictureBox logo;

        public BrandSplashForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(620, 370);
            BackColor = AppTheme.Window;
            ForeColor = AppTheme.Text;
            ShowInTaskbar = false;
            TopMost = true;
            Opacity = 0.01d;
            DoubleBuffered = true;
            Icon = BrandAssets.LoadAppIcon();

            SmoothPanel surface = new SmoothPanel();
            surface.Dock = DockStyle.Fill;
            surface.BackColor = Color.FromArgb(10, 12, 23);
            surface.BorderColor = Color.FromArgb(120, AppTheme.Accent);
            Controls.Add(surface);

            Label eyebrow = UiFactory.Label(
                "WINDOWS OYUN VE PERFORMANS MERKEZİ",
                new Font("Segoe UI Semibold", 8f, FontStyle.Bold),
                AppTheme.Cyan);
            eyebrow.Location = new Point(176, 28);
            surface.Controls.Add(eyebrow);

            logo = BrandAssets.CreateLogoBox(158, 158);
            logo.Location = new Point(231, 59);
            surface.Controls.Add(logo);

            Label title = UiFactory.Label(
                "tercan.exe",
                new Font("Segoe UI Semibold", 26f, FontStyle.Bold),
                AppTheme.Text);
            title.Location = new Point(228, 225);
            surface.Controls.Add(title);
            Label tagline = UiFactory.Label(
                "Güvenli optimizasyon • Daha akıcı oyun • Kontrol sizde",
                new Font("Segoe UI", 9.5f),
                AppTheme.TextMuted);
            tagline.Location = new Point(146, 282);
            surface.Controls.Add(tagline);
            Label loading = UiFactory.Label(
                "Sistem merkezi hazırlanıyor…",
                new Font("Segoe UI Semibold", 8f, FontStyle.Bold),
                AppTheme.Accent);
            loading.Location = new Point(227, 318);
            surface.Controls.Add(loading);

            Panel track = new Panel();
            track.Location = new Point(80, 348);
            track.Size = new Size(460, 3);
            track.BackColor = Color.FromArgb(44, 47, 65);
            surface.Controls.Add(track);
            progressLine = new Panel();
            progressLine.Location = Point.Empty;
            progressLine.Size = new Size(1, 3);
            progressLine.BackColor = AppTheme.Cyan;
            track.Controls.Add(progressLine);

            animationTimer = new Timer();
            animationTimer.Interval = 20;
            animationTimer.Tick += AnimateSplash;
            animationTimer.Start();
            safetyCloseTimer = new System.Threading.Timer(
                delegate
                {
                    try
                    {
                        if (IsDisposed || !IsHandleCreated) return;
                        BeginInvoke(new Action(delegate
                        {
                            if (!IsDisposed) Close();
                        }));
                    }
                    catch
                    {
                    }
                },
                null,
                2300,
                System.Threading.Timeout.Infinite);

            Shown += delegate
            {
                Region = new Region(SmoothPanel.RoundedRect(ClientRectangle, 18));
                if (!animationTimer.Enabled) animationTimer.Start();
            };
        }

        private void AnimateSplash(object sender, EventArgs e)
        {
            elapsed += animationTimer.Interval;
            if (elapsed <= 220)
            {
                Opacity = Math.Min(1d, elapsed / 220d);
            }
            else if (elapsed >= 1120)
            {
                Opacity = Math.Max(0d, 1d - (elapsed - 1120d) / 260d);
            }
            progressLine.Width = Math.Max(1, Math.Min(460, (int)(460d * elapsed / 1380d)));
            if (elapsed < 1380) return;
            animationTimer.Stop();
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (animationTimer != null)
                {
                    animationTimer.Stop();
                    animationTimer.Dispose();
                    animationTimer = null;
                }
                if (safetyCloseTimer != null)
                {
                    safetyCloseTimer.Dispose();
                    safetyCloseTimer = null;
                }
                if (logo != null && logo.Image != null)
                {
                    logo.Image.Dispose();
                    logo.Image = null;
                }
            }
            base.Dispose(disposing);
        }
    }

    internal static class UiFactory
    {
        public static Label Label(string text, Font font, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.AutoSize = true;
            return label;
        }

        public static Button Button(string text, Color backColor, Color foreColor)
        {
            Button button = new ModernActionButton();
            button.Text = text;
            button.Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);
            button.BackColor = backColor;
            button.ForeColor = foreColor;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Cursor = Cursors.Hand;
            button.Height = 38;
            button.Padding = new Padding(12, 0, 12, 0);
            button.AutoSize = true;
            button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            return button;
        }

        public static Label Pill(string text, Color color)
        {
            Label label = new Label();
            label.Text = "  " + text + "  ";
            label.Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold);
            label.ForeColor = color;
            label.BackColor = Color.FromArgb(
                Math.Min(255, color.R / 5 + 25),
                Math.Min(255, color.G / 5 + 25),
                Math.Min(255, color.B / 5 + 25));
            label.AutoSize = true;
            label.Padding = new Padding(2, 3, 2, 3);
            label.Margin = new Padding(0, 0, 8, 0);
            return label;
        }

        public static Color RiskColor(RiskLevel risk)
        {
            if (risk == RiskLevel.Experimental) return AppTheme.Red;
            if (risk == RiskLevel.Caution) return AppTheme.Amber;
            return AppTheme.Green;
        }

        public static string RiskText(RiskLevel risk)
        {
            if (risk == RiskLevel.Experimental) return "DENEYSEL";
            if (risk == RiskLevel.Caution) return "TEMKİNLİ";
            return "GÜVENLİ";
        }

        public static string ImpactText(ImpactLevel impact)
        {
            if (impact == ImpactLevel.High) return "YÜKSEK ETKİ";
            if (impact == ImpactLevel.Medium) return "ORTA ETKİ";
            return "DÜŞÜK ETKİ";
        }
    }

    internal sealed class ModernActionButton : Button
    {
        private Timer motionTimer;
        private float hoverAmount;
        private bool pointerInside;
        private bool pointerDown;
        private float shinePhase;
        private Point rippleOrigin;
        private float rippleRadius;
        private float rippleAlpha;

        public ModernActionButton()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor,
                true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            pointerInside = true;
            StartMotion();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            pointerInside = false;
            pointerDown = false;
            StartMotion();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            pointerDown = true;
            rippleOrigin = mevent.Location;
            rippleRadius = 0f;
            rippleAlpha = 1f;
            StartMotion();
            Invalidate();
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            pointerDown = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        private void StartMotion()
        {
            if (!UiMotion.AnimationsEnabled)
            {
                hoverAmount = pointerInside ? 1f : 0f;
                Invalidate();
                return;
            }

            if (motionTimer == null)
            {
                motionTimer = new Timer();
                motionTimer.Interval = 16;
                motionTimer.Tick += delegate
                {
                    float target = pointerInside ? 1f : 0f;
                    hoverAmount += (target - hoverAmount) * 0.28f;
                    if (pointerInside)
                    {
                        shinePhase += 0.035f;
                        if (shinePhase >= 1f) shinePhase -= 1f;
                    }
                    if (rippleAlpha > 0f)
                    {
                        rippleRadius += Math.Max(4f, Width * 0.035f);
                        rippleAlpha = Math.Max(0f, rippleAlpha - 0.065f);
                    }
                    bool settled = Math.Abs(target - hoverAmount) < 0.02f;
                    if (settled)
                    {
                        hoverAmount = target;
                        if (!pointerInside && rippleAlpha <= 0f) motionTimer.Stop();
                    }
                    Invalidate();
                };
            }
            motionTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = Enabled ? BackColor : UiMotion.Blend(BackColor, AppTheme.Window, 0.55f);
            fill = UiMotion.Blend(fill, Color.White, hoverAmount * 0.10f);
            if (pointerDown) fill = UiMotion.Blend(fill, Color.Black, 0.12f);

            Rectangle rect = new Rectangle(0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
            using (GraphicsPath path = SmoothPanel.RoundedRect(rect, 8))
            using (SolidBrush brush = new SolidBrush(fill))
            {
                e.Graphics.FillPath(brush, path);
                GraphicsState state = e.Graphics.Save();
                e.Graphics.SetClip(path);
                if (hoverAmount > 0.02f)
                {
                    int shineWidth = Math.Max(42, Width / 3);
                    int shineLeft = (int)((Width + shineWidth) * shinePhase) - shineWidth;
                    Rectangle shineRect = new Rectangle(shineLeft, 0, shineWidth, Height);
                    using (LinearGradientBrush shine = new LinearGradientBrush(
                        shineRect,
                        Color.FromArgb(0, Color.White),
                        Color.FromArgb((int)(42 * hoverAmount), Color.White),
                        LinearGradientMode.Horizontal))
                    {
                        ColorBlend blend = new ColorBlend();
                        blend.Colors = new[]
                        {
                            Color.FromArgb(0, Color.White),
                            Color.FromArgb((int)(42 * hoverAmount), Color.White),
                            Color.FromArgb(0, Color.White)
                        };
                        blend.Positions = new[] { 0f, 0.5f, 1f };
                        shine.InterpolationColors = blend;
                        e.Graphics.FillRectangle(shine, shineRect);
                    }
                }
                if (rippleAlpha > 0f)
                {
                    int rippleSize = (int)(rippleRadius * 2f);
                    using (SolidBrush ripple = new SolidBrush(Color.FromArgb((int)(55 * rippleAlpha), Color.White)))
                    {
                        e.Graphics.FillEllipse(
                            ripple,
                            rippleOrigin.X - (int)rippleRadius,
                            rippleOrigin.Y - (int)rippleRadius,
                            rippleSize,
                            rippleSize);
                    }
                }
                e.Graphics.Restore(state);
                using (Pen edge = new Pen(Color.FromArgb((int)(28 + hoverAmount * 70), ForeColor), 1f))
                {
                    e.Graphics.DrawPath(edge, path);
                }
                if (Focused && ShowFocusCues)
                {
                    using (Pen focus = new Pen(Color.FromArgb(110, ForeColor), 1f))
                    {
                        e.Graphics.DrawPath(focus, path);
                    }
                }
            }

            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                ClientRectangle,
                Enabled ? ForeColor : AppTheme.TextMuted,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && motionTimer != null)
            {
                motionTimer.Stop();
                motionTimer.Dispose();
                motionTimer = null;
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class AnimatedNavButton : Button
    {
        private Timer motionTimer;
        private float hoverAmount;
        private bool pointerInside;
        private bool selectedState;

        public bool SelectedState
        {
            get { return selectedState; }
            set
            {
                if (selectedState == value) return;
                selectedState = value;
                StartMotion();
                Invalidate();
            }
        }

        public AnimatedNavButton()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            BackColor = AppTheme.Sidebar;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            pointerInside = true;
            StartMotion();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            pointerInside = false;
            StartMotion();
            base.OnMouseLeave(e);
        }

        private void StartMotion()
        {
            if (!UiMotion.AnimationsEnabled)
            {
                hoverAmount = pointerInside ? 1f : 0f;
                Invalidate();
                return;
            }

            if (motionTimer == null)
            {
                motionTimer = new Timer();
                motionTimer.Interval = 16;
                motionTimer.Tick += delegate
                {
                    float target = pointerInside ? 1f : 0f;
                    hoverAmount += (target - hoverAmount) * 0.30f;
                    if (Math.Abs(target - hoverAmount) < 0.02f)
                    {
                        hoverAmount = target;
                        motionTimer.Stop();
                    }
                    Invalidate();
                };
            }
            motionTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(AppTheme.Sidebar);

            Color hover = UiMotion.Blend(AppTheme.Sidebar, AppTheme.SurfaceRaised, hoverAmount);
            Color fill = selectedState
                ? UiMotion.Blend(AppTheme.AccentSoft, AppTheme.Accent, hoverAmount * 0.12f)
                : hover;
            Rectangle rect = new Rectangle(2, 2, Math.Max(0, Width - 4), Math.Max(0, Height - 4));
            using (GraphicsPath path = SmoothPanel.RoundedRect(rect, 9))
            using (SolidBrush brush = new SolidBrush(fill))
            {
                e.Graphics.FillPath(brush, path);
            }

            if (selectedState)
            {
                using (SolidBrush accent = new SolidBrush(AppTheme.Accent))
                {
                    e.Graphics.FillRectangle(accent, 2, 12, 3, Math.Max(8, Height - 24));
                }
            }

            Rectangle textRect = new Rectangle(17, 0, Math.Max(0, Width - 25), Height);
            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                textRect,
                selectedState ? Color.White : UiMotion.Blend(AppTheme.TextMuted, AppTheme.Text, hoverAmount),
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && motionTimer != null)
            {
                motionTimer.Stop();
                motionTimer.Dispose();
                motionTimer = null;
            }
            base.Dispose(disposing);
        }
    }

    internal class SmoothPanel : Panel
    {
        public Color BorderColor { get; set; }
        public int Radius { get; set; }

        public SmoothPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BorderColor = AppTheme.Border;
            Radius = 12;
            BackColor = AppTheme.Surface;
            Padding = new Padding(1);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = RoundedRect(rect, Radius))
            using (Pen pen = new Pen(BorderColor))
            {
                e.Graphics.DrawPath(pen, path);
            }
        }

        internal static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            if (bounds.Width <= diameter || bounds.Height <= diameter)
            {
                path.AddRectangle(bounds);
                path.CloseFigure();
                return path;
            }
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class ModernScrollSmoothPanel : SmoothPanel
    {
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.Style = ModernScrollChrome.RemoveNativeStyles(parameters.Style);
                return parameters;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ModernScrollChrome.HideNativeBars(this);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            ModernScrollChrome.HideNativeBars(this);
            ModernScrollChrome.HideNativeBarsLater(this);
        }

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            ModernScrollChrome.HideNativeBars(this);
            ModernScrollChrome.HideNativeBarsLater(this);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ModernScrollChrome.HideNativeBars(this);
            base.OnMouseWheel(e);
            ModernScrollChrome.HideNativeBars(this);
            ModernScrollChrome.HideNativeBarsLater(this);
        }

        protected override void WndProc(ref Message message)
        {
            ModernScrollChrome.SuppressNativeStyles(ref message);
            bool suppress = ModernScrollChrome.IsScrollChromeMessage(message.Msg);
            if (suppress) ModernScrollChrome.HideNativeBars(this);
            base.WndProc(ref message);
            if (suppress) ModernScrollChrome.HideNativeBars(this);
        }
    }

    internal sealed class PremiumCard : SmoothPanel
    {
        private Timer hoverTimer;
        private float hoverAmount;
        private bool pointerInside;

        public Color AccentColor { get; set; }

        public PremiumCard()
        {
            AccentColor = AppTheme.Accent;
            Cursor = Cursors.Hand;
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            HookChild(e.Control);
        }

        private void HookChild(Control child)
        {
            if (child == null) return;
            child.MouseEnter += delegate { SetPointerInside(true); };
            child.MouseLeave += delegate { VerifyPointer(); };
            foreach (Control nested in child.Controls) HookChild(nested);
            child.ControlAdded += delegate(object sender, ControlEventArgs e) { HookChild(e.Control); };
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            SetPointerInside(true);
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            VerifyPointer();
            base.OnMouseLeave(e);
        }

        private void VerifyPointer()
        {
            bool inside = RectangleToScreen(ClientRectangle).Contains(Cursor.Position);
            SetPointerInside(inside);
        }

        private void SetPointerInside(bool inside)
        {
            if (pointerInside == inside) return;
            pointerInside = inside;
            StartHoverMotion();
        }

        private void StartHoverMotion()
        {
            if (!UiMotion.AnimationsEnabled)
            {
                hoverAmount = pointerInside ? 1f : 0f;
                Invalidate();
                return;
            }
            if (hoverTimer == null)
            {
                hoverTimer = new Timer();
                hoverTimer.Interval = 16;
                hoverTimer.Tick += delegate
                {
                    float target = pointerInside ? 1f : 0f;
                    hoverAmount += (target - hoverAmount) * 0.24f;
                    if (Math.Abs(target - hoverAmount) < 0.015f)
                    {
                        hoverAmount = target;
                        hoverTimer.Stop();
                    }
                    Invalidate();
                };
            }
            hoverTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (hoverAmount <= 0.01f) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle borderRect = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
            using (GraphicsPath path = RoundedRect(borderRect, Math.Max(4, Radius - 1)))
            using (Pen glow = new Pen(Color.FromArgb((int)(55 + 105 * hoverAmount), AccentColor), 1.4f))
            {
                e.Graphics.DrawPath(glow, path);
            }
            int lineWidth = Math.Max(60, (int)((Width - 36) * hoverAmount));
            Rectangle lineRect = new Rectangle(18, 2, lineWidth, 2);
            using (LinearGradientBrush line = new LinearGradientBrush(
                lineRect,
                Color.FromArgb((int)(180 * hoverAmount), AccentColor),
                Color.FromArgb(0, AccentColor),
                LinearGradientMode.Horizontal))
            {
                e.Graphics.FillRectangle(line, lineRect);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && hoverTimer != null)
            {
                hoverTimer.Stop();
                hoverTimer.Dispose();
                hoverTimer = null;
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class PremiumBackdrop : Panel
    {
        private Timer ambientTimer;
        private float phase;

        public PremiumBackdrop()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = AppTheme.Window;
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            UpdateAmbientState();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateAmbientState();
        }

        private void UpdateAmbientState()
        {
            if (!UiMotion.AnimationsEnabled || !Visible || !IsHandleCreated)
            {
                if (ambientTimer != null) ambientTimer.Stop();
                return;
            }
            if (ambientTimer == null)
            {
                ambientTimer = new Timer();
                ambientTimer.Interval = 400;
                ambientTimer.Tick += delegate
                {
                    Form owner = FindForm();
                    if (!Visible || owner == null || !owner.Visible || owner.WindowState == FormWindowState.Minimized)
                    {
                        ambientTimer.Stop();
                        return;
                    }
                    phase += 0.04f;
                    if (phase >= 1f) phase -= 1f;
                    Invalidate();
                };
            }
            ambientTimer.Start();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(AppTheme.Window);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int drift = (int)(Math.Sin(phase * Math.PI * 2d) * 34d);
            using (SolidBrush purple = new SolidBrush(Color.FromArgb(14, AppTheme.Accent)))
            using (SolidBrush blue = new SolidBrush(Color.FromArgb(11, AppTheme.Cyan)))
            {
                e.Graphics.FillEllipse(purple, Width - 480 + drift, -170, 520, 430);
                e.Graphics.FillEllipse(blue, -250 - drift, Height - 260, 470, 350);
            }
            using (Pen grid = new Pen(Color.FromArgb(10, AppTheme.Cyan), 1f))
            {
                int offset = (int)(phase * 48f);
                for (int x = -100 + offset; x < Width + 100; x += 48)
                {
                    e.Graphics.DrawLine(grid, x, Height, x + 135, 0);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && ambientTimer != null)
            {
                ambientTimer.Stop();
                ambientTimer.Dispose();
                ambientTimer = null;
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class ToggleSwitch : Control
    {
        private bool isChecked;
        private float animatedPosition;
        private Timer animationTimer;
        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return isChecked; }
            set
            {
                if (isChecked == value) return;
                isChecked = value;
                if (UiMotion.AnimationsEnabled && IsHandleCreated)
                {
                    EnsureAnimation();
                }
                else
                {
                    animatedPosition = isChecked ? 1f : 0f;
                    Invalidate();
                }
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        public ToggleSwitch()
        {
            Width = 48;
            Height = 26;
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            TabStop = true;
        }

        private void EnsureAnimation()
        {
            if (animationTimer == null)
            {
                animationTimer = new Timer();
                animationTimer.Interval = 25;
                animationTimer.Tick += delegate
                {
                    float target = isChecked ? 1f : 0f;
                    animatedPosition += (target - animatedPosition) * 0.32f;
                    if (Math.Abs(target - animatedPosition) < 0.02f)
                    {
                        animatedPosition = target;
                        animationTimer.Stop();
                    }
                    Invalidate();
                };
            }
            if (!animationTimer.Enabled) animationTimer.Start();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location))
            {
                Checked = !Checked;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                Checked = !Checked;
                e.Handled = true;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle track = new Rectangle(0, 2, Width - 1, Height - 5);
            Color trackColor = UiMotion.Blend(
                Color.FromArgb(65, 73, 90),
                AppTheme.Accent,
                animatedPosition);
            using (GraphicsPath path = SmoothPanel.RoundedRect(track, track.Height / 2))
            using (SolidBrush brush = new SolidBrush(trackColor))
            {
                e.Graphics.FillPath(brush, path);
            }
            int knobSize = Height - 10;
            int knobX = 5 + (int)((Width - knobSize - 10) * animatedPosition);
            using (SolidBrush knob = new SolidBrush(Color.White))
            {
                e.Graphics.FillEllipse(knob, knobX, 5, knobSize, knobSize);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && animationTimer != null)
            {
                animationTimer.Stop();
                animationTimer.Dispose();
                animationTimer = null;
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class PerformanceGauge : Control
    {
        private int value;
        public int Value
        {
            get { return value; }
            set { this.value = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        public string Caption { get; set; }

        public PerformanceGauge()
        {
            Width = 160;
            Height = 160;
            DoubleBuffered = true;
            Caption = "Hazırlık";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle ring = new Rectangle(13, 13, Width - 27, Height - 27);
            using (Pen background = new Pen(Color.FromArgb(48, 56, 71), 11f))
            using (Pen accent = new Pen(AppTheme.Cyan, 11f))
            {
                background.StartCap = LineCap.Round;
                background.EndCap = LineCap.Round;
                accent.StartCap = LineCap.Round;
                accent.EndCap = LineCap.Round;
                e.Graphics.DrawArc(background, ring, -90, 360);
                e.Graphics.DrawArc(accent, ring, -90, 360f * Value / 100f);
            }

            using (Font score = new Font("Segoe UI Semibold", 28f, FontStyle.Bold))
            using (Font caption = new Font("Segoe UI", 8.5f))
            using (SolidBrush text = new SolidBrush(AppTheme.Text))
            using (SolidBrush muted = new SolidBrush(AppTheme.TextMuted))
            {
                string scoreText = Value.ToString();
                SizeF scoreSize = e.Graphics.MeasureString(scoreText, score);
                e.Graphics.DrawString(scoreText, score, text, (Width - scoreSize.Width) / 2f, Height / 2f - 34f);
                SizeF captionSize = e.Graphics.MeasureString(Caption, caption);
                e.Graphics.DrawString(Caption, caption, muted, (Width - captionSize.Width) / 2f, Height / 2f + 17f);
            }
        }
    }

    internal sealed class CleanupGauge : Control
    {
        private int progress;
        private bool busy;
        private float phase;
        private float hoverAmount;
        private bool hovered;
        private bool pressed;
        private bool completed;
        private int celebrationFrame;
        private string primaryText;
        private string secondaryText;
        private Timer animationTimer;

        public int Progress
        {
            get { return progress; }
            set
            {
                progress = Math.Max(0, Math.Min(100, value));
                Invalidate();
            }
        }

        public bool Busy
        {
            get { return busy; }
            set
            {
                busy = value;
                UpdateAnimation();
                Invalidate();
            }
        }

        public bool Completed
        {
            get { return completed; }
            set
            {
                if (completed == value) return;
                completed = value;
                celebrationFrame = 0;
                UpdateAnimation();
                Invalidate();
            }
        }

        public string PrimaryText
        {
            get { return primaryText; }
            set
            {
                primaryText = value;
                AccessibleName = value;
                Invalidate();
            }
        }

        public string SecondaryText
        {
            get { return secondaryText; }
            set
            {
                secondaryText = value;
                AccessibleDescription = value;
                Invalidate();
            }
        }

        public CleanupGauge()
        {
            Size = new Size(250, 250);
            DoubleBuffered = true;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.PushButton;
            PrimaryText = "TARA";
            SecondaryText = "Temizlik analizi";
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hovered = true;
            UpdateAnimation();
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hovered = false;
            pressed = false;
            UpdateAnimation();
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !Busy)
            {
                pressed = true;
                Focus();
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!Busy && (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space))
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        private void UpdateAnimation()
        {
            if (!UiMotion.AnimationsEnabled)
            {
                hoverAmount = hovered ? 1f : 0f;
                if (animationTimer != null) animationTimer.Stop();
                return;
            }
            if (animationTimer == null)
            {
                animationTimer = new Timer();
                animationTimer.Interval = 35;
                animationTimer.Tick += delegate
                {
                    float target = hovered && !Busy ? 1f : 0f;
                    hoverAmount += (target - hoverAmount) * 0.22f;
                    bool celebrating = Completed && celebrationFrame < 54;
                    if (celebrating) celebrationFrame++;
                    phase += Busy ? 0.014f : celebrating ? 0.018f : 0.006f;
                    if (phase >= 1f) phase -= 1f;
                    if (!Busy && Math.Abs(target - hoverAmount) < 0.015f)
                    {
                        hoverAmount = target;
                        if (!hovered && !celebrating) animationTimer.Stop();
                    }
                    Invalidate();
                };
            }
            animationTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            PointF center = new PointF(Width / 2f, Height / 2f);
            float radius = Math.Min(Width, Height) * 0.41f +
                           hoverAmount * 2.5f -
                           (pressed ? 2f : 0f);
            RectangleF outer = new RectangleF(
                center.X - radius,
                center.Y - radius,
                radius * 2f,
                radius * 2f);

            bool cleanAction =
                (!string.IsNullOrWhiteSpace(PrimaryText) &&
                 PrimaryText.IndexOf("TEMİZ", StringComparison.OrdinalIgnoreCase) >= 0) ||
                (!string.IsNullOrWhiteSpace(SecondaryText) &&
                 SecondaryText.IndexOf("TEMİZ", StringComparison.OrdinalIgnoreCase) >= 0);
            Color actionColor = cleanAction || Completed ? AppTheme.Green : AppTheme.Cyan;
            float completionPulse = Completed && celebrationFrame < 54
                ? 0.5f + 0.5f * (float)Math.Sin(phase * Math.PI * 2d)
                : 0f;
            int glowAlpha = Busy
                ? 38
                : 22 + (int)(26f * hoverAmount) + (int)(24f * completionPulse);
            using (SolidBrush glow = new SolidBrush(Color.FromArgb(glowAlpha, actionColor)))
            {
                e.Graphics.FillEllipse(
                    glow,
                    outer.X - 14f,
                    outer.Y - 14f,
                    outer.Width + 28f,
                    outer.Height + 28f);
            }
            using (Pen track = new Pen(Color.FromArgb(45, 48, 72), 9f))
            using (Pen ring = new Pen(
                UiMotion.Blend(actionColor, Color.White, hoverAmount * 0.18f),
                9f + hoverAmount * 1.5f))
            {
                track.StartCap = track.EndCap = LineCap.Round;
                ring.StartCap = ring.EndCap = LineCap.Round;
                e.Graphics.DrawArc(track, outer, -90f, 360f);
                float sweep = Math.Max(Busy ? 18f : 0f, Progress * 3.6f);
                float start = Busy ? -90f + phase * 360f : -90f;
                e.Graphics.DrawArc(ring, outer, start, Math.Min(360f, sweep));
            }

            RectangleF inner = new RectangleF(
                outer.X + 23f,
                outer.Y + 23f,
                outer.Width - 46f,
                outer.Height - 46f);
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(inner);
                using (PathGradientBrush fill = new PathGradientBrush(path))
                {
                    fill.CenterColor = Color.FromArgb(52, 42, 94);
                    fill.SurroundColors = new[] { Color.FromArgb(12, 16, 36) };
                    e.Graphics.FillPath(fill, path);
                }
            }
            using (Pen border = new Pen(Color.FromArgb(115, AppTheme.Accent), 2f))
            {
                e.Graphics.DrawEllipse(border, inner);
            }
            if (hoverAmount > 0.02f)
            {
                float orbit = phase * 360f;
                using (Pen highlight = new Pen(Color.FromArgb((int)(145f * hoverAmount), Color.White), 2f))
                {
                    highlight.StartCap = LineCap.Round;
                    highlight.EndCap = LineCap.Round;
                    e.Graphics.DrawArc(highlight, outer, orbit - 90f, 42f);
                }
            }
            if (Completed)
            {
                using (Pen check = new Pen(AppTheme.Green, 4f))
                {
                    check.StartCap = LineCap.Round;
                    check.EndCap = LineCap.Round;
                    e.Graphics.DrawLines(
                        check,
                        new[]
                        {
                            new PointF(center.X - 15f, center.Y - 47f),
                            new PointF(center.X - 4f, center.Y - 36f),
                            new PointF(center.X + 19f, center.Y - 61f)
                        });
                }
            }

            string primary = string.IsNullOrWhiteSpace(PrimaryText) ? (Progress + "%") : PrimaryText;
            string secondary = string.IsNullOrWhiteSpace(SecondaryText) ? string.Empty : SecondaryText;
            float primarySize = primary.Length > 12 ? 14f : primary.Length > 7 ? 18f : 27f;
            using (Font primaryFont = new Font("Segoe UI Semilight", primarySize, FontStyle.Regular))
            using (Font secondaryFont = new Font("Segoe UI Semibold", 8f, FontStyle.Bold))
            using (SolidBrush text = new SolidBrush(AppTheme.Text))
            using (SolidBrush muted = new SolidBrush(actionColor))
            {
                SizeF primaryBounds = e.Graphics.MeasureString(primary, primaryFont);
                e.Graphics.DrawString(
                    primary,
                    primaryFont,
                    text,
                    center.X - primaryBounds.Width / 2f,
                    center.Y - primaryBounds.Height / 2f - 8f);
                SizeF secondaryBounds = e.Graphics.MeasureString(secondary, secondaryFont);
                e.Graphics.DrawString(
                    secondary,
                    secondaryFont,
                    muted,
                    center.X - secondaryBounds.Width / 2f,
                    center.Y + 29f);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && animationTimer != null)
            {
                animationTimer.Stop();
                animationTimer.Dispose();
                animationTimer = null;
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class AutomotiveGauge : Control
    {
        private int value;
        private int targetValue;
        private int impactScore;
        private float animatedValue;
        private bool initialAnimationPending;
        private Timer needleTimer;

        public int Value
        {
            get { return value; }
            set
            {
                this.value = Math.Max(0, Math.Min(100, value));
                if (!UiMotion.AnimationsEnabled)
                {
                    animatedValue = this.value;
                }
                else if (!IsHandleCreated)
                {
                    animatedValue = 0f;
                    initialAnimationPending = this.value > 0;
                }
                else
                {
                    StartNeedleAnimation();
                }
                Invalidate();
            }
        }

        public int TargetValue
        {
            get { return targetValue; }
            set { targetValue = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        public int ImpactScore
        {
            get { return impactScore; }
            set { impactScore = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        public string ImpactLabel { get; set; }
        public bool HasReading { get; set; }

        public AutomotiveGauge()
        {
            Width = 360;
            Height = 245;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            ImpactLabel = "TARANMADI";
            HasReading = false;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (initialAnimationPending)
            {
                initialAnimationPending = false;
                StartNeedleAnimation();
            }
        }

        private void StartNeedleAnimation()
        {
            if (!UiMotion.AnimationsEnabled)
            {
                animatedValue = value;
                Invalidate();
                return;
            }
            if (needleTimer == null)
            {
                needleTimer = new Timer();
                needleTimer.Interval = 25;
                needleTimer.Tick += delegate
                {
                    animatedValue += (value - animatedValue) * 0.16f;
                    if (Math.Abs(value - animatedValue) < 0.25f)
                    {
                        animatedValue = value;
                        needleTimer.Stop();
                    }
                    Invalidate();
                };
            }
            if (!needleTimer.Enabled) needleTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            PointF center = new PointF(Width / 2f, Height - 42f);
            float radius = Math.Min(Width * 0.38f, Height * 0.61f);
            RectangleF arc = new RectangleF(center.X - radius, center.Y - radius, radius * 2f, radius * 2f);
            const float start = 145f;
            const float sweep = 250f;

            using (Pen baseArc = new Pen(Color.FromArgb(58, 66, 80), 15f))
            using (Pen low = new Pen(AppTheme.Red, 15f))
            using (Pen medium = new Pen(AppTheme.Amber, 15f))
            using (Pen high = new Pen(AppTheme.Green, 15f))
            {
                baseArc.StartCap = baseArc.EndCap = LineCap.Round;
                low.StartCap = medium.StartCap = high.StartCap = LineCap.Flat;
                low.EndCap = medium.EndCap = high.EndCap = LineCap.Flat;
                e.Graphics.DrawArc(baseArc, arc, start, sweep);
                e.Graphics.DrawArc(low, arc, start, sweep * 0.45f);
                e.Graphics.DrawArc(medium, arc, start + sweep * 0.45f, sweep * 0.30f);
                e.Graphics.DrawArc(high, arc, start + sweep * 0.75f, sweep * 0.25f);
            }

            using (Pen tick = new Pen(Color.FromArgb(205, 215, 228), 2f))
            using (Pen minor = new Pen(Color.FromArgb(100, 110, 126), 1f))
            using (Font number = new Font("Segoe UI Semibold", 7.5f, FontStyle.Bold))
            using (SolidBrush numberBrush = new SolidBrush(AppTheme.TextMuted))
            {
                for (int i = 0; i <= 20; i++)
                {
                    float gaugeValue = i * 5f;
                    double angle = DegreesToRadians(start + sweep * gaugeValue / 100f);
                    bool major = i % 2 == 0;
                    float outer = radius - 13f;
                    float inner = outer - (major ? 12f : 7f);
                    PointF p1 = Polar(center, outer, angle);
                    PointF p2 = Polar(center, inner, angle);
                    e.Graphics.DrawLine(major ? tick : minor, p1, p2);
                    if (major)
                    {
                        PointF labelPoint = Polar(center, radius - 35f, angle);
                        string label = ((int)gaugeValue).ToString();
                        SizeF size = e.Graphics.MeasureString(label, number);
                        e.Graphics.DrawString(label, number, numberBrush, labelPoint.X - size.Width / 2f, labelPoint.Y - size.Height / 2f);
                    }
                }
            }

            if (HasReading && TargetValue > Value)
            {
                double targetAngle = DegreesToRadians(start + sweep * TargetValue / 100f);
                PointF target = Polar(center, radius + 1f, targetAngle);
                using (SolidBrush targetBrush = new SolidBrush(AppTheme.Cyan))
                using (Pen targetRing = new Pen(Color.FromArgb(180, AppTheme.Cyan), 2f))
                {
                    e.Graphics.FillEllipse(targetBrush, target.X - 5f, target.Y - 5f, 10f, 10f);
                    e.Graphics.DrawEllipse(targetRing, target.X - 8f, target.Y - 8f, 16f, 16f);
                }
            }

            if (HasReading)
            {
                double needleAngle = DegreesToRadians(start + sweep * animatedValue / 100f);
                PointF needleTip = Polar(center, radius - 30f, needleAngle);
                PointF needleTail = Polar(center, 24f, needleAngle + Math.PI);
                using (Pen shadow = new Pen(Color.FromArgb(100, 0, 0, 0), 7f))
                using (Pen needle = new Pen(Color.White, 4f))
                {
                    shadow.StartCap = shadow.EndCap = LineCap.Round;
                    needle.StartCap = needle.EndCap = LineCap.Round;
                    e.Graphics.DrawLine(shadow, needleTail.X + 2f, needleTail.Y + 3f, needleTip.X + 2f, needleTip.Y + 3f);
                    e.Graphics.DrawLine(needle, needleTail, needleTip);
                }
            }
            using (SolidBrush hubOuter = new SolidBrush(AppTheme.Accent))
            using (SolidBrush hubInner = new SolidBrush(Color.White))
            {
                e.Graphics.FillEllipse(hubOuter, center.X - 13f, center.Y - 13f, 26f, 26f);
                e.Graphics.FillEllipse(hubInner, center.X - 5f, center.Y - 5f, 10f, 10f);
            }

            using (Font score = new Font("Segoe UI Semibold", 28f, FontStyle.Bold))
            using (Font caption = new Font("Segoe UI Semibold", 8f, FontStyle.Bold))
            using (Font impact = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold))
            using (SolidBrush text = new SolidBrush(AppTheme.Text))
            using (SolidBrush muted = new SolidBrush(AppTheme.TextMuted))
            {
                string scoreText = HasReading ? ((int)Math.Round(animatedValue)).ToString() : "—";
                SizeF scoreSize = e.Graphics.MeasureString(scoreText, score);
                e.Graphics.DrawString(scoreText, score, text, center.X - scoreSize.Width / 2f, center.Y - 83f);
                string captionText = "PERFORMANS HAZIRLIĞI";
                SizeF captionSize = e.Graphics.MeasureString(captionText, caption);
                e.Graphics.DrawString(captionText, caption, muted, center.X - captionSize.Width / 2f, center.Y - 47f);

                string impactText = HasReading
                    ? "TAHMİNİ ETKİ  " + (ImpactLabel ?? "—") + "  •  " + ImpactScore
                    : "TARAMA BEKLENİYOR";
                SizeF impactSize = e.Graphics.MeasureString(impactText, impact);
                RectangleF chip = new RectangleF(center.X - impactSize.Width / 2f - 10f, Height - 25f, impactSize.Width + 20f, 22f);
                using (GraphicsPath path = SmoothPanel.RoundedRect(Rectangle.Round(chip), 8))
                using (SolidBrush chipBrush = new SolidBrush(Color.FromArgb(58, 35, 31)))
                using (Pen chipBorder = new Pen(AppTheme.Accent))
                {
                    e.Graphics.FillPath(chipBrush, path);
                    e.Graphics.DrawPath(chipBorder, path);
                    e.Graphics.DrawString(impactText, impact, text, chip.Left + 10f, chip.Top + 3f);
                }
            }
        }

        private static PointF Polar(PointF center, float radius, double angle)
        {
            return new PointF(
                center.X + radius * (float)Math.Cos(angle),
                center.Y + radius * (float)Math.Sin(angle));
        }

        private static double DegreesToRadians(double value)
        {
            return value * Math.PI / 180d;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && needleTimer != null)
            {
                needleTimer.Stop();
                needleTimer.Dispose();
                needleTimer = null;
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class GameBoostGauge : Control
    {
        private float animatedValue = 7f;
        private float targetValue = 7f;
        private bool active;
        private bool busy;
        private float pulse;
        private Timer animationTimer;

        public bool Active
        {
            get { return active; }
            set
            {
                active = value;
                targetValue = value ? 94f : 7f;
                EnsureAnimation();
                Invalidate();
            }
        }

        public bool Busy
        {
            get { return busy; }
        }

        public GameBoostGauge()
        {
            Size = new Size(64, 58);
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
        }

        public void BeginTransition(bool turningOn)
        {
            busy = true;
            targetValue = turningOn ? 94f : 7f;
            if (turningOn && animatedValue > 18f) animatedValue = 7f;
            if (!turningOn && animatedValue < 82f) animatedValue = 94f;
            EnsureAnimation();
            Invalidate();
        }

        public void CompleteTransition(bool nowActive)
        {
            busy = false;
            active = nowActive;
            targetValue = nowActive ? 94f : 7f;
            EnsureAnimation();
            Invalidate();
        }

        private void EnsureAnimation()
        {
            if (!UiMotion.AnimationsEnabled)
            {
                animatedValue = targetValue;
                Invalidate();
                return;
            }
            if (animationTimer == null)
            {
                animationTimer = new Timer();
                animationTimer.Interval = 30;
                animationTimer.Tick += delegate
                {
                    pulse += 0.16f;
                    float difference = targetValue - animatedValue;
                    animatedValue += difference * (busy ? 0.045f : 0.12f);
                    if (Math.Abs(difference) < 0.25f)
                    {
                        animatedValue = targetValue;
                        if (!busy) animationTimer.Stop();
                    }
                    Invalidate();
                };
            }
            if (!animationTimer.Enabled) animationTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            PointF center = new PointF(Width / 2f, Height - 9f);
            float radius = Math.Min(Width * 0.39f, Height * 0.52f);
            RectangleF bounds = new RectangleF(
                center.X - radius,
                center.Y - radius,
                radius * 2f,
                radius * 2f);
            const float start = 150f;
            const float sweep = 240f;
            Color accent = busy ? AppTheme.Cyan : (active ? AppTheme.Green : AppTheme.Amber);

            using (Pen track = new Pen(Color.FromArgb(78, 84, 108), 4.5f))
            using (Pen progress = new Pen(accent, 4.5f))
            {
                track.StartCap = track.EndCap = LineCap.Round;
                progress.StartCap = progress.EndCap = LineCap.Round;
                e.Graphics.DrawArc(track, bounds, start, sweep);
                e.Graphics.DrawArc(progress, bounds, start, sweep * animatedValue / 100f);
            }

            using (Pen tick = new Pen(Color.FromArgb(150, AppTheme.TextMuted), 1.2f))
            {
                for (int i = 0; i <= 6; i++)
                {
                    double angle = (start + sweep * i / 6f) * Math.PI / 180d;
                    PointF outer = new PointF(
                        center.X + (radius - 5f) * (float)Math.Cos(angle),
                        center.Y + (radius - 5f) * (float)Math.Sin(angle));
                    PointF inner = new PointF(
                        center.X + (radius - 10f) * (float)Math.Cos(angle),
                        center.Y + (radius - 10f) * (float)Math.Sin(angle));
                    e.Graphics.DrawLine(tick, inner, outer);
                }
            }

            double needleAngle = (start + sweep * animatedValue / 100f) * Math.PI / 180d;
            PointF needleTip = new PointF(
                center.X + (radius - 8f) * (float)Math.Cos(needleAngle),
                center.Y + (radius - 8f) * (float)Math.Sin(needleAngle));
            using (Pen needle = new Pen(Color.White, 2.6f))
            using (SolidBrush hub = new SolidBrush(accent))
            using (SolidBrush core = new SolidBrush(Color.White))
            {
                needle.StartCap = needle.EndCap = LineCap.Round;
                e.Graphics.DrawLine(needle, center, needleTip);
                e.Graphics.FillEllipse(hub, center.X - 5.5f, center.Y - 5.5f, 11f, 11f);
                e.Graphics.FillEllipse(core, center.X - 2f, center.Y - 2f, 4f, 4f);
            }

            if (active && !busy)
            {
                using (SolidBrush badge = new SolidBrush(AppTheme.Cyan))
                using (Font checkFont = new Font("Segoe UI Symbol", 7f, FontStyle.Bold))
                using (SolidBrush check = new SolidBrush(Color.FromArgb(6, 14, 24)))
                {
                    RectangleF badgeBounds = new RectangleF(Width - 17f, 4f, 13f, 13f);
                    e.Graphics.FillEllipse(badge, badgeBounds);
                    e.Graphics.DrawString("✓", checkFont, check, badgeBounds.X + 1.4f, badgeBounds.Y - 0.4f);
                }
            }
            else if (busy)
            {
                int alpha = 90 + (int)(50f * (0.5f + 0.5f * Math.Sin(pulse)));
                using (Pen glow = new Pen(Color.FromArgb(alpha, accent), 2f))
                {
                    e.Graphics.DrawEllipse(glow, Width - 18f, 4f, 13f, 13f);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && animationTimer != null)
            {
                animationTimer.Stop();
                animationTimer.Dispose();
                animationTimer = null;
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class DpiStatusOrb : Control
    {
        private bool active;
        private float phase;
        private Timer animationTimer;

        public bool Active
        {
            get { return active; }
            set
            {
                if (active == value) return;
                active = value;
                if (active && UiMotion.AnimationsEnabled && IsHandleCreated)
                {
                    EnsureTimer();
                }
                else if (animationTimer != null)
                {
                    animationTimer.Stop();
                }
                Invalidate();
            }
        }

        public DpiStatusOrb()
        {
            Size = new Size(170, 170);
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (active && UiMotion.AnimationsEnabled) EnsureTimer();
        }

        private void EnsureTimer()
        {
            if (animationTimer == null)
            {
                animationTimer = new Timer();
                animationTimer.Interval = 70;
                animationTimer.Tick += delegate
                {
                    phase += 0.08f;
                    if (phase > 1f) phase -= 1f;
                    Invalidate();
                };
            }
            if (!animationTimer.Enabled) animationTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            PointF center = new PointF(Width / 2f, Height / 2f);
            Color status = active ? AppTheme.Cyan : Color.FromArgb(85, 95, 112);

            for (int i = 0; i < 3; i++)
            {
                float pulse = active ? (phase + i / 3f) % 1f : i * 0.18f;
                float radius = 42f + pulse * 33f;
                int alpha = active ? (int)(90 * (1f - pulse)) : 22;
                using (Pen ring = new Pen(Color.FromArgb(alpha, status), 2f))
                {
                    e.Graphics.DrawEllipse(
                        ring,
                        center.X - radius,
                        center.Y - radius,
                        radius * 2f,
                        radius * 2f);
                }
            }

            using (SolidBrush glow = new SolidBrush(Color.FromArgb(active ? 48 : 24, status)))
            using (SolidBrush core = new SolidBrush(status))
            using (Pen border = new Pen(Color.FromArgb(180, status), 2f))
            {
                e.Graphics.FillEllipse(glow, center.X - 38f, center.Y - 38f, 76f, 76f);
                e.Graphics.FillEllipse(core, center.X - 23f, center.Y - 23f, 46f, 46f);
                e.Graphics.DrawEllipse(border, center.X - 30f, center.Y - 30f, 60f, 60f);
            }

            using (Font icon = new Font("Segoe UI Semibold", 18f, FontStyle.Bold))
            using (SolidBrush text = new SolidBrush(active ? Color.FromArgb(7, 23, 30) : AppTheme.Text))
            {
                string glyph = active ? "ON" : "OFF";
                SizeF size = e.Graphics.MeasureString(glyph, icon);
                e.Graphics.DrawString(glyph, icon, text, center.X - size.Width / 2f, center.Y - size.Height / 2f);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && animationTimer != null)
            {
                animationTimer.Stop();
                animationTimer.Dispose();
                animationTimer = null;
            }
            base.Dispose(disposing);
        }
    }

    internal enum OptimizerOrbState
    {
        Scan,
        Scanning,
        Optimize,
        Optimizing,
        Complete
    }

    internal sealed class OptimizerOrbButton : Control
    {
        private OptimizerOrbState state;
        private int progress;
        private float phase;
        private bool hovered;
        private Timer animationTimer;

        public event EventHandler OrbClick;

        public OptimizerOrbState State
        {
            get { return state; }
            set
            {
                if (state == value) return;
                state = value;
                UpdateAnimationState();
                Invalidate();
            }
        }

        public int Progress
        {
            get { return progress; }
            set
            {
                progress = Math.Max(0, Math.Min(100, value));
                Invalidate();
            }
        }

        public bool Busy
        {
            get { return state == OptimizerOrbState.Scanning || state == OptimizerOrbState.Optimizing; }
        }

        public OptimizerOrbButton()
        {
            Size = new Size(270, 270);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            DoubleBuffered = true;
            state = OptimizerOrbState.Scan;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            hovered = true;
            UpdateAnimationState();
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hovered = false;
            UpdateAnimationState();
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location) && !Busy)
            {
                if (OrbClick != null) OrbClick(this, EventArgs.Empty);
            }
        }

        private void UpdateAnimationState()
        {
            bool shouldAnimate = UiMotion.AnimationsEnabled && (Busy || hovered);
            if (!shouldAnimate)
            {
                if (animationTimer != null) animationTimer.Stop();
                return;
            }

            if (animationTimer == null)
            {
                animationTimer = new Timer();
                animationTimer.Interval = 33;
                animationTimer.Tick += delegate
                {
                    phase += Busy ? 0.018f : 0.008f;
                    if (phase >= 1f) phase -= 1f;
                    Invalidate();
                };
            }
            if (!animationTimer.Enabled) animationTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            PointF center = new PointF(Width / 2f, Height / 2f);
            float pulse = hovered || Busy
                ? 3f + 2f * (float)Math.Sin(phase * Math.PI * 2d)
                : 0f;
            float outerRadius = Math.Min(Width, Height) * 0.43f + pulse;
            RectangleF outer = Circle(center, outerRadius);

            using (SolidBrush ambient = new SolidBrush(Color.FromArgb(28, AppTheme.Cyan)))
            {
                e.Graphics.FillEllipse(
                    ambient,
                    center.X - outerRadius - 15f,
                    center.Y - outerRadius - 15f,
                    (outerRadius + 15f) * 2f,
                    (outerRadius + 15f) * 2f);
            }

            float rotation = phase * 360f;
            for (int i = 0; i < 10; i++)
            {
                float start = -90f + rotation + i * 36f;
                Color segmentColor = i % 2 == 0
                    ? AppTheme.Cyan
                    : AppTheme.Accent;
                using (Pen segment = new Pen(Color.FromArgb(215, segmentColor), 15f))
                {
                    segment.StartCap = LineCap.Round;
                    segment.EndCap = LineCap.Round;
                    e.Graphics.DrawArc(segment, outer, start, 23f);
                }
            }

            using (Pen outerBorder = new Pen(Color.FromArgb(120, AppTheme.Cyan), 2f))
            using (Pen innerBorder = new Pen(Color.FromArgb(115, 132, 190), 2f))
            {
                e.Graphics.DrawEllipse(outerBorder, outer);
                e.Graphics.DrawEllipse(innerBorder, Circle(center, outerRadius - 27f));
            }

            RectangleF core = Circle(center, outerRadius - 35f);
            using (GraphicsPath corePath = new GraphicsPath())
            {
                corePath.AddEllipse(core);
                using (PathGradientBrush gradient = new PathGradientBrush(corePath))
                {
                    gradient.CenterColor = Color.FromArgb(80, 62, 137);
                    gradient.SurroundColors = new[] { Color.FromArgb(12, 17, 39) };
                    e.Graphics.FillPath(gradient, corePath);
                }
            }

            if (Busy)
            {
                RectangleF progressRing = Circle(center, outerRadius - 23f);
                using (Pen progressPen = new Pen(AppTheme.Green, 5f))
                {
                    progressPen.StartCap = LineCap.Round;
                    progressPen.EndCap = LineCap.Round;
                    e.Graphics.DrawArc(progressPen, progressRing, -90f, Math.Max(3f, progress * 3.6f));
                }
            }

            string title;
            string detail;
            switch (state)
            {
                case OptimizerOrbState.Scanning:
                    title = "TARANIYOR";
                    detail = progress + "%";
                    break;
                case OptimizerOrbState.Optimize:
                    title = "OPTİMİZE ET";
                    detail = "Tek tıkla uygula";
                    break;
                case OptimizerOrbState.Optimizing:
                    title = "UYGULANIYOR";
                    detail = progress + "%";
                    break;
                case OptimizerOrbState.Complete:
                    title = "TAMAMLANDI";
                    detail = "Sonuçları inceleyin";
                    break;
                default:
                    title = "TARA";
                    detail = "Sistemi analiz et";
                    break;
            }

            using (Font titleFont = new Font("Segoe UI Semilight", title.Length > 9 ? 16f : 27f, FontStyle.Regular))
            using (Font detailFont = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold))
            using (SolidBrush titleBrush = new SolidBrush(Color.White))
            using (SolidBrush detailBrush = new SolidBrush(Busy ? AppTheme.Green : AppTheme.Cyan))
            {
                SizeF titleSize = e.Graphics.MeasureString(title, titleFont);
                e.Graphics.DrawString(
                    title,
                    titleFont,
                    titleBrush,
                    center.X - titleSize.Width / 2f,
                    center.Y - titleSize.Height / 2f - 8f);
                SizeF detailSize = e.Graphics.MeasureString(detail, detailFont);
                e.Graphics.DrawString(
                    detail,
                    detailFont,
                    detailBrush,
                    center.X - detailSize.Width / 2f,
                    center.Y + 26f);
            }
        }

        private static RectangleF Circle(PointF center, float radius)
        {
            return new RectangleF(
                center.X - radius,
                center.Y - radius,
                radius * 2f,
                radius * 2f);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && animationTimer != null)
            {
                animationTimer.Stop();
                animationTimer.Dispose();
                animationTimer = null;
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class TercanHeroPanel : SmoothPanel
    {
        private Timer ambientTimer;
        private float phase;

        public TercanHeroPanel()
        {
            DoubleBuffered = true;
            BorderColor = Color.FromArgb(96, AppTheme.Accent);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateAmbientMotion();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            UpdateAmbientMotion();
        }

        private void UpdateAmbientMotion()
        {
            if (!UiMotion.AnimationsEnabled || !Visible || !IsHandleCreated)
            {
                if (ambientTimer != null) ambientTimer.Stop();
                return;
            }

            if (ambientTimer == null)
            {
                ambientTimer = new Timer();
                ambientTimer.Interval = 160;
                ambientTimer.Tick += delegate
                {
                    if (!Visible || IsDisposed)
                    {
                        ambientTimer.Stop();
                        return;
                    }
                    Form owner = FindForm();
                    if (owner == null || !owner.Visible || owner.WindowState == FormWindowState.Minimized)
                    {
                        ambientTimer.Stop();
                        return;
                    }
                    phase += 0.018f;
                    if (phase >= 1f) phase -= 1f;
                    Invalidate();
                };
            }
            ambientTimer.Start();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (ambientTimer != null && !ambientTimer.Enabled && Visible)
            {
                UpdateAmbientMotion();
            }
            Rectangle bounds = ClientRectangle;
            using (LinearGradientBrush gradient = new LinearGradientBrush(
                bounds,
                Color.FromArgb(17, 19, 29),
                Color.FromArgb(9, 13, 30),
                LinearGradientMode.Horizontal))
            {
                e.Graphics.FillRectangle(gradient, bounds);
            }
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen grid = new Pen(Color.FromArgb(18, AppTheme.Cyan), 1f))
            {
                int offset = (int)(phase * 64f);
                for (int x = -160 + offset; x < Width + 100; x += 64)
                {
                    e.Graphics.DrawLine(grid, x, Height, x + 150, 0);
                }
                for (int y = 24; y < Height; y += 42)
                {
                    e.Graphics.DrawLine(grid, 0, y, Width, y);
                }
            }
            int glowShift = (int)(Math.Sin(phase * Math.PI * 2d) * 30d);
            using (LinearGradientBrush glow = new LinearGradientBrush(
                new Rectangle(0, 0, Width, Height),
                Color.FromArgb(0, AppTheme.Accent),
                Color.FromArgb(52, AppTheme.Accent),
                LinearGradientMode.Horizontal))
            {
                e.Graphics.FillRectangle(glow, Width - 390 + glowShift, 0, 390, Height);
            }
            using (LinearGradientBrush blueGlow = new LinearGradientBrush(
                new Rectangle(0, 0, Width, Height),
                Color.FromArgb(0, AppTheme.Cyan),
                Color.FromArgb(25, AppTheme.Cyan),
                LinearGradientMode.Vertical))
            {
                e.Graphics.FillRectangle(blueGlow, Width - 520, Height / 2, 520, Height / 2);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && ambientTimer != null)
            {
                ambientTimer.Stop();
                ambientTimer.Dispose();
                ambientTimer = null;
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class PageAccentSweep : Control
    {
        private Timer animationTimer;
        private float progress;

        public PageAccentSweep()
        {
            Height = 5;
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.SupportsTransparentBackColor,
                true);
            BackColor = Color.Transparent;
            Enabled = false;
        }

        public void StartAnimation()
        {
            if (!UiMotion.AnimationsEnabled)
            {
                Visible = false;
                return;
            }

            progress = 0f;
            Visible = true;
            if (animationTimer != null)
            {
                animationTimer.Stop();
                animationTimer.Dispose();
            }
            animationTimer = new Timer();
            animationTimer.Interval = 16;
            animationTimer.Tick += delegate
            {
                progress += 0.052f;
                if (progress >= 1f)
                {
                    progress = 1f;
                    animationTimer.Stop();
                    Visible = false;
                }
                Invalidate();
            };
            animationTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 0) return;
            float eased = 1f - (float)Math.Pow(1f - progress, 3d);
            int sweepWidth = Math.Max(140, Width / 4);
            int left = (int)((Width + sweepWidth) * eased) - sweepWidth;
            Rectangle glowRect = new Rectangle(left, 1, sweepWidth, Math.Max(1, Height - 2));
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (LinearGradientBrush glow = new LinearGradientBrush(
                glowRect,
                Color.FromArgb(0, AppTheme.Cyan),
                AppTheme.Accent,
                LinearGradientMode.Horizontal))
            {
                ColorBlend blend = new ColorBlend();
                blend.Colors = new[]
                {
                    Color.FromArgb(0, AppTheme.Cyan),
                    AppTheme.Cyan,
                    AppTheme.Accent,
                    Color.FromArgb(0, AppTheme.Accent)
                };
                blend.Positions = new[] { 0f, 0.35f, 0.7f, 1f };
                glow.InterpolationColors = blend;
                e.Graphics.FillRectangle(glow, glowRect);
            }
            Rectangle bloomRect = new Rectangle(left + sweepWidth / 3, 0, Math.Max(20, sweepWidth / 3), Height);
            using (LinearGradientBrush bloom = new LinearGradientBrush(
                bloomRect,
                Color.FromArgb(0, Color.White),
                Color.FromArgb(100, Color.White),
                LinearGradientMode.Horizontal))
            {
                ColorBlend bloomBlend = new ColorBlend();
                bloomBlend.Colors = new[]
                {
                    Color.FromArgb(0, Color.White),
                    Color.FromArgb(95, Color.White),
                    Color.FromArgb(0, Color.White)
                };
                bloomBlend.Positions = new[] { 0f, 0.5f, 1f };
                bloom.InterpolationColors = bloomBlend;
                e.Graphics.FillRectangle(bloom, bloomRect);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && animationTimer != null)
            {
                animationTimer.Stop();
                animationTimer.Dispose();
                animationTimer = null;
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class MetricCard : SmoothPanel
    {
        private readonly Label valueLabel;
        private readonly Label detailLabel;

        public MetricCard(string title, string value, string detail, Color accent)
        {
            Width = 220;
            Height = 116;
            Margin = new Padding(0, 0, 12, 12);
            BackColor = AppTheme.SurfaceRaised;

            Panel accentBar = new Panel();
            accentBar.BackColor = accent;
            accentBar.Width = 4;
            accentBar.Dock = DockStyle.Left;
            Controls.Add(accentBar);

            Label titleLabel = UiFactory.Label(title.ToUpperInvariant(), AppTheme.Small, AppTheme.TextMuted);
            titleLabel.Location = new Point(18, 17);
            Controls.Add(titleLabel);

            valueLabel = UiFactory.Label(value, new Font("Segoe UI Semibold", 14f, FontStyle.Bold), AppTheme.Text);
            valueLabel.Location = new Point(18, 43);
            valueLabel.MaximumSize = new Size(188, 26);
            valueLabel.AutoEllipsis = true;
            valueLabel.AutoSize = false;
            valueLabel.Size = new Size(188, 26);
            Controls.Add(valueLabel);

            detailLabel = UiFactory.Label(detail, AppTheme.Small, AppTheme.TextMuted);
            detailLabel.Location = new Point(18, 78);
            detailLabel.MaximumSize = new Size(188, 28);
            detailLabel.AutoSize = false;
            detailLabel.Size = new Size(188, 28);
            detailLabel.AutoEllipsis = true;
            Controls.Add(detailLabel);
        }

        public void SetValue(string value, string detail)
        {
            valueLabel.Text = value;
            detailLabel.Text = detail;
        }
    }

    internal sealed class TweakCard : SmoothPanel
    {
        private readonly TweakDefinition definition;
        private readonly ToggleSwitch toggle;
        private readonly Label statusLabel;
        private bool suppressToggle;

        public event Action<TweakDefinition, bool> RequestedChanged;
        public event Action<TweakDefinition> Selected;

        public TweakDefinition Definition { get { return definition; } }

        public TweakCard(TweakDefinition definition, bool applied)
        {
            this.definition = definition;
            Width = 650;
            Height = 128;
            Margin = new Padding(0, 0, 0, 12);
            BackColor = AppTheme.SurfaceRaised;
            Cursor = Cursors.Hand;

            Label title = UiFactory.Label(definition.Title, new Font("Segoe UI Semibold", 11.5f, FontStyle.Bold), AppTheme.Text);
            title.Location = new Point(18, 16);
            title.MaximumSize = new Size(520, 24);
            Controls.Add(title);

            Label summary = UiFactory.Label(definition.Summary, AppTheme.Body, AppTheme.TextMuted);
            summary.Location = new Point(18, 47);
            summary.MaximumSize = new Size(530, 38);
            summary.AutoSize = false;
            summary.Size = new Size(530, 38);
            Controls.Add(summary);

            FlowLayoutPanel badges = new FlowLayoutPanel();
            badges.Location = new Point(18, 91);
            badges.Width = 500;
            badges.Height = 26;
            badges.WrapContents = false;
            badges.BackColor = Color.Transparent;
            badges.Controls.Add(UiFactory.Pill(UiFactory.RiskText(definition.Risk), UiFactory.RiskColor(definition.Risk)));
            badges.Controls.Add(UiFactory.Pill(UiFactory.ImpactText(definition.Impact), AppTheme.Cyan));
            if (definition.RequiresRestart)
            {
                badges.Controls.Add(UiFactory.Pill("YENİDEN BAŞLATMA", AppTheme.Amber));
            }
            Controls.Add(badges);

            toggle = new ToggleSwitch();
            toggle.Location = new Point(580, 28);
            suppressToggle = true;
            toggle.Checked = applied;
            suppressToggle = false;
            toggle.CheckedChanged += Toggle_CheckedChanged;
            Controls.Add(toggle);

            statusLabel = UiFactory.Label(applied ? "Etkin" : "Kapalı", AppTheme.Small, applied ? AppTheme.Green : AppTheme.TextMuted);
            statusLabel.Location = new Point(579, 61);
            statusLabel.Width = 55;
            statusLabel.TextAlign = ContentAlignment.MiddleCenter;
            statusLabel.AutoSize = false;
            statusLabel.Height = 20;
            Controls.Add(statusLabel);

            HookClick(this);
        }

        private void HookClick(Control control)
        {
            control.Click += delegate
            {
                if (Selected != null) Selected(definition);
            };
            foreach (Control child in control.Controls)
            {
                if (!(child is ToggleSwitch)) HookClick(child);
            }
        }

        private void Toggle_CheckedChanged(object sender, EventArgs e)
        {
            if (suppressToggle) return;
            if (RequestedChanged != null) RequestedChanged(definition, toggle.Checked);
        }

        public void UpdateState(bool applied, bool? pending)
        {
            bool shown = pending.HasValue ? pending.Value : applied;
            suppressToggle = true;
            toggle.Checked = shown;
            suppressToggle = false;

            if (pending.HasValue)
            {
                statusLabel.Text = pending.Value ? "Bekliyor: Aç" : "Bekliyor: Kapat";
                statusLabel.ForeColor = AppTheme.Amber;
                statusLabel.Width = 80;
                statusLabel.Left = 559;
                BorderColor = AppTheme.Amber;
            }
            else
            {
                statusLabel.Text = applied ? "Etkin" : "Kapalı";
                statusLabel.ForeColor = applied ? AppTheme.Green : AppTheme.TextMuted;
                statusLabel.Width = 55;
                statusLabel.Left = 579;
                BorderColor = AppTheme.Border;
            }
            Invalidate();
        }
    }

    internal sealed class ApplyReviewDialog : Form
    {
        private readonly CheckBox restorePoint;
        public bool Confirmed { get; private set; }
        public bool CreateRestorePoint { get { return restorePoint.Checked; } }

        public ApplyReviewDialog(IList<KeyValuePair<TweakDefinition, bool>> changes)
        {
            Text = "Değişiklikleri gözden geçir";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(680, 580);
            MinimumSize = new Size(640, 520);
            BackColor = AppTheme.Window;
            ForeColor = AppTheme.Text;
            Font = AppTheme.Body;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            Label title = UiFactory.Label("Uygulanacak değişiklikler", AppTheme.Heading, AppTheme.Text);
            title.Location = new Point(24, 22);
            Controls.Add(title);

            Label description = UiFactory.Label(
                "Tercan her ayarın mevcut değerini yedekleyecek. Aşağıdaki listeyi kontrol edin.",
                AppTheme.Body,
                AppTheme.TextMuted);
            description.Location = new Point(26, 65);
            Controls.Add(description);

            ListView list = new ListView();
            list.Location = new Point(24, 100);
            list.Size = new Size(616, 330);
            list.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            list.View = View.Details;
            list.FullRowSelect = true;
            list.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            list.BackColor = AppTheme.Surface;
            list.ForeColor = AppTheme.Text;
            list.BorderStyle = BorderStyle.FixedSingle;
            list.Columns.Add("İşlem", 80);
            list.Columns.Add("Ayar", 310);
            list.Columns.Add("Risk", 100);
            list.Columns.Add("Etki", 100);
            foreach (KeyValuePair<TweakDefinition, bool> item in changes)
            {
                ListViewItem row = new ListViewItem(item.Value ? "Aç" : "Geri al");
                row.SubItems.Add(item.Key.Title);
                row.SubItems.Add(UiFactory.RiskText(item.Key.Risk));
                row.SubItems.Add(UiFactory.ImpactText(item.Key.Impact));
                if (item.Key.Risk == RiskLevel.Experimental)
                {
                    row.ForeColor = AppTheme.Red;
                }
                else if (item.Key.Risk == RiskLevel.Caution)
                {
                    row.ForeColor = AppTheme.Amber;
                }
                list.Items.Add(row);
            }
            Controls.Add(list);

            restorePoint = new CheckBox();
            restorePoint.Text = "Uygulamadan önce Windows geri yükleme noktası oluştur";
            restorePoint.ForeColor = AppTheme.Text;
            restorePoint.BackColor = Color.Transparent;
            restorePoint.Checked = true;
            restorePoint.AutoSize = true;
            restorePoint.Location = new Point(28, 448);
            restorePoint.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            Controls.Add(restorePoint);

            Label note = UiFactory.Label(
                "Not: Windows, 24 saat içinde ikinci geri yükleme noktasına izin vermeyebilir. Kayıt defteri yedeği yine alınır.",
                AppTheme.Small,
                AppTheme.TextMuted);
            note.Location = new Point(28, 476);
            note.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            Controls.Add(note);

            Button cancel = UiFactory.Button("Vazgeç", AppTheme.SurfaceRaised, AppTheme.Text);
            cancel.AutoSize = false;
            cancel.Size = new Size(90, 38);
            cancel.Location = new Point(378, 510);
            cancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            Button apply = UiFactory.Button("Değişiklikleri uygula", AppTheme.Accent, Color.White);
            apply.AutoSize = false;
            apply.Size = new Size(160, 38);
            apply.Location = new Point(480, 510);
            apply.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            apply.Click += delegate
            {
                Confirmed = true;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(apply);
        }
    }
}
