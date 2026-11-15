using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NetworkSharing.UI
{
    public enum ButtonIcon
    {
        None,
        Host,
        Client,
        Shares,
        Drives,
        Diagnostics,
        Settings,
        ThemeToggle,
        Copy,
        Rocket,
        Refresh,
        Delete,
        Explorer,
        Pin
    }

    public static class UITheme
    {
        public static bool IsDark { get; set; }

        // Light Theme (Clean Modern Windows 11 Slate)
        public static readonly Color LightBg = Color.FromArgb(248, 250, 252);
        public static readonly Color LightCard = Color.FromArgb(255, 255, 255);
        public static readonly Color LightSidebar = Color.FromArgb(255, 255, 255);
        public static readonly Color LightText = Color.FromArgb(15, 23, 42);
        public static readonly Color LightSubText = Color.FromArgb(100, 116, 139);
        public static readonly Color LightBorder = Color.FromArgb(226, 232, 240);
        public static readonly Color LightAccent = Color.FromArgb(0, 103, 192);
        public static readonly Color LightAccentHover = Color.FromArgb(24, 121, 205);
        public static readonly Color LightBadgeBg = Color.FromArgb(238, 242, 255);
        public static readonly Color LightBadgeBorder = Color.FromArgb(199, 210, 254);
        public static readonly Color LightInputBg = Color.FromArgb(248, 250, 252);
        public static readonly Color LightNavHover = Color.FromArgb(241, 245, 249);
        public static readonly Color LightNavActive = Color.FromArgb(228, 240, 254);

        // Dark Theme (Modern Deep Zinc / Charcoal)
        public static readonly Color DarkBg = Color.FromArgb(20, 20, 22);
        public static readonly Color DarkCard = Color.FromArgb(32, 32, 35);
        public static readonly Color DarkSidebar = Color.FromArgb(26, 26, 29);
        public static readonly Color DarkText = Color.FromArgb(244, 244, 245);
        public static readonly Color DarkSubText = Color.FromArgb(161, 161, 170);
        public static readonly Color DarkBorder = Color.FromArgb(55, 55, 62);
        public static readonly Color DarkAccent = Color.FromArgb(56, 189, 248);
        public static readonly Color DarkAccentHover = Color.FromArgb(14, 165, 233);
        public static readonly Color DarkBadgeBg = Color.FromArgb(22, 36, 58);
        public static readonly Color DarkBadgeBorder = Color.FromArgb(49, 75, 120);
        public static readonly Color DarkInputBg = Color.FromArgb(26, 26, 29);
        public static readonly Color DarkNavHover = Color.FromArgb(38, 38, 42);
        public static readonly Color DarkNavActive = Color.FromArgb(40, 50, 68);

        // Status Colors
        public static readonly Color SuccessColor = Color.FromArgb(34, 197, 94);
        public static readonly Color WarningColor = Color.FromArgb(245, 158, 11);
        public static readonly Color DangerColor = Color.FromArgb(239, 68, 68);
        public static readonly Color DangerHover = Color.FromArgb(220, 38, 38);

        // Dynamic Theme Accessors
        public static Color BackgroundColor { get { return IsDark ? DarkBg : LightBg; } }
        public static Color CardColor { get { return IsDark ? DarkCard : LightCard; } }
        public static Color SidebarColor { get { return IsDark ? DarkSidebar : LightSidebar; } }
        public static Color TextColor { get { return IsDark ? DarkText : LightText; } }
        public static Color SubTextColor { get { return IsDark ? DarkSubText : LightSubText; } }
        public static Color BorderColor { get { return IsDark ? DarkBorder : LightBorder; } }
        public static Color AccentColor { get { return IsDark ? DarkAccent : LightAccent; } }
        public static Color AccentHoverColor { get { return IsDark ? DarkAccentHover : LightAccentHover; } }
        public static Color BadgeBgColor { get { return IsDark ? DarkBadgeBg : LightBadgeBg; } }
        public static Color BadgeBorderColor { get { return IsDark ? DarkBadgeBorder : LightBadgeBorder; } }
        public static Color InputBgColor { get { return IsDark ? DarkInputBg : LightInputBg; } }
        public static Color NavHoverColor { get { return IsDark ? DarkNavHover : LightNavHover; } }
        public static Color NavActiveColor { get { return IsDark ? DarkNavActive : LightNavActive; } }

        // Typography
        public static Font HeaderFont = new Font("Segoe UI", 14f, FontStyle.Bold);
        public static Font SubHeaderFont = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        public static Font RegularFont = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        public static Font BoldFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        public static Font SmallFont = new Font("Segoe UI", 8.5f, FontStyle.Regular);

        public static GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;

            if (diameter > rect.Width) diameter = rect.Width;
            if (diameter > rect.Height) diameter = rect.Height;

            if (diameter <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }

            Rectangle arc = new Rectangle(rect.X, rect.Y, diameter, diameter);

            path.AddArc(arc, 180, 90);
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        public static void ApplyTheme(Control parent)
        {
            parent.BackColor = BackgroundColor;
            parent.ForeColor = TextColor;

            foreach (Control c in parent.Controls)
            {
                ModernButton mb = c as ModernButton;
                ModernCard mc = c as ModernCard;
                TextBox tb = c as TextBox;
                ComboBox cb = c as ComboBox;
                ListView lv = c as ListView;
                Panel p = c as Panel;
                Label lbl = c as Label;
                RadioButton rb = c as RadioButton;
                CheckBox chk = c as CheckBox;

                if (mb != null)
                {
                    mb.Invalidate();
                }
                else if (mc != null)
                {
                    mc.BackColor = CardColor;
                    mc.ForeColor = TextColor;
                    mc.Invalidate();
                }
                else if (tb != null)
                {
                    tb.BackColor = InputBgColor;
                    tb.ForeColor = TextColor;
                    tb.BorderStyle = BorderStyle.FixedSingle;
                }
                else if (cb != null)
                {
                    cb.BackColor = InputBgColor;
                    cb.ForeColor = TextColor;
                }
                else if (lv != null)
                {
                    lv.BackColor = CardColor;
                    lv.ForeColor = TextColor;
                    lv.BorderStyle = BorderStyle.FixedSingle;
                    foreach (ListViewItem item in lv.Items)
                    {
                        item.BackColor = CardColor;
                        item.ForeColor = TextColor;
                        foreach (ListViewItem.ListViewSubItem sub in item.SubItems)
                        {
                            sub.BackColor = CardColor;
                            sub.ForeColor = TextColor;
                        }
                    }
                }
                else if (rb != null)
                {
                    rb.ForeColor = TextColor;
                }
                else if (chk != null)
                {
                    chk.ForeColor = TextColor;
                }
                else if (p != null)
                {
                    if (p.Tag != null && p.Tag.ToString() == "Sidebar")
                    {
                        p.BackColor = SidebarColor;
                        p.ForeColor = TextColor;
                    }
                    else if (p.Tag != null && p.Tag.ToString() == "Header")
                    {
                        p.BackColor = SidebarColor;
                        p.ForeColor = TextColor;
                    }
                    else if (p.Tag != null && p.Tag.ToString() == "Badge")
                    {
                        p.BackColor = BadgeBgColor;
                    }
                }
                else if (lbl != null)
                {
                    if (lbl.Tag != null && lbl.Tag.ToString() == "SubText")
                    {
                        lbl.ForeColor = SubTextColor;
                    }
                    else if (lbl.Tag != null && lbl.Tag.ToString() == "Success")
                    {
                        lbl.ForeColor = SuccessColor;
                    }
                    else if (lbl.Tag != null && lbl.Tag.ToString() == "Warning")
                    {
                        lbl.ForeColor = WarningColor;
                    }
                    else if (lbl.Tag != null && lbl.Tag.ToString() == "Accent")
                    {
                        lbl.ForeColor = AccentColor;
                    }
                    else
                    {
                        lbl.ForeColor = TextColor;
                    }
                }

                if (c.HasChildren)
                {
                    ApplyTheme(c);
                }
            }
        }
    }

    /// <summary>
    /// Executive-grade, antialiased, modern Windows 11 Fluent Button.
    /// Completely eliminates wireframe borders, black halo artifacts, and text ghosting.
    /// </summary>
    public class ModernButton : Button
    {
        private bool _isHovered;
        private bool _isPressed;

        public bool IsPrimary { get; set; }
        public bool IsDanger { get; set; }
        public bool IsNav { get; set; }
        public bool IsActiveNav { get; set; }
        public bool IsPill { get; set; }
        public int BorderRadius { get; set; }
        public ButtonIcon IconType { get; set; }

        public ModernButton()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | 
                          ControlStyles.OptimizedDoubleBuffer | 
                          ControlStyles.ResizeRedraw | 
                          ControlStyles.UserPaint, true);

            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.Cursor = Cursors.Hand;
            this.Font = UITheme.BoldFont;
            this.BorderRadius = 6;
            this.Height = 36;
            this.IconType = ButtonIcon.None;
            this.UseMnemonic = false;
        }

        protected override bool ShowFocusCues
        {
            get { return false; }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            this.Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            _isPressed = false;
            this.Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            base.OnMouseDown(mevent);
            if (mevent.Button == MouseButtons.Left)
            {
                _isPressed = true;
                this.Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            base.OnMouseUp(mevent);
            _isPressed = false;
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;

            // 1. MUST CLEAR BUFFER WITH PARENT BACKGROUND COLOR
            // This prevents uninitialized alpha memory, completely eliminating black smears,
            // black rectangular halos, and ghost text overlay bugs!
            Color parentBg = this.Parent != null ? this.Parent.BackColor : (this.IsNav ? UITheme.SidebarColor : UITheme.BackgroundColor);
            g.Clear(parentBg);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            int radius = IsPill ? (this.Height / 2) : BorderRadius;

            Color fillColor;
            Color textColor;
            Color borderColor = Color.Transparent;

            if (!this.Enabled)
            {
                fillColor = UITheme.IsDark ? Color.FromArgb(40, 40, 46) : Color.FromArgb(228, 232, 238);
                textColor = UITheme.SubTextColor;
                borderColor = UITheme.IsDark ? Color.FromArgb(55, 55, 62) : Color.FromArgb(210, 214, 220);
                this.Cursor = Cursors.Default;
            }
            else if (IsNav)
            {
                this.Cursor = Cursors.Hand;
                borderColor = Color.Transparent;

                if (IsActiveNav)
                {
                    fillColor = UITheme.NavActiveColor;
                    textColor = UITheme.AccentColor;
                }
                else if (_isHovered)
                {
                    fillColor = UITheme.NavHoverColor;
                    textColor = UITheme.TextColor;
                }
                else
                {
                    fillColor = parentBg;
                    textColor = UITheme.SubTextColor;
                }
            }
            else if (IconType == ButtonIcon.ThemeToggle)
            {
                this.Cursor = Cursors.Hand;
                if (UITheme.IsDark)
                {
                    fillColor = _isPressed ? Color.FromArgb(50, 50, 56) : (_isHovered ? Color.FromArgb(46, 46, 52) : Color.FromArgb(36, 36, 40));
                    borderColor = Color.FromArgb(70, 70, 78);
                    textColor = Color.FromArgb(244, 244, 245);
                }
                else
                {
                    fillColor = _isPressed ? Color.FromArgb(226, 232, 240) : (_isHovered ? Color.FromArgb(235, 240, 246) : Color.FromArgb(243, 245, 248));
                    borderColor = Color.FromArgb(209, 213, 219);
                    textColor = Color.FromArgb(51, 65, 85);
                }
            }
            else if (IsDanger)
            {
                this.Cursor = Cursors.Hand;
                fillColor = _isPressed ? Color.FromArgb(185, 28, 28) : (_isHovered ? UITheme.DangerHover : UITheme.DangerColor);
                textColor = Color.White;
                borderColor = Color.Transparent;
            }
            else if (IsPrimary)
            {
                this.Cursor = Cursors.Hand;
                fillColor = _isPressed ? Color.FromArgb(0, 85, 160) : (_isHovered ? UITheme.AccentHoverColor : UITheme.AccentColor);
                textColor = UITheme.IsDark ? Color.FromArgb(15, 23, 42) : Color.White;
                borderColor = Color.Transparent;
            }
            else
            {
                this.Cursor = Cursors.Hand;
                // Standard Secondary Button
                fillColor = _isPressed ? UITheme.NavHoverColor : (_isHovered ? UITheme.NavHoverColor : UITheme.CardColor);
                textColor = UITheme.TextColor;
                borderColor = UITheme.BorderColor;
            }

            // Draw filled rounded background
            if (fillColor != parentBg || IsActiveNav || _isHovered || !IsNav)
            {
                using (GraphicsPath path = UITheme.GetRoundedPath(rect, radius))
                {
                    using (SolidBrush brush = new SolidBrush(fillColor))
                    {
                        g.FillPath(brush, path);
                    }

                    if (borderColor != Color.Transparent)
                    {
                        using (Pen pen = new Pen(borderColor, 1f))
                        {
                            g.DrawPath(pen, path);
                        }
                    }
                }
            }

            // Windows 11 Left Accent Indicator for Active Nav Item
            if (IsNav && IsActiveNav)
            {
                int barHeight = Math.Max(16, this.Height - 20);
                int barY = (this.Height - barHeight) / 2;
                Rectangle indicator = new Rectangle(0, barY, 3, barHeight);
                using (GraphicsPath ipath = UITheme.GetRoundedPath(indicator, 1))
                using (SolidBrush ibrush = new SolidBrush(UITheme.AccentColor))
                {
                    g.FillPath(ibrush, ipath);
                }
            }

            // Draw Vector Icon & Text
            int textLeft = 14;
            if (IconType != ButtonIcon.None)
            {
                int iconSize = 16;
                int iconY = (this.Height - iconSize) / 2;
                int iconX = IsNav ? 16 : 12;
                Rectangle iconBox = new Rectangle(iconX, iconY, iconSize, iconSize);

                Color iconColor;
                if (IsPrimary)
                {
                    iconColor = textColor;
                }
                else if (IsNav)
                {
                    iconColor = IsActiveNav ? UITheme.AccentColor : (_isHovered ? UITheme.TextColor : UITheme.SubTextColor);
                }
                else
                {
                    iconColor = textColor;
                }

                DrawVectorIcon(g, IconType, iconBox, iconColor);
                textLeft = iconX + iconSize + 10;
            }

            // Draw Text
            TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
            if (IsNav || IconType == ButtonIcon.ThemeToggle)
            {
                flags |= TextFormatFlags.Left;
                Rectangle textRect = new Rectangle(textLeft, 0, this.Width - textLeft - 6, this.Height);
                TextRenderer.DrawText(g, this.Text, this.Font, textRect, textColor, flags);
            }
            else
            {
                if (IconType != ButtonIcon.None)
                {
                    flags |= TextFormatFlags.Left;
                    Rectangle textRect = new Rectangle(textLeft, 0, this.Width - textLeft, this.Height);
                    TextRenderer.DrawText(g, this.Text, this.Font, textRect, textColor, flags);
                }
                else
                {
                    flags |= TextFormatFlags.HorizontalCenter;
                    TextRenderer.DrawText(g, this.Text, this.Font, rect, textColor, flags);
                }
            }
        }

        private void DrawVectorIcon(Graphics g, ButtonIcon icon, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.6f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                p.LineJoin = LineJoin.Round;

                int x = r.X;
                int y = r.Y;
                int w = r.Width;
                int h = r.Height;

                switch (icon)
                {
                    case ButtonIcon.Host:
                        // Monitor screen and stand
                        g.DrawRectangle(p, x + 1, y + 1, w - 2, h - 6);
                        g.DrawLine(p, x + w / 2, y + h - 5, x + w / 2, y + h - 1);
                        g.DrawLine(p, x + 3, y + h - 1, x + w - 3, y + h - 1);
                        break;

                    case ButtonIcon.Client:
                        // Modern laptop
                        g.DrawRectangle(p, x + 2, y + 2, w - 4, h - 7);
                        g.DrawLine(p, x, y + h - 2, x + w, y + h - 2);
                        break;

                    case ButtonIcon.Shares:
                        // Network shared folder
                        g.DrawLine(p, x + 1, y + 3, x + 5, y + 3);
                        g.DrawLine(p, x + 5, y + 3, x + 7, y + 5);
                        g.DrawLine(p, x + 7, y + 5, x + w - 2, y + 5);
                        g.DrawRectangle(p, x + 1, y + 5, w - 2, h - 7);
                        using (SolidBrush dot = new SolidBrush(color))
                        {
                            g.FillEllipse(dot, x + w - 5, y + h - 5, 3, 3);
                        }
                        break;

                    case ButtonIcon.Drives:
                        // Stacked hard drive
                        g.DrawRectangle(p, x + 1, y + 2, w - 2, 5);
                        g.DrawRectangle(p, x + 1, y + 9, w - 2, 5);
                        using (SolidBrush dot = new SolidBrush(color))
                        {
                            g.FillEllipse(dot, x + w - 5, y + 4, 2, 2);
                            g.FillEllipse(dot, x + w - 5, y + 11, 2, 2);
                        }
                        break;

                    case ButtonIcon.Diagnostics:
                        // Heartbeat EKG Pulse
                        Point[] pulse = new Point[]
                        {
                            new Point(x, y + h / 2),
                            new Point(x + 3, y + h / 2),
                            new Point(x + 6, y + 2),
                            new Point(x + 9, y + h - 2),
                            new Point(x + 12, y + h / 2),
                            new Point(x + w, y + h / 2)
                        };
                        g.DrawLines(p, pulse);
                        break;

                    case ButtonIcon.Settings:
                        // 8-tooth gear
                        g.DrawEllipse(p, x + 4, y + 4, w - 8, h - 8);
                        g.DrawLine(p, x + w / 2, y + 1, x + w / 2, y + 3);
                        g.DrawLine(p, x + w / 2, y + h - 3, x + w / 2, y + h - 1);
                        g.DrawLine(p, x + 1, y + h / 2, x + 3, y + h / 2);
                        g.DrawLine(p, x + w - 3, y + h / 2, x + w - 1, y + h / 2);
                        g.DrawLine(p, x + 3, y + 3, x + 5, y + 5);
                        g.DrawLine(p, x + w - 5, y + h - 5, x + w - 3, y + h - 3);
                        g.DrawLine(p, x + w - 3, y + 3, x + w - 5, y + 5);
                        g.DrawLine(p, x + 5, y + h - 5, x + 3, y + h - 3);
                        break;

                    case ButtonIcon.ThemeToggle:
                        if (UITheme.IsDark)
                        {
                            // Sun vector icon in golden amber
                            Color sunColor = Color.FromArgb(251, 191, 36);
                            using (Pen sunPen = new Pen(sunColor, 1.6f))
                            using (SolidBrush sunBrush = new SolidBrush(sunColor))
                            {
                                sunPen.StartCap = LineCap.Round;
                                sunPen.EndCap = LineCap.Round;
                                g.FillEllipse(sunBrush, x + 4, y + 4, 8, 8);
                                g.DrawLine(sunPen, x + 8, y + 1, x + 8, y + 2.5f);
                                g.DrawLine(sunPen, x + 8, y + 13.5f, x + 8, y + 15);
                                g.DrawLine(sunPen, x + 1, y + 8, x + 2.5f, y + 8);
                                g.DrawLine(sunPen, x + 13.5f, y + 8, x + 15, y + 8);
                                g.DrawLine(sunPen, x + 3, y + 3, x + 4.5f, y + 4.5f);
                                g.DrawLine(sunPen, x + 13, y + 3, x + 11.5f, y + 4.5f);
                                g.DrawLine(sunPen, x + 3, y + 13, x + 4.5f, y + 11.5f);
                                g.DrawLine(sunPen, x + 13, y + 13, x + 11.5f, y + 11.5f);
                            }
                        }
                        else
                        {
                            // Crescent Moon vector icon in indigo
                            Color moonColor = Color.FromArgb(79, 70, 229);
                            using (GraphicsPath moon = new GraphicsPath())
                            {
                                moon.AddArc(x + 2, y + 1, 12, 14, -80, 210);
                                moon.AddArc(x + 5, y + 3, 9, 10, 110, -170);
                                moon.CloseFigure();
                                using (SolidBrush mbrush = new SolidBrush(moonColor))
                                {
                                    g.FillPath(mbrush, moon);
                                }
                            }
                        }
                        break;

                    case ButtonIcon.Rocket:
                        // Launch arrow
                        Point[] arrow = new Point[]
                        {
                            new Point(x + 2, y + h - 3),
                            new Point(x + w - 3, y + 2),
                            new Point(x + 7, y + 2),
                            new Point(x + w - 3, y + 2),
                            new Point(x + w - 3, y + h - 7)
                        };
                        g.DrawLines(p, arrow);
                        break;

                    case ButtonIcon.Refresh:
                        // Circular arrows
                        g.DrawArc(p, x + 2, y + 2, w - 4, h - 4, 30, 280);
                        g.DrawLine(p, x + w - 2, y + 4, x + w - 2, y + 9);
                        g.DrawLine(p, x + w - 2, y + 4, x + w - 7, y + 4);
                        break;

                    case ButtonIcon.Copy:
                        // Dual overlapping sheets
                        g.DrawRectangle(p, x + 4, y + 4, w - 6, h - 6);
                        g.DrawLine(p, x + 1, y + h - 4, x + 1, y + 1);
                        g.DrawLine(p, x + 1, y + 1, x + w - 4, y + 1);
                        break;

                    case ButtonIcon.Explorer:
                        // Open folder arrow
                        g.DrawRectangle(p, x + 1, y + 4, w - 3, h - 6);
                        g.DrawLine(p, x + 5, y + 1, x + 10, y + 1);
                        break;

                    case ButtonIcon.Pin:
                        // Link / Pin icon
                        g.DrawRectangle(p, x + 2, y + 2, w - 4, h - 4);
                        g.DrawLine(p, x + 4, y + h - 4, x + w - 4, y + 4);
                        g.DrawLine(p, x + w - 7, y + 4, x + w - 4, y + 4);
                        g.DrawLine(p, x + w - 4, y + 7, x + w - 4, y + 4);
                        break;

                    case ButtonIcon.Delete:
                        // Trash / X
                        g.DrawLine(p, x + 3, y + 3, x + w - 3, y + h - 3);
                        g.DrawLine(p, x + w - 3, y + 3, x + 3, y + h - 3);
                        break;

                    default:
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Clean rounded card surface with antialiased borders.
    /// Clears outer background so rounded corners blend perfectly without black fringes.
    /// </summary>
    public class ModernCard : Panel
    {
        public int Radius { get; set; }

        public ModernCard(int radius = 8)
        {
            this.Radius = radius;
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | 
                          ControlStyles.OptimizedDoubleBuffer | 
                          ControlStyles.ResizeRedraw, true);

            this.BackColor = UITheme.CardColor;
            this.Padding = new Padding(20);
            this.Margin = new Padding(0, 0, 0, 16);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            Color parentBg = this.Parent != null ? this.Parent.BackColor : UITheme.BackgroundColor;
            g.Clear(parentBg);

            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            using (GraphicsPath path = UITheme.GetRoundedPath(rect, Radius))
            {
                using (SolidBrush brush = new SolidBrush(UITheme.CardColor))
                {
                    g.FillPath(brush, path);
                }

                using (Pen pen = new Pen(UITheme.BorderColor, 1f))
                {
                    g.DrawPath(pen, path);
                }
            }
        }
    }
}
