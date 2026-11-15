using System;
using System.Drawing;
using System.Windows.Forms;
using NetworkSharing.Core;
using NetworkSharing.Logging;

namespace NetworkSharing.UI
{
    public class MainDashboard : Form
    {
        private Panel _pnlHeader;
        private Panel _pnlSidebar;
        private Panel _pnlContent;
        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _statusLabel;
        private ToolStripStatusLabel _ipLabel;

        private Label _lblLogo;
        private Label _lblHostBadge;
        private ModernButton _btnThemeToggle;

        private ModernButton _btnNavHost;
        private ModernButton _btnNavClient;
        private ModernButton _btnNavSpeed;
        private ModernButton _btnNavSettings;

        private HostSetupControl _ctrlHost;
        private ClientConnectControl _ctrlClient;
        private DiagnosticsControl _ctrlDiagnostics;
        private SettingsControl _ctrlSettings;

        private Panel _pnlDivider;

        public MainDashboard(NetworkSharing.Privileges.DispatchedOperation initialOp = null)
        {
            InitializeComponent();
            ApplyTheme();

            if (initialOp != null && initialOp.OpType == NetworkSharing.Privileges.PrivilegedOperationType.FullRollback)
            {
                SwitchView(_ctrlSettings, _btnNavSettings);
            }
            else
            {
                SwitchView(_ctrlHost, _btnNavHost);
            }
        }

