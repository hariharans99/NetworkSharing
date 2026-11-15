using System;
using System.Drawing;
using System.Windows.Forms;
using NetworkSharing.Recovery;

namespace NetworkSharing.UI
{
    public class SettingsControl : UserControl
    {
        private FlowLayoutPanel _pnlFlow;
        private Panel _pnlHeader;
        private ModernCard _card1;
        private ModernCard _card2;

        private CheckBox _chkDarkMode;
        private CheckBox _chkAutoReconnect;
        private ModernButton _btnFullRollback;
        private Label _lblWarning;

        public event Action ThemeChanged;
        public event Action ConfigurationReset;

        public SettingsControl()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.Padding = new Padding(0);

            _pnlFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(28, 20, 28, 24)
            };
            _pnlFlow.Resize += OnFlowResize;

            _pnlHeader = new Panel { Height = 58, Margin = new Padding(0, 0, 0, 10) };
            Label lblTitle = new Label
            {
                Text = "Settings & Rollback Manager",
                Font = UITheme.HeaderFont,
                AutoSize = true,
                Location = new Point(0, 0),
                UseMnemonic = false
            };
            Label lblSubtitle = new Label
            {
                Text = "Configure application preferences or perform a clean rollback of all sharing settings.",
                Font = UITheme.RegularFont,
                ForeColor = UITheme.SubTextColor,
                Tag = "SubText",
                AutoSize = true,
                Location = new Point(0, 28),
                UseMnemonic = false
            };
            _pnlHeader.Controls.Add(lblSubtitle);
            _pnlHeader.Controls.Add(lblTitle);
            _pnlFlow.Controls.Add(_pnlHeader);

            // Card 1: Appearance & Startup
            _card1 = new ModernCard { Height = 140 };

            Label lblGen = new Label { Text = "GENERAL PREFERENCES", Font = UITheme.SubHeaderFont, ForeColor = UITheme.AccentColor, Location = new Point(18, 14), AutoSize = true, UseMnemonic = false };
            _card1.Controls.Add(lblGen);

            _chkDarkMode = new CheckBox
            {
                Text = "Enable Dark Mode (Fluent Dark Theme)",
                Location = new Point(18, 48),
                Width = 500,
                Font = UITheme.RegularFont,
                Checked = AppConfig.Settings.IsDarkMode
            };
            _chkDarkMode.CheckedChanged += OnThemeToggle;
            _card1.Controls.Add(_chkDarkMode);

            _chkAutoReconnect = new CheckBox
            {
                Text = "Keep network drives persistent and reconnect automatically on Windows reboot",
                Location = new Point(18, 84),
                Width = 600,
                Font = UITheme.RegularFont,
                Checked = AppConfig.Settings.AutoReconnectOnStartup
            };
            _chkAutoReconnect.CheckedChanged += (s, e) =>
            {
                AppConfig.Settings.AutoReconnectOnStartup = _chkAutoReconnect.Checked;
                AppConfig.Save();
            };
            _card1.Controls.Add(_chkAutoReconnect);
            _pnlFlow.Controls.Add(_card1);

            // Card 2: Rollback / Uninstall
            _card2 = new ModernCard { Height = 200 };

            Label lblClean = new Label { Text = "CLEAN ROLLBACK & CONFIGURATION REMOVAL", Font = UITheme.SubHeaderFont, ForeColor = UITheme.DangerColor, Location = new Point(18, 14), AutoSize = true, UseMnemonic = false };
            _card2.Controls.Add(lblClean);

            _lblWarning = new Label
            {
                Text = "When you perform a clean rollback:\n" +
                       "• Removes ONLY shares and firewall rules created by NetworkSharing.exe.\n" +
                       "• Restores modified file permissions and removes the dedicated sharing user account.\n" +
                       "• Unmounts network drives and clears stored credentials.\n" +
                       "• NEVER deletes your files or user data.",
                Location = new Point(18, 46),
                Height = 85,
                Font = UITheme.RegularFont,
                ForeColor = UITheme.SubTextColor,
                Tag = "SubText",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                UseMnemonic = false
            };
            _card2.Controls.Add(_lblWarning);

            _btnFullRollback = new ModernButton
            {
                Text = "Rollback All & Remove Sharing Configuration",
                IconType = ButtonIcon.Delete,
                Location = new Point(18, 142),
                Width = 380,
                Height = 40,
                IsDanger = true,
                BorderRadius = 6
            };
            _btnFullRollback.Click += OnFullRollbackClick;
            _card2.Controls.Add(_btnFullRollback);

            _pnlFlow.Controls.Add(_card2);
            this.Controls.Add(_pnlFlow);
        }

        private void OnFlowResize(object sender, EventArgs e)
        {
            int targetWidth = Math.Max(_pnlFlow.ClientSize.Width - 65, 550);

            _pnlHeader.Width = targetWidth;
            _card1.Width = targetWidth;
            _card2.Width = targetWidth;
            _lblWarning.Width = targetWidth - 36;
        }

        private void OnThemeToggle(object sender, EventArgs e)
        {
            AppConfig.Settings.IsDarkMode = _chkDarkMode.Checked;
            UITheme.IsDark = _chkDarkMode.Checked;
            AppConfig.Save();
            if (ThemeChanged != null) ThemeChanged();
        }

        public void SyncThemeCheckbox()
        {
            _chkDarkMode.Checked = UITheme.IsDark;
        }

        private void OnFullRollbackClick(object sender, EventArgs e)
        {
            if (!Program.IsAdministrator())
            {
                DialogResult elevDr = MessageBox.Show(
                    "Performing a full rollback requires Administrator privileges to remove local SMB shares, clean firewall rules, and delete sharing user accounts.\n\n" +
                    "Would you like to restart NetworkSharing with Administrator privileges now?",
                    "Administrator Privileges Required",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (elevDr == DialogResult.Yes)
                {
                    NetworkSharing.Privileges.ElevationManager.RequestElevation("rollback");
                }
                return;
            }

            DialogResult dr = MessageBox.Show(
                "Are you sure you want to perform a full rollback?\n\n" +
                "This will safely remove all application-created SMB shares, unmap all drives, " +
                "remove the 'ShareUser' account, and delete the custom firewall rules.\n\n" +
                "Your actual files on disk will NOT be touched.",
                "Confirm Full Rollback", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (dr == DialogResult.Yes)
            {
                _btnFullRollback.Enabled = false;
                _btnFullRollback.Text = "Rolling Back Configuration...";

                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    bool success = UninstallManager.PerformFullRollback();
                    this.BeginInvoke(new Action(() =>
                    {
                        _btnFullRollback.Enabled = true;
                        _btnFullRollback.Text = "Rollback All & Remove Sharing Configuration";

                        if (success)
                        {
                            MessageBox.Show("All sharing configuration has been cleanly removed and restored.", "Rollback Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            if (ConfigurationReset != null) ConfigurationReset();
                        }
                        else
                        {
                            MessageBox.Show("Rollback completed with warnings. Check the Diagnostics tab for logs.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }));
                });
            }
        }
    }
}
