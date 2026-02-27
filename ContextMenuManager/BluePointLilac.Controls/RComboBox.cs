using BluePointLilac.Controls;
using BluePointLilac.Methods;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ContextMenuManager.BluePointLilac.Controls
{
    public sealed partial class RComboBox : ComboBox
    {
        #region Fields
        private int _originalSelectedIndex = -1;
        private bool _mouseOverDropDown, _focused;
        private readonly Timer _animTimer;
        private float _borderWidth = 1.2f, _targetWidth = 1.2f, _hoverProgress;
        private Color _currentBorder, _targetBorder;
        private int _animatedIndex = -1, _previousAnimatedIndex = -1;
        private IntPtr _dropDownHwnd;
        #endregion

        #region Properties
        [DefaultValue(8), Category("Style")]
        public int BorderRadius { get; set; } = 8;

        private Color _hoverColor = Color.FromArgb(255, 145, 60);
        [DefaultValue(typeof(Color), "255, 145, 60"), Category("Style")]
        public Color HoverColor
        {
            get => _hoverColor;
            set { _hoverColor = value; DropDownHoverColor = value; }
        }

        private Color _focusColor = Color.FromArgb(255, 107, 0);
        [DefaultValue(typeof(Color), "255, 107, 0"), Category("Style")]
        public Color FocusColor
        {
            get => _focusColor;
            set { _focusColor = value; DropDownSelectedColor = value; }
        }

        [DefaultValue(typeof(Color), "100, 100, 100"), Category("Style")]
        public Color ArrowColor { get; set; } = Color.FromArgb(100, 100, 100);

        [DefaultValue(true)]
        public new bool AutoSize { get; set; } = true;

        private int _minWidth = 120;
        [DefaultValue(120), Category("Style")]
        public int MinWidth { get => _minWidth.DpiZoom(); set => _minWidth = value; }

        private int _maxWidth = 400;
        [DefaultValue(400), Category("Style")]
        public int MaxWidth { get => _maxWidth.DpiZoom(); set => _maxWidth = value; }

        private int _textPadding = 5;
        [DefaultValue(5), Category("Style")]
        public int TextPadding { get => _textPadding.DpiZoom(); set => _textPadding = value; }

        [Category("DropDown"), DefaultValue(32)]
        public int DropDownItemHeight { get; set; } = 32;

        [Category("DropDown")]
        public Font DropDownFont { get; set; }

        [Category("DropDown")]
        public Color DropDownHoverColor { get; set; }

        [Category("DropDown"), DefaultValue(typeof(Color), "White")]
        public Color DropDownHoverForeColor { get; set; } = Color.White;

        [Category("DropDown")]
        public Color DropDownSelectedColor { get; set; }

        [Category("DropDown"), DefaultValue(typeof(Color), "White")]
        public Color DropDownSelectedForeColor { get; set; } = Color.White;

        [Category("DropDown")]
        public Color DropDownForeColor { get; set; }
        #endregion

        public RComboBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
            DrawMode = DrawMode.OwnerDrawVariable;
            DropDownStyle = ComboBoxStyle.DropDownList;
            FlatStyle = FlatStyle.Flat;
            Height = 32.DpiZoom();
            _animTimer = new Timer { Interval = 16 };
            InitEvents();
        }

        private void InitEvents()
        {
            DarkModeHelper.ThemeChanged += OnThemeChanged;
            GotFocus += (s, e) => { _focused = true; UpdateState(); };
            LostFocus += (s, e) => { _focused = false; UpdateState(); };
            MouseEnter += (s, e) => UpdateState();
            MouseLeave += (s, e) => { _mouseOverDropDown = false; UpdateState(); };
            MouseMove += (s, e) => UpdateDropDownHoverState(e.Location);
            MouseDown += (s, e) => { if (e.Button == MouseButtons.Left && !DroppedDown) { if (!Focused) Focus(); DroppedDown = true; } };
            SelectedIndexChanged += (s, e) => { if (AutoSize) AdjustWidth(); };
            TextChanged += (s, e) => { if (AutoSize) AdjustWidth(); };
            DropDown += OnDropDown;
            DropDownClosed += (s, e) => { _originalSelectedIndex = _animatedIndex = _previousAnimatedIndex = -1; _dropDownHwnd = IntPtr.Zero; };
            _animTimer.Tick += OnAnimTick;
            _animTimer.Start();
        }

        private void OnAnimTick(object s, EventArgs e)
        {
            if (IsDisposed) { _animTimer.Stop(); return; }
            bool redraw = false;
            if (Math.Abs(_borderWidth - _targetWidth) > 0.01f) { _borderWidth += (_targetWidth - _borderWidth) * 0.3f; redraw = true; }
            if (_currentBorder != _targetBorder) { _currentBorder = ColorLerp(_currentBorder, _targetBorder, 0.25f); redraw = true; }
            if (redraw) Invalidate();
            UpdateDropDownAnimation();
        }

        private void OnDropDown(object s, EventArgs e)
        {
            _originalSelectedIndex = SelectedIndex;
            DropDownHeight = Items.Count * DropDownItemHeight.DpiZoom() + 2 * SystemInformation.BorderSize.Height;
            if (Parent?.FindForm() is not Form form) return;
            form.BeginInvoke(() =>
            {
                try
                {
                    if (IsDisposed || !IsHandleCreated) return;
                    var cbi = new COMBOBOXINFO { cbSize = Marshal.SizeOf<COMBOBOXINFO>() };
                    if (!GetComboBoxInfo(Handle, ref cbi) || cbi.hwndList == IntPtr.Zero) return;
                    _dropDownHwnd = cbi.hwndList;
                    if (!GetWindowRect(cbi.hwndList, out RECT rect)) return;
                    var hrgn = CreateRoundRectRgn(0, 0, rect.Right - rect.Left, rect.Bottom - rect.Top, BorderRadius.DpiZoom(), BorderRadius.DpiZoom());
                    if (hrgn != IntPtr.Zero && SetWindowRgn(cbi.hwndList, hrgn, true) == 0) DeleteObject(hrgn);
                }
                catch { }
            });
        }

        private void UpdateDropDownAnimation()
        {
            if (DroppedDown && _dropDownHwnd != IntPtr.Zero) UpdateHoverIndex();
            else if (_animatedIndex != -1) { _previousAnimatedIndex = _animatedIndex; _animatedIndex = -1; _hoverProgress = 0; }
            if (_hoverProgress < 1f)
            {
                _hoverProgress = Math.Min(1f, _hoverProgress + 0.1f);
                if (_dropDownHwnd != IntPtr.Zero) InvalidateRect(_dropDownHwnd, IntPtr.Zero, true);
            }
            else _previousAnimatedIndex = -1;
        }

        private void UpdateHoverIndex()
        {
            GetCursorPos(out POINT pt);
            GetWindowRect(_dropDownHwnd, out RECT rc);
            var rect = new Rectangle(rc.Left, rc.Top, rc.Right - rc.Left, rc.Bottom - rc.Top);
            int idx = -1;
            if (rect.Contains(pt.X, pt.Y))
            {
                idx = (pt.Y - rect.Top) / DropDownItemHeight.DpiZoom();
                if (idx < 0 || idx >= Items.Count || idx == _originalSelectedIndex) idx = -1;
            }
            if (_animatedIndex != idx) { _previousAnimatedIndex = _animatedIndex; _animatedIndex = idx; _hoverProgress = 0; }
        }

        protected override void OnMeasureItem(MeasureItemEventArgs e)
        {
            base.OnMeasureItem(e);
            e.ItemHeight = DropDownItemHeight.DpiZoom();
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected ||
                            (DroppedDown && _animatedIndex == -1 && e.Index == _originalSelectedIndex);
            var (back, fore) = GetItemColors(e.Index, selected);
            using (var brush = new SolidBrush(back))
            using (var path = DarkModeHelper.CreateRoundedRectanglePath(new Rectangle(e.Bounds.X + 2, e.Bounds.Y + 2, e.Bounds.Width - 4, e.Bounds.Height - 4), 4))
                e.Graphics.FillPath(brush, path);
            TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), DropDownFont ?? Font,
                new Rectangle(e.Bounds.Left + TextPadding, e.Bounds.Top, e.Bounds.Width - TextPadding * 2, e.Bounds.Height),
                fore, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }

        private (Color Back, Color Fore) GetItemColors(int index, bool selected)
        {
            if (index == _animatedIndex) return (ColorLerp(BackColor, DropDownHoverColor, _hoverProgress), ColorLerp(DropDownForeColor, DropDownHoverForeColor, _hoverProgress));
            if (index == _previousAnimatedIndex) return (ColorLerp(DropDownHoverColor, BackColor, _hoverProgress), ColorLerp(DropDownHoverForeColor, DropDownForeColor, _hoverProgress));
            if (selected) return (DropDownSelectedColor, DropDownSelectedForeColor);
            return (BackColor, DropDownForeColor);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? SystemColors.Control);
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = DarkModeHelper.CreateRoundedRectanglePath(rect, BorderRadius.DpiZoom()))
            {
                using (var brush = new SolidBrush(BackColor)) g.FillPath(brush, path);
                using (var pen = new Pen(_currentBorder, _borderWidth)) g.DrawPath(pen, path);
            }
            var btnRect = GetDropDownButtonRect();
            var text = string.IsNullOrEmpty(Text) && SelectedItem != null ? GetItemText(SelectedItem) : Text;
            TextRenderer.DrawText(g, text, Font, new Rectangle(TextPadding, 0, Width - btnRect.Width - TextPadding * 2, Height),
                ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            var c = new Point(btnRect.Left + btnRect.Width / 2, btnRect.Top + btnRect.Height / 2);
            int s = 6.DpiZoom();
            using (var brush = new SolidBrush(_mouseOverDropDown || _focused ? FocusColor : ArrowColor))
                g.FillPolygon(brush, new[] { new Point(c.X - s, c.Y - s / 2), new Point(c.X + s, c.Y - s / 2), new Point(c.X, c.Y + s / 2) });
        }

        private void UpdateState()
        {
            bool hover = _mouseOverDropDown || ClientRectangle.Contains(PointToClient(MousePosition));
            _targetBorder = _focused ? FocusColor : (hover ? HoverColor : DarkModeHelper.ComboBoxBorder);
            _targetWidth = _focused || hover ? 2f : 1.2f;
            Invalidate();
        }

        private void UpdateDropDownHoverState(Point loc)
        {
            bool hover = GetDropDownButtonRect().Contains(loc);
            if (_mouseOverDropDown == hover) return;
            _mouseOverDropDown = hover;
            Cursor = hover ? Cursors.Hand : Cursors.Default;
            UpdateState();
        }

        private Rectangle GetDropDownButtonRect() => new(ClientRectangle.Right - (SystemInformation.HorizontalScrollBarArrowWidth + 8.DpiZoom()),
            ClientRectangle.Top, SystemInformation.HorizontalScrollBarArrowWidth + 8.DpiZoom(), ClientRectangle.Height);

        private void AdjustWidth()
        {
            if (!AutoSize) return;
            int w = Items.Count == 0 && string.IsNullOrEmpty(Text) ? MinWidth :
                TextRenderer.MeasureText(string.IsNullOrEmpty(Text) ? GetItemText(SelectedItem) : Text, Font).Width + TextPadding * 2 + GetDropDownButtonRect().Width;
            Width = Math.Max(MinWidth, Math.Min(MaxWidth, w));
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            if (AutoSize) AdjustWidth();
            UpdateColors();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            if (AutoSize) AdjustWidth();
        }

        public void UpdateColors()
        {
            if (IsDisposed) return;
            SafeInvoke(() =>
            {
                BackColor = DarkModeHelper.ComboBoxBack;
                ForeColor = DarkModeHelper.ComboBoxFore;
                DropDownForeColor = DarkModeHelper.IsDarkTheme ? Color.White : Color.Black;
                _currentBorder = _targetBorder = DarkModeHelper.ComboBoxBorder;
                ArrowColor = DarkModeHelper.ComboBoxArrow;
                DropDownHoverColor = DarkModeHelper.ComboBoxHover;
                DropDownSelectedColor = DarkModeHelper.ComboBoxSelected;
                HoverColor = FocusColor = DarkModeHelper.MainColor;
            });
        }

        private void OnThemeChanged(object s, EventArgs e) => UpdateColors();

        private void SafeInvoke(Action action)
        {
            if (!IsHandleCreated) return;
            if (InvokeRequired) Invoke(action); else action();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { DarkModeHelper.ThemeChanged -= OnThemeChanged; _animTimer?.Dispose(); }
            base.Dispose(disposing);
        }

        private static Color ColorLerp(Color c1, Color c2, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb((int)(c1.A + (c2.A - c1.A) * t), (int)(c1.R + (c2.R - c1.R) * t),
                                  (int)(c1.G + (c2.G - c1.G) * t), (int)(c1.B + (c2.B - c1.B) * t));
        }

        public void AutosizeDropDownWidth()
        {
            int max = 0;
            foreach (var item in Items) max = Math.Max(max, TextRenderer.MeasureText(item?.ToString() ?? string.Empty, Font).Width);
            DropDownWidth = max + SystemInformation.VerticalScrollBarWidth;
        }

        #region Win32
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);
        [DllImport("user32.dll")] private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);
        [DllImport("user32.dll")] private static extern bool GetComboBoxInfo(IntPtr hwnd, ref COMBOBOXINFO pcbi);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int w, int h);
        [DllImport("user32.dll")] private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct COMBOBOXINFO { public int cbSize; public RECT rcItem, rcButton; public int stateButton; public IntPtr hwndCombo, hwndItem, hwndList; }
        #endregion
    }
}