        private void InitializeComponent()
        {
            this.Text = "Network Sharing Manager – Windows Network Drive & Folder Sharing";
            this.Size = new Size(1160, 780);
            this.MinimumSize = new Size(980, 640);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = UITheme.RegularFont;

            string hostIp = NetworkManager.GetLocalIpAddress();
            string compName = NetworkManager.GetComputerName();

            // -------------------------------------------------------------
            // 1. Bottom Status Bar (Dock = Bottom)
            // -------------------------------------------------------------
            _statusStrip = new StatusStrip
            {
                Dock = DockStyle.Bottom,
                Height = 28
            };
            _statusLabel = new ToolStripStatusLabel { Text = "Ready. SMB File Sharing service active." };
            _ipLabel = new ToolStripStatusLabel { Text = "Local IPv4: " + hostIp, Alignment = ToolStripItemAlignment.Right };
            _statusStrip.Items.Add(_statusLabel);
            _statusStrip.Items.Add(_ipLabel);

            // -------------------------------------------------------------
            // 2. Top Header (Dock = Top, full window width)
            // -------------------------------------------------------------
            _pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                Tag = "Header"
            };
            _pnlHeader.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(UITheme.BorderColor, 1))
                {
                    e.Graphics.DrawLine(pen, 0, _pnlHeader.Height - 1, _pnlHeader.Width, _pnlHeader.Height - 1);
                }
            };
            _pnlHeader.Resize += (s, e) =>
            {
                if (_btnThemeToggle != null)
                {
                    _btnThemeToggle.Location = new Point(_pnlHeader.ClientSize.Width - 145, 14);
                }
            };

            _lblLogo = new Label
            {
                Text = "NetworkSharing",
                Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
                ForeColor = UITheme.AccentColor,
                AutoSize = true,
                Location = new Point(20, 16),
                UseMnemonic = false
            };
            _pnlHeader.Controls.Add(_lblLogo);

            bool isAdmin = Program.IsAdministrator();
            _lblHostBadge = new Label
            {
                Text = string.Format(" ● Host: {0} ({1})   •   SMB 2/3 {2} ", compName, hostIp, isAdmin ? "Active (Admin)" : "Ready (Client Mode)"),
                Font = UITheme.BoldFont,
                AutoSize = true,
                Padding = new Padding(12, 6, 12, 6),
                Location = new Point(235, 14),
                BackColor = UITheme.BadgeBgColor,
                ForeColor = UITheme.AccentColor,
                UseMnemonic = false
            };
            _pnlHeader.Controls.Add(_lblHostBadge);

            _btnThemeToggle = new ModernButton
            {
                Text = UITheme.IsDark ? "Light Mode" : "Dark Mode",
                IconType = ButtonIcon.ThemeToggle,
                Width = 125,
                Height = 32,
                Location = new Point(1160 - 165, 14),
                IsPill = true,
                BorderRadius = 16
            };
            _btnThemeToggle.Click += OnThemeToggleClick;
            _pnlHeader.Controls.Add(_btnThemeToggle);

            // -------------------------------------------------------------
            // 3. Left Navigation Sidebar (Dock = Left, Width = 220)
            // -------------------------------------------------------------
            _pnlSidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 220,
                Tag = "Sidebar"
            };
            _pnlSidebar.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(UITheme.BorderColor, 1))
                {
                    e.Graphics.DrawLine(pen, _pnlSidebar.Width - 1, 0, _pnlSidebar.Width - 1, _pnlSidebar.Height);
                }
            };

            // Top Navigation Flow Area
            FlowLayoutPanel pnlTopNav = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0),
                Padding = new Padding(12, 14, 12, 0)
            };

            _btnNavHost = CreateNavButton("Host Sharing", ButtonIcon.Host);
            _btnNavClient = CreateNavButton("Client Connect", ButtonIcon.Client);
            _btnNavSpeed = CreateNavButton("Speed & Ping Test", ButtonIcon.Rocket);
            _btnNavSettings = CreateNavButton("Settings & Rollback", ButtonIcon.Settings);

            pnlTopNav.Controls.Add(_btnNavHost);
            pnlTopNav.Controls.Add(_btnNavClient);
            pnlTopNav.Controls.Add(_btnNavSpeed);
            _pnlSidebar.Controls.Add(pnlTopNav);

            // Bottom Settings Panel
            Panel pnlBottomNav = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 54,
                Padding = new Padding(12, 4, 12, 10)
            };
            _btnNavSettings.Dock = DockStyle.Fill;
            pnlBottomNav.Controls.Add(_btnNavSettings);
            _pnlSidebar.Controls.Add(pnlBottomNav);

            // Divider above Settings
            _pnlDivider = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 12
            };
            _pnlDivider.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(UITheme.BorderColor, 1))
                {
                    e.Graphics.DrawLine(pen, 16, 5, _pnlDivider.Width - 16, 5);
                }
            };
            _pnlSidebar.Controls.Add(_pnlDivider);

            // -------------------------------------------------------------
            // 4. Main Content Workspace (Dock = Fill)
            // -------------------------------------------------------------
            _pnlContent = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0)
            };

            // Add to Form in precise Z-order (Fill added first)
            this.Controls.Add(_pnlContent);
            this.Controls.Add(_pnlSidebar);
            this.Controls.Add(_pnlHeader);
            this.Controls.Add(_statusStrip);

            // Initialize child views
            _ctrlHost = new HostSetupControl { Dock = DockStyle.Fill };
            _ctrlHost.ShareCreated += () =>
            {
                _statusLabel.Text = "Share created successfully.";
            };

            _ctrlClient = new ClientConnectControl { Dock = DockStyle.Fill };
            _ctrlClient.DriveMapped += () =>
            {
                _statusLabel.Text = "Drive mapped successfully.";
            };

            _ctrlDiagnostics = new DiagnosticsControl { Dock = DockStyle.Fill };

            _ctrlSettings = new SettingsControl { Dock = DockStyle.Fill };
            _ctrlSettings.ThemeChanged += ApplyTheme;
            _ctrlSettings.ConfigurationReset += () =>
            {
                _ctrlHost.RefreshActiveShares();
                _ctrlClient.RefreshMappedDrives();
                _statusLabel.Text = "Sharing configuration cleanly rolled back.";
            };

            // Wire nav events
            _btnNavHost.Click += (s, e) => SwitchView(_ctrlHost, _btnNavHost);
            _btnNavClient.Click += (s, e) => SwitchView(_ctrlClient, _btnNavClient);
            _btnNavSpeed.Click += (s, e) => SwitchView(_ctrlDiagnostics, _btnNavSpeed);
            _btnNavSettings.Click += (s, e) => SwitchView(_ctrlSettings, _btnNavSettings);
        }

        private ModernButton CreateNavButton(string text, ButtonIcon icon)
        {
            ModernButton btn = new ModernButton
            {
                Text = text,
                IconType = icon,
                Width = 196,
                Height = 40,
                BorderRadius = 6,
                IsNav = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            return btn;
        }

        private void SwitchView(UserControl view, ModernButton activeBtn)
        {
            _pnlContent.Controls.Clear();
            _pnlContent.Controls.Add(view);

            ModernButton[] allNav = { _btnNavHost, _btnNavClient, _btnNavSpeed, _btnNavSettings };
            foreach (var b in allNav)
            {
                b.IsActiveNav = (b == activeBtn);
                b.Invalidate();
            }
        }

        private void OnThemeToggleClick(object sender, EventArgs e)
        {
            UITheme.IsDark = !UITheme.IsDark;
            AppConfig.Settings.IsDarkMode = UITheme.IsDark;
            AppConfig.Save();
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            UITheme.IsDark = AppConfig.Settings.IsDarkMode;
            _btnThemeToggle.Text = UITheme.IsDark ? "Light Mode" : "Dark Mode";

            this.BackColor = UITheme.BackgroundColor;
            this.ForeColor = UITheme.TextColor;

            _pnlHeader.BackColor = UITheme.SidebarColor;
            _pnlHeader.ForeColor = UITheme.TextColor;

            _lblLogo.ForeColor = UITheme.AccentColor;

            _lblHostBadge.BackColor = UITheme.BadgeBgColor;
            _lblHostBadge.ForeColor = UITheme.AccentColor;

            _pnlSidebar.BackColor = UITheme.SidebarColor;
            _pnlSidebar.ForeColor = UITheme.TextColor;

            _statusStrip.BackColor = UITheme.SidebarColor;
            _statusStrip.ForeColor = UITheme.TextColor;

            if (_ctrlSettings != null)
            {
                _ctrlSettings.SyncThemeCheckbox();
            }

            UITheme.ApplyTheme(this);
            this.Invalidate(true);
        }
    }
}
