/*
    Copyright (c) 2017 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

namespace BluePointLilac.Controls
{
    public sealed partial class LoadingDialog : Form
    {
        private const int CsDropShadow = 0x20000, DefaultWidth = 408, DefaultHeight = 45;
        private const int ProgressBarWidth = 391, ProgressBarHeight = 25, PaddingH = 8, PaddingV = 10;

        private readonly LoadingDialogInterface _controller;
        private readonly Action<LoadingDialogInterface> _action;
        private Point _offset;
        private ContentAlignment _ownerAlignment;
        private bool _startAutomatically;
        private NewProgressBar progressBar;
        private Panel panel1;

        internal LoadingDialog(string title, Action<LoadingDialogInterface> action)
        {
            InitializeComponent();
            Text = title;
            ForeColor = DarkModeHelper.FormFore;
            BackColor = DarkModeHelper.FormBack;
            UseWaitCursor = true;
            _action = action;
            _controller = new LoadingDialogInterface(this);
            DarkModeHelper.ThemeChanged += OnThemeChanged;
        }

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ClassStyle |= CsDropShadow; return cp; }
        }

        public static Form DefaultOwner { get; set; }
        public Exception Error { get; private set; }
        internal ProgressBar ProgressBar => progressBar;

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _controller.SignalDialogReady();
            if (Owner != null)
            {
                Owner.Move += OwnerOnMove;
                Owner.Resize += OwnerOnMove;
                OwnerOnMove(this, e);
            }
            if (_startAutomatically) StartWork();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (Owner != null) { Owner.Move -= OwnerOnMove; Owner.Resize -= OwnerOnMove; }
            base.OnClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e) { _controller.Abort = true; base.OnFormClosed(e); }

        private void OwnerOnMove(object sender, EventArgs e)
        {
            if (Owner == null) return;
            var pos = CalculatePosition();
            Location = new Point(pos.X + _offset.X, pos.Y + _offset.Y);
        }

        private Point CalculatePosition()
        {
            var r = Owner.Bounds;
            var s = Size;
            var (h, v) = GetAlignment(_ownerAlignment);
            return new Point(
                h == 0 ? r.X : h == 1 ? r.X + (r.Width - s.Width) / 2 : r.Right - s.Width,
                v == 0 ? r.Y : v == 1 ? r.Y + (r.Height - s.Height) / 2 : r.Bottom - s.Height);
        }

        private static (int, int) GetAlignment(ContentAlignment a) => a switch
        {
            ContentAlignment.TopLeft => (0, 0), ContentAlignment.TopCenter => (1, 0), ContentAlignment.TopRight => (2, 0),
            ContentAlignment.MiddleLeft => (0, 1), ContentAlignment.MiddleCenter => (1, 1), ContentAlignment.MiddleRight => (2, 1),
            ContentAlignment.BottomLeft => (0, 2), ContentAlignment.BottomCenter => (1, 2), ContentAlignment.BottomRight => (2, 2),
            _ => (1, 1)
        };

        public static LoadingDialog Show(Form owner, string title, Action<LoadingDialogInterface> action,
            Point offset = default, ContentAlignment ownerAlignment = ContentAlignment.MiddleCenter)
        {
            owner = GetTopmostOwner(owner);
            var loadBar = CreateLoadingDialog(owner, title, action, offset, ownerAlignment);
            loadBar.Show(loadBar.Owner);
            return loadBar;
        }

        public static Exception ShowDialog(Form owner, string title, Action<LoadingDialogInterface> action,
            Point offset = default, ContentAlignment ownerAlignment = ContentAlignment.MiddleCenter)
        {
            using var loadBar = CreateLoadingDialog(owner, title, action, offset, ownerAlignment);
            loadBar._startAutomatically = true;
            loadBar.ShowDialog(loadBar.Owner);
            return loadBar.Error;
        }

        private static LoadingDialog CreateLoadingDialog(Form owner, string title,
            Action<LoadingDialogInterface> action, Point offset, ContentAlignment alignment)
            => new(title, action) { _offset = offset, _ownerAlignment = alignment, Owner = owner, StartPosition = FormStartPosition.Manual };

        private static Form GetTopmostOwner(Form owner)
        {
            owner ??= DefaultOwner;
            while (owner?.OwnedForms.Length > 0) owner = owner.OwnedForms[0];
            return owner;
        }

        public void StartWork() => ThreadPool.QueueUserWorkItem(static state => ((LoadingDialog)state!).ExecuteAction(), this);
        private void panel1_Resize(object sender, EventArgs e) => Size = panel1.Size;

        private void ExecuteAction()
        {
            _controller.WaitTillDialogIsReady();
            try { _action(_controller); } catch (Exception ex) { Error = ex; } finally { _controller.CloseDialog(); }
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing) return;
            ForeColor = DarkModeHelper.FormFore;
            BackColor = DarkModeHelper.FormBack;
            panel1.BackColor = DarkModeHelper.FormBack;
            Invalidate();
        }

        private void InitializeComponent()
        {
            progressBar = new NewProgressBar();
            panel1 = new Panel();
            panel1.SuspendLayout();
            SuspendLayout();

            progressBar.Location = new Point(PaddingH, PaddingV);
            progressBar.Size = new Size(ProgressBarWidth, ProgressBarHeight);
            progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            panel1.AutoSize = true;
            panel1.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panel1.BorderStyle = BorderStyle.FixedSingle;
            panel1.Controls.Add(progressBar);
            panel1.Dock = DockStyle.Fill;
            panel1.MinimumSize = new Size(DefaultWidth, DefaultHeight);
            panel1.Padding = new Padding(PaddingH, PaddingV, PaddingH, PaddingV);
            panel1.Size = new Size(DefaultWidth, DefaultHeight);
            panel1.Resize += panel1_Resize;

            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(DefaultWidth, DefaultHeight);
            Controls.Add(panel1);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;

            panel1.ResumeLayout(false);
            ResumeLayout(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DarkModeHelper.ThemeChanged -= OnThemeChanged;
                _controller?.Dispose();
                progressBar?.Dispose();
                panel1?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public sealed class LoadingDialogInterface : IDisposable
    {
        private const int UpdateInterval = 35;
        private const byte OpNone = 0, OpProgress = 1, OpMaximum = 2, OpMinimum = 3;

        private readonly System.Windows.Forms.Timer _updateTimer;
        private int _pendingValue, _pendingMinOrMax;
        private byte _pendingOperation;
        private readonly ManualResetEventSlim _dialogReadyEvent = new(false);
        private bool _disposed;

        internal LoadingDialogInterface(LoadingDialog dialog)
        {
            Dialog = dialog;
            _updateTimer = new System.Windows.Forms.Timer { Interval = UpdateInterval };
            _updateTimer.Tick += OnTimerTick;
            _updateTimer.Start();
        }

        public volatile bool Abort;
        private LoadingDialog Dialog { get; }

        internal void SignalDialogReady() => _dialogReadyEvent.Set();

        public void CloseDialog()
        {
            _updateTimer.Stop();
            SafeInvoke(() => { if (!Dialog.IsDisposed) Dialog.Close(); });
        }

        public void SetMaximum(int value) { _pendingMinOrMax = value; Volatile.Write(ref _pendingOperation, OpMaximum); }
        public void SetMinimum(int value) { _pendingMinOrMax = value; Volatile.Write(ref _pendingOperation, OpMinimum); }
        public void SetProgress(int value, bool forceNoAnimation = false)
        {
            _pendingValue = (value << 1) | (forceNoAnimation ? 1 : 0);
            Volatile.Write(ref _pendingOperation, OpProgress);
        }
        public void SetTitle(string newTitle) => SafeInvoke(() => Dialog.Text = newTitle);

        internal void WaitTillDialogIsReady() => _dialogReadyEvent.Wait();

        private void OnTimerTick(object sender, EventArgs e)
        {
            var op = Volatile.Read(ref _pendingOperation);
            if (op == OpNone) return;
            Volatile.Write(ref _pendingOperation, OpNone);

            var pb = Dialog.ProgressBar;
            if (pb == null || pb.IsDisposed) return;

            if (op == OpProgress)
            {
                var packed = _pendingValue;
                ApplyProgressValue(pb, packed >> 1, (packed & 1) == 1);
            }
            else if (op == OpMaximum) pb.Maximum = _pendingMinOrMax;
            else if (op == OpMinimum) pb.Minimum = _pendingMinOrMax;
        }

        private static void ApplyProgressValue(ProgressBar pb, int value, bool forceNoAnimation)
        {
            try
            {
                if (pb.Value == value) return;
                if (value < pb.Minimum || value > pb.Maximum) { pb.Style = ProgressBarStyle.Marquee; return; }
                pb.Style = ProgressBarStyle.Blocks;
                if (forceNoAnimation && value < pb.Maximum) pb.Value = value + 1;
                pb.Value = value;
            }
            catch { pb.Style = ProgressBarStyle.Marquee; }
        }

        private void SafeInvoke(Action action)
        {
            if (Dialog.IsDisposed || Dialog.Disposing) return;
            if (Dialog.InvokeRequired) { try { Dialog.Invoke(action); } catch { } }
            else action();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _updateTimer?.Dispose();
            _dialogReadyEvent?.Dispose();
        }
    }

    public class NewProgressBar : ProgressBar
    {
        private const int Inset = 1, CornerRadius = 6;

        private static readonly Color Fg1 = Color.FromArgb(255, 195, 0), Fg2 = Color.FromArgb(255, 140, 26), Fg3 = Color.FromArgb(255, 195, 0);
        private static readonly Color[] FgColors = { Fg1, Fg2, Fg3 };
        private static readonly Color[] DarkBg = { Color.FromArgb(80, 80, 80), Color.FromArgb(60, 60, 60), Color.FromArgb(80, 80, 80) };
        private static readonly Color[] LightBg = { Color.FromArgb(220, 220, 220), Color.FromArgb(180, 180, 180), Color.FromArgb(220, 220, 220) };
        private static readonly float[] Positions = { 0f, 0.5f, 1f };
        private static readonly ColorBlend FgBlend = new() { Colors = FgColors, Positions = Positions };
        private static readonly ColorBlend DarkBlend = new() { Colors = DarkBg, Positions = Positions };
        private static readonly ColorBlend LightBlend = new() { Colors = LightBg, Positions = Positions };
        private static readonly Point Origin = new(0, 0);

        private ColorBlend _bgBlend;
        private Color _backColor;

        public NewProgressBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            UpdateColors();
            DarkModeHelper.ThemeChanged += OnThemeChanged;
        }

        private void UpdateColors()
        {
            _bgBlend = DarkModeHelper.IsDarkTheme ? DarkBlend : LightBlend;
            _backColor = DarkModeHelper.FormBack;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            using (var b = new SolidBrush(_backColor)) g.FillRectangle(b, ClientRectangle);

            using (var p = CreateRoundedRect(new Rectangle(0, 0, Width, Height), CornerRadius))
            using (var b = new LinearGradientBrush(Origin, new Point(0, Height), _bgBlend.Colors[0], _bgBlend.Colors[2]))
            {
                b.InterpolationColors = _bgBlend;
                g.FillPath(b, p);
            }

            if (Maximum <= 0) return;
            var w = (int)((Width - 2 * Inset) * ((double)Value / Maximum));
            if (w <= 0) return;

            var rect = new Rectangle(Inset, Inset, Math.Max(w, CornerRadius), Height - 2 * Inset);
            using var fp = CreateRoundedRect(rect, CornerRadius - 1);
            using var fb = new LinearGradientBrush(new Point(0, rect.Top), new Point(0, rect.Bottom), Fg1, Fg3);
            fb.InterpolationColors = FgBlend;
            g.FillPath(fb, fp);
        }

        private static GraphicsPath CreateRoundedRect(Rectangle r, int rad)
        {
            var path = new GraphicsPath();
            if (rad <= 0) { path.AddRectangle(r); return path; }

            rad = Math.Min(rad, Math.Min(r.Width, r.Height) / 2);
            var d = rad * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void OnThemeChanged(object sender, EventArgs e) { UpdateColors(); Invalidate(); }
        protected override void Dispose(bool disposing) { if (disposing) DarkModeHelper.ThemeChanged -= OnThemeChanged; base.Dispose(disposing); }
    }
}
