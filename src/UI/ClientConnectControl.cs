using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetworkSharing.Core;
using NetworkSharing.Logging;
using NetworkSharing.Security;

namespace NetworkSharing.UI
{
    public class ClientConnectControl : UserControl
    {
        private FlowLayoutPanel _pnlFlow;
        private Panel _pnlHeader;
        private ModernCard _card1;
        private ModernCard _card2;
        private ModernCard _card3;
        private TableLayoutPanel _pnlActions;

        private TextBox _txtHost;
        private ModernButton _btnScanNetwork;
        private ModernButton _btnTestConnection;
        private Label _lblConnStatus;

        private TextBox _txtUser;
        private TextBox _txtPass;
        private CheckBox _chkShowPass;
        private CheckBox _chkRemember;
        private ModernButton _btnClearOldCreds;

        private ModernButton _btnBrowseShares;
        private ComboBox _cmbShares;
        private ComboBox _cmbDriveLetters;
        private CheckBox _chkPersistent;

        private ModernButton _btnConnectAndMap;
        private ModernButton _btnOpenExplorer;
        private ModernButton _btnDesktopShortcut;
        private ProgressBar _scanProgress;

        // Mapped Drives & Disconnect
        private ModernCard _cardMappedDrives;
        private ListView _lvMappedDrives;
        private ModernButton _btnDisconnectDrive;
        private ModernButton _btnDisconnectAllDrives;
        private ModernButton _btnResetAllSessions;
        private ModernButton _btnRefreshMapped;

        public event Action DriveMapped;

        public ClientConnectControl()
        {
            InitializeComponent();
            LoadDriveLetters();
            RefreshMappedDrives();
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

            // -------------------------------------------------------------
            // Page Header
            // -------------------------------------------------------------
            _pnlHeader = new Panel { Height = 58, Margin = new Padding(0, 0, 0, 10) };
            Label lblTitle = new Label
            {
                Text = "Client Connection – Connect to Network Drives",
                Font = UITheme.HeaderFont,
                AutoSize = true,
                Location = new Point(0, 0)
            };
            Label lblSubtitle = new Label
            {
                Text = "Discover shared drives on your local network, authenticate, and map them to local drive letters in 1 click.",
                Font = UITheme.RegularFont,
                ForeColor = UITheme.SubTextColor,
                Tag = "SubText",
                AutoSize = true,
                Location = new Point(0, 28)
            };
            _pnlHeader.Controls.Add(lblSubtitle);
            _pnlHeader.Controls.Add(lblTitle);
            _pnlFlow.Controls.Add(_pnlHeader);

            // -------------------------------------------------------------
            // CARD 1: TARGET HOST
            // -------------------------------------------------------------
            _card1 = new ModernCard { Height = 140 };

            Label lblStep1 = new Label
            {
                Text = "STEP 1: TARGET COMPUTER / HOST",
                Font = UITheme.SubHeaderFont,
                ForeColor = UITheme.AccentColor,
                AutoSize = true,
                Location = new Point(18, 14)
            };
            _card1.Controls.Add(lblStep1);

            Label lblHost = new Label
            {
                Text = "Host IP or Name:",
                Font = UITheme.BoldFont,
                Location = new Point(18, 48),
                AutoSize = true
            };
            _card1.Controls.Add(lblHost);

            _txtHost = new TextBox
            {
                Location = new Point(155, 45),
                Height = 28,
                Font = UITheme.RegularFont,
                Text = "192.168.0.128",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _txtHost.TextChanged += OnHostChanged;
            _card1.Controls.Add(_txtHost);

            _btnTestConnection = new ModernButton
            {
                Text = "Test Connection",
                Height = 29,
                Width = 135,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BorderRadius = 6
            };
            _btnTestConnection.Click += OnTestConnectionClick;
            _card1.Controls.Add(_btnTestConnection);

            _btnScanNetwork = new ModernButton
            {
                Text = "Scan Local Wi-Fi",
                Height = 29,
                Width = 145,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BorderRadius = 6
            };
            _btnScanNetwork.Click += OnScanNetworkClick;
            _card1.Controls.Add(_btnScanNetwork);

            _lblConnStatus = new Label
            {
                Text = "● Ready to probe connection.",
                Location = new Point(155, 82),
                AutoSize = true,
                Font = UITheme.SmallFont,
                ForeColor = UITheme.SubTextColor,
                Tag = "SubText"
            };
            _card1.Controls.Add(_lblConnStatus);

            _scanProgress = new ProgressBar
            {
                Location = new Point(18, 108),
                Height = 14,
                Visible = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _card1.Controls.Add(_scanProgress);
            _pnlFlow.Controls.Add(_card1);

            // -------------------------------------------------------------
            // CARD 2: AUTHENTICATION
            // -------------------------------------------------------------
            _card2 = new ModernCard { Height = 185 };

            Label lblStep2 = new Label
            {
                Text = "STEP 2: AUTHENTICATION CREDENTIALS",
                Font = UITheme.SubHeaderFont,
                ForeColor = UITheme.AccentColor,
                AutoSize = true,
                Location = new Point(18, 14)
            };
            _card2.Controls.Add(lblStep2);

            TableLayoutPanel gridAuth = new TableLayoutPanel
            {
                Location = new Point(18, 44),
                Height = 125,
                ColumnCount = 3,
                RowCount = 4,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            gridAuth.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            gridAuth.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            gridAuth.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));

            gridAuth.Controls.Add(new Label { Text = "Username:", Font = UITheme.BoldFont, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            _txtUser = new TextBox { Dock = DockStyle.Fill, Font = UITheme.RegularFont, Text = "ShareUser", Margin = new Padding(0, 4, 8, 4) };
            gridAuth.Controls.Add(_txtUser, 1, 0);
            gridAuth.Controls.Add(new Label { Text = "(Configured on host)", Font = UITheme.SmallFont, ForeColor = UITheme.SubTextColor, Tag = "SubText", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 2, 0);

            gridAuth.Controls.Add(new Label { Text = "Password:", Font = UITheme.BoldFont, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
            _txtPass = new TextBox { Dock = DockStyle.Fill, Font = UITheme.RegularFont, UseSystemPasswordChar = true, Margin = new Padding(0, 4, 8, 4) };
            gridAuth.Controls.Add(_txtPass, 1, 1);

            _chkShowPass = new CheckBox { Text = "Show Password", Dock = DockStyle.Fill, Font = UITheme.SmallFont, Margin = new Padding(4, 4, 0, 0) };
            _chkShowPass.CheckedChanged += (s, e) => _txtPass.UseSystemPasswordChar = !_chkShowPass.Checked;
            gridAuth.Controls.Add(_chkShowPass, 2, 1);

            _chkRemember = new CheckBox
            {
                Text = "Save credentials securely in Windows Credential Manager (Auto-reconnect without password prompt)",
                Dock = DockStyle.Fill,
                Font = UITheme.RegularFont,
                Checked = true,
                Margin = new Padding(0, 4, 0, 0)
            };
            gridAuth.Controls.Add(_chkRemember, 1, 2);
            gridAuth.SetColumnSpan(_chkRemember, 2);

            // Row 3: Clear Old Device Credentials & Reset Button
            FlowLayoutPanel pnlPurge = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 4, 0, 0)
            };

            _btnClearOldCreds = new ModernButton
            {
                Text = "🧹 Clear Old Credentials & Reset Session",
                Width = 275,
                Height = 28,
                BorderRadius = 6,
                Margin = new Padding(0, 0, 10, 0)
            };
            _btnClearOldCreds.Click += OnClearOldCredsClick;
            pnlPurge.Controls.Add(_btnClearOldCreds);

            Label lblPurgeTip = new Label
            {
                Text = "← Click if username or password was changed on the host PC",
                Font = UITheme.SmallFont,
                ForeColor = UITheme.SubTextColor,
                Tag = "SubText",
                AutoSize = true,
                Margin = new Padding(0, 6, 0, 0)
            };
            pnlPurge.Controls.Add(lblPurgeTip);

            gridAuth.Controls.Add(pnlPurge, 1, 3);
            gridAuth.SetColumnSpan(pnlPurge, 2);

            _card2.Controls.Add(gridAuth);
            _pnlFlow.Controls.Add(_card2);

            // -------------------------------------------------------------
            // CARD 3: SHARES & DRIVE LETTER
            // -------------------------------------------------------------
            _card3 = new ModernCard { Height = 140 };

            Label lblStep3 = new Label
            {
                Text = "STEP 3: SELECT SHARE & ASSIGN DRIVE LETTER",
                Font = UITheme.SubHeaderFont,
                ForeColor = UITheme.AccentColor,
                AutoSize = true,
                Location = new Point(18, 14)
            };
            _card3.Controls.Add(lblStep3);

            TableLayoutPanel gridShare = new TableLayoutPanel
            {
                Location = new Point(18, 44),
                Height = 80,
                ColumnCount = 4,
                RowCount = 2,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            gridShare.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            gridShare.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            gridShare.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            gridShare.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));

            gridShare.Controls.Add(new Label { Text = "Select Share:", Font = UITheme.BoldFont, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);

            _cmbShares = new ComboBox { Dock = DockStyle.Fill, Font = UITheme.RegularFont, DropDownStyle = ComboBoxStyle.DropDown, Margin = new Padding(0, 4, 8, 4) };
            _cmbShares.Items.AddRange(new object[] { "Drive_D", "Drive_C", "SharedFiles" });
            if (_cmbShares.Items.Count > 0) _cmbShares.SelectedIndex = 0;
            gridShare.Controls.Add(_cmbShares, 1, 0);

            _btnBrowseShares = new ModernButton { Text = "Discover Shares", Dock = DockStyle.Fill, Margin = new Padding(0, 2, 8, 4), BorderRadius = 6 };
            _btnBrowseShares.Click += OnBrowseSharesClick;
            gridShare.Controls.Add(_btnBrowseShares, 2, 0);

            FlowLayoutPanel pnlDriveLetter = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0) };
            pnlDriveLetter.Controls.Add(new Label { Text = "Drive: ", Font = UITheme.BoldFont, AutoSize = true, Margin = new Padding(0, 6, 2, 0) });
            _cmbDriveLetters = new ComboBox { Width = 75, Font = UITheme.RegularFont, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 4, 0, 0) };
            pnlDriveLetter.Controls.Add(_cmbDriveLetters);
            gridShare.Controls.Add(pnlDriveLetter, 3, 0);

            _chkPersistent = new CheckBox
            {
                Text = "Reconnect automatically after computer reboot (persistent network mapping)",
                Dock = DockStyle.Fill,
                Checked = true,
                Font = UITheme.RegularFont,
                Margin = new Padding(0, 4, 0, 0)
            };
            gridShare.Controls.Add(_chkPersistent, 1, 1);
            gridShare.SetColumnSpan(_chkPersistent, 3);

            _card3.Controls.Add(gridShare);
            _pnlFlow.Controls.Add(_card3);

            // -------------------------------------------------------------
            // ACTION BUTTONS
            // -------------------------------------------------------------
            _pnlActions = new TableLayoutPanel
            {
                Height = 48,
                ColumnCount = 3,
                Margin = new Padding(0, 6, 0, 16)
            };
            _pnlActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _pnlActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            _pnlActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

            _btnConnectAndMap = new ModernButton
            {
                Text = "Connect & Map Drive in 1-Click",
                IconType = ButtonIcon.Rocket,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                Margin = new Padding(0, 0, 8, 0),
                IsPrimary = true,
                BorderRadius = 8
            };
            _btnConnectAndMap.Click += OnConnectAndMapClick;
            _pnlActions.Controls.Add(_btnConnectAndMap, 0, 0);

            _btnOpenExplorer = new ModernButton
            {
                Text = "Open Explorer",
                IconType = ButtonIcon.Explorer,
                Dock = DockStyle.Fill,
                Font = UITheme.BoldFont,
                Margin = new Padding(0, 0, 8, 0),
                BorderRadius = 8
            };
            _btnOpenExplorer.Click += (s, e) =>
            {
                string target = _txtHost.Text.Trim();
                if (!string.IsNullOrEmpty(target))
                {
                    DriveMapper.OpenInExplorer(@"\\" + target);
                }
                else
                {
                    MessageBox.Show("Please enter a host IP address or computer name first.", "Host Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            _pnlActions.Controls.Add(_btnOpenExplorer, 1, 0);

            _btnDesktopShortcut = new ModernButton
            {
                Text = "Desktop Shortcut",
                IconType = ButtonIcon.Pin,
                Dock = DockStyle.Fill,
                Font = UITheme.BoldFont,
                BorderRadius = 8
            };
            _btnDesktopShortcut.Click += (s, e) =>
            {
                string target = _txtHost.Text.Trim();
                if (!string.IsNullOrEmpty(target))
                {
                    string path = @"\\" + target;
                    if (_cmbShares.SelectedItem != null && !string.IsNullOrEmpty(_cmbShares.SelectedItem.ToString()))
                    {
                        path += @"\" + _cmbShares.SelectedItem.ToString();
                    }
                    DriveMapper.CreateDesktopShortcut("Network Drives (" + target + ")", path);
                    MessageBox.Show("Desktop shortcut created successfully!", "Shortcut Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Please enter a host IP address or computer name first.", "Host Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            _pnlActions.Controls.Add(_btnDesktopShortcut, 2, 0);
            _pnlFlow.Controls.Add(_pnlActions);

            // -------------------------------------------------------------
            // ACTIVE MAPPED DRIVES & DISCONNECT CARD
            // -------------------------------------------------------------
            _cardMappedDrives = new ModernCard { Height = 210 };

            Label lblMappedHdr = new Label
            {
                Text = "ACTIVE MAPPED NETWORK DRIVES – DISCONNECT",
                Font = UITheme.SubHeaderFont,
                ForeColor = UITheme.AccentColor,
                AutoSize = true,
                Location = new Point(18, 14),
                UseMnemonic = false
            };
            _cardMappedDrives.Controls.Add(lblMappedHdr);

            _lvMappedDrives = new ListView
            {
                Location = new Point(18, 42),
                Height = 110,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                GridLines = true,
                Font = UITheme.RegularFont,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _lvMappedDrives.Columns.Add("Drive Letter", 120);
            _lvMappedDrives.Columns.Add("Remote Network Share Path", 320);
            _lvMappedDrives.Columns.Add("Status", 140);
            _cardMappedDrives.Controls.Add(_lvMappedDrives);

            FlowLayoutPanel pnlDriveActions = new FlowLayoutPanel
            {
                Location = new Point(18, 160),
                Height = 40,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _btnDisconnectDrive = new ModernButton
            {
                Text = "Disconnect Selected",
                Width = 175,
                Height = 34,
                BorderRadius = 6,
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnDisconnectDrive.Click += OnDisconnectDriveClick;
            pnlDriveActions.Controls.Add(_btnDisconnectDrive);

            _btnDisconnectAllDrives = new ModernButton
            {
                Text = "Disconnect ALL",
                IconType = ButtonIcon.Delete,
                Width = 150,
                Height = 34,
                IsDanger = true,
                BorderRadius = 6,
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnDisconnectAllDrives.Click += OnDisconnectAllDrivesClick;
            pnlDriveActions.Controls.Add(_btnDisconnectAllDrives);

            _btnResetAllSessions = new ModernButton
            {
                Text = "🧹 Reset ALL Sessions & Keys",
                Width = 230,
                Height = 34,
                IsDanger = true,
                BorderRadius = 6,
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnResetAllSessions.Click += OnResetAllSessionsClick;
            pnlDriveActions.Controls.Add(_btnResetAllSessions);

            _btnRefreshMapped = new ModernButton
            {
                Text = "Refresh",
                IconType = ButtonIcon.Refresh,
                Width = 110,
                Height = 34,
                BorderRadius = 6
            };
            _btnRefreshMapped.Click += (s, e) => RefreshMappedDrives();
            pnlDriveActions.Controls.Add(_btnRefreshMapped);

            _cardMappedDrives.Controls.Add(pnlDriveActions);
            _pnlFlow.Controls.Add(_cardMappedDrives);

            this.Controls.Add(_pnlFlow);
        }

        private void OnFlowResize(object sender, EventArgs e)
        {
            int targetWidth = Math.Max(_pnlFlow.ClientSize.Width - 65, 550);

            _pnlHeader.Width = targetWidth;
            _card1.Width = targetWidth;
            _card2.Width = targetWidth;
            _card3.Width = targetWidth;
            _pnlActions.Width = targetWidth;
            _cardMappedDrives.Width = targetWidth;

            // Align Card 1 controls
            _txtHost.Width = targetWidth - 155 - 135 - 145 - 24;
            _btnTestConnection.Location = new Point(targetWidth - 145 - 135 - 12, 44);
            _btnScanNetwork.Location = new Point(targetWidth - 145 - 4, 44);
            _scanProgress.Width = targetWidth - 36;
            _lvMappedDrives.Width = targetWidth - 36;
        }

        public void RefreshMappedDrives()
        {
            if (_lvMappedDrives == null) return;
            _lvMappedDrives.Items.Clear();
            List<MappedDriveInfo> drives = DriveMapper.GetMappedDrives();
            foreach (var d in drives)
            {
                ListViewItem lvi = new ListViewItem(d.DriveLetter);
                lvi.SubItems.Add(d.RemotePath);
                lvi.SubItems.Add(d.Status);
                lvi.Tag = d;
                _lvMappedDrives.Items.Add(lvi);
            }
        }

        private void OnDisconnectDriveClick(object sender, EventArgs e)
        {
            if (_lvMappedDrives.SelectedItems.Count > 0)
            {
                string letter = _lvMappedDrives.SelectedItems[0].SubItems[0].Text;
                DriveMapper.UnmapDrive(letter);
                RefreshMappedDrives();
                LoadDriveLetters();
                MessageBox.Show(string.Format("Network drive {0} has been disconnected.", letter), "Disconnected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Please select a mapped drive letter to disconnect.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OnDisconnectAllDrivesClick(object sender, EventArgs e)
        {
            DialogResult dr = MessageBox.Show(
                "Are you sure you want to disconnect ALL mapped network drives?",
                "Confirm Disconnect All", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (dr == DialogResult.Yes)
            {
                DriveMapper.DisconnectAll();
                RefreshMappedDrives();
                LoadDriveLetters();
                MessageBox.Show("All network drives have been disconnected.", "Disconnected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void LoadDriveLetters()
        {
            _cmbDriveLetters.Items.Clear();
            List<string> free = DriveMapper.GetAvailableDriveLetters();
            foreach (string letter in free)
            {
                _cmbDriveLetters.Items.Add(letter);
            }
            if (_cmbDriveLetters.Items.Count > 0)
            {
                _cmbDriveLetters.SelectedIndex = 0;
            }
        }

        private void OnHostChanged(object sender, EventArgs e)
        {
            string host = _txtHost.Text.Trim();
            if (string.IsNullOrEmpty(host)) return;

            var cred = CredentialManager.GetCredential(host);
            if (cred != null)
            {
                _txtUser.Text = cred.UserName;
                _txtPass.Text = cred.Password;
                _lblConnStatus.Text = "✔ Saved credentials found in Windows Credential Manager.";
                _lblConnStatus.ForeColor = UITheme.SuccessColor;
            }
        }

        private void OnTestConnectionClick(object sender, EventArgs e)
        {
            string host = _txtHost.Text.Trim();
            var val = SecurityValidator.ValidateHost(host);
            if (!val.IsValid)
            {
                MessageBox.Show(val.Message, "Invalid Host", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _btnTestConnection.Enabled = false;
            _lblConnStatus.Text = "Probing connection to " + host + "...";
            _lblConnStatus.ForeColor = UITheme.SubTextColor;

            Task.Factory.StartNew(() =>
            {
                try
                {
                    bool pingOk = NetworkManager.PingHost(host, 1200);
                    bool smbOk = NetworkManager.TestSmbPort(host, 1500);

                    this.BeginInvoke(new Action(() =>
                    {
                        if (smbOk)
                        {
                            _lblConnStatus.Text = "✔ Host is online! SMB file sharing port 445 is open and reachable.";
                            _lblConnStatus.ForeColor = UITheme.SuccessColor;
                        }
                        else if (pingOk)
                        {
                            _lblConnStatus.Text = "⚠ Host responds to ping, but SMB port 445 is closed or blocked by firewall.";
                            _lblConnStatus.ForeColor = UITheme.WarningColor;
                        }
                        else
                        {
                            _lblConnStatus.Text = "✖ Host is unreachable. Ensure both computers are on the same Wi-Fi / LAN.";
                            _lblConnStatus.ForeColor = UITheme.DangerColor;
                        }
                    }));
                }
                finally
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        _btnTestConnection.Enabled = true;
                    }));
                }
            });
        }

        private void OnScanNetworkClick(object sender, EventArgs e)
        {
            _btnScanNetwork.Enabled = false;
            _scanProgress.Visible = true;
            _scanProgress.Value = 0;
            _lblConnStatus.Text = "Scanning local subnet for active SMB hosts...";
            _lblConnStatus.ForeColor = UITheme.SubTextColor;

            Task.Factory.StartNew(() =>
            {
                List<NetworkHostInfo> hosts = NetworkManager.ScanLocalSubnet((scanned, total) =>
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        _scanProgress.Value = (scanned * 100) / total;
                    }));
                });

                this.BeginInvoke(new Action(() =>
                {
                    _scanProgress.Visible = false;
                    _btnScanNetwork.Enabled = true;

                    if (hosts.Count > 0)
                    {
                        string target = hosts[0].IpAddress;
                        _txtHost.Text = target;
                        _lblConnStatus.Text = string.Format("✔ Discovered {0} active SMB host(s). Selected {1}.", hosts.Count, target);
                        _lblConnStatus.ForeColor = UITheme.SuccessColor;
                    }
                    else
                    {
                        _lblConnStatus.Text = "Subnet scan completed. No active SMB hosts responded.";
                        _lblConnStatus.ForeColor = UITheme.WarningColor;
                    }
                }));
            });
        }

        private void OnBrowseSharesClick(object sender, EventArgs e)
        {
            string host = _txtHost.Text.Trim();
            if (string.IsNullOrEmpty(host))
            {
                MessageBox.Show("Please enter a host IP or hostname first.", "Host Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _btnBrowseShares.Enabled = false;
            _btnBrowseShares.Text = "Discovering...";

            Task.Factory.StartNew(() =>
            {
                List<string> shares = ShareManager.GetRemoteShares(host);
                this.BeginInvoke(new Action(() =>
                {
                    _btnBrowseShares.Enabled = true;
                    _btnBrowseShares.Text = "Discover Shares";

                    if (shares.Count > 0)
                    {
                        _cmbShares.Items.Clear();
                        foreach (string s in shares) _cmbShares.Items.Add(s);
                        _cmbShares.SelectedIndex = 0;
                        MessageBox.Show(string.Format("Found {0} available share(s) on {1}.", shares.Count, host), "Shares Discovered", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("No public shares discovered or credentials required first.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }));
            });
        }

        private void OnConnectAndMapClick(object sender, EventArgs e)
        {
            string host = _txtHost.Text.Trim();
            string user = _txtUser.Text.Trim();
            string pass = _txtPass.Text;
            string share = _cmbShares.Text.Trim();
            string driveLetter = _cmbDriveLetters.SelectedItem != null ? _cmbDriveLetters.SelectedItem.ToString() : "Z:";
            bool persistent = _chkPersistent.Checked;
            bool remember = _chkRemember.Checked;

            if (string.IsNullOrEmpty(host))
            {
                MessageBox.Show("Please enter a valid host IP or computer name.", "Host Required", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (string.IsNullOrEmpty(share))
            {
                MessageBox.Show("Please select or enter a share name (e.g. Drive_D).", "Share Name Required", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string remotePath = string.Format(@"\\{0}\{1}", host, share);

            _btnConnectAndMap.Enabled = false;
            _btnConnectAndMap.Text = "Connecting & Mapping...";

            Task.Factory.StartNew(() =>
            {
                if (remember && !string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass))
                {
                    CredentialManager.SaveCredential(host, user, pass);
                }

                MappingResult mapResult = DriveMapper.MapDrive(driveLetter, remotePath, user, pass, persistent);

                this.BeginInvoke(new Action(() =>
                {
                    _btnConnectAndMap.Enabled = true;
                    _btnConnectAndMap.Text = "Connect & Map Drive in 1-Click";

                    if (mapResult.Success)
                    {
                        MessageBox.Show(string.Format("Drive {0} successfully mapped to:\n{1}\n\nOpening File Explorer...", driveLetter, remotePath),
                            "Connection Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        DriveMapper.OpenInExplorer(driveLetter + @"\");
                        LoadDriveLetters();
                        RefreshMappedDrives();
                        if (DriveMapped != null) DriveMapped();
                    }
                    else if (mapResult.IsCredentialConflict)
                    {
                        string conflictMsg = string.Format(
                            "Windows Credential & Session Conflict Detected (Error {0})!\n\n" +
                            "Reason: {1}\n\n" +
                            "Why this happens:\n" +
                            "Windows has cached an older session, username, or password for '{2}'. Even if you updated credentials on the host PC, the Windows kernel holds onto the old session tokens.\n\n" +
                            "Would you like to automatically purge the old cached credentials & reset the network session now, then reconnect?",
                            mapResult.ErrorCode, mapResult.Message, host);

                        DialogResult drConflict = MessageBox.Show(conflictMsg, "Credentials Conflict Detected", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                        if (drConflict == DialogResult.Yes)
                        {
                            string purgeDetails;
                            DriveMapper.ResetServerSession(host, out purgeDetails);
                            if (remember && !string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass))
                            {
                                CredentialManager.SaveCredential(host, user, pass);
                            }
                            OnConnectAndMapClick(sender, e);
                            return;
                        }
                    }
                    else
                    {
                        string errorMsg = string.Format("Could not map drive letter {0}.\n\nReason: {1}\n\nTip: {2}\n\nWould you like to open the network folder directly in File Explorer without a drive letter?",
                            driveLetter, mapResult.Message, mapResult.RecoveryTip);

                        DialogResult dr = MessageBox.Show(errorMsg, "Drive Mapping Notice", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                        if (dr == DialogResult.Yes)
                        {
                            DriveMapper.OpenInExplorer(remotePath);
                        }
                    }
                }));
            });
        }

        private void OnClearOldCredsClick(object sender, EventArgs e)
        {
            string host = _txtHost.Text.Trim();
            if (string.IsNullOrEmpty(host))
            {
                MessageBox.Show("Please enter a host IP address or computer name in Step 1 first.", "Host Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult dr = MessageBox.Show(
                string.Format("Are you sure you want to clear all cached credentials, open SMB sessions, and ticket tokens for '{0}'?\n\n" +
                              "This will:\n" +
                              "• Delete stored logins in Windows Credential Manager for {0}\n" +
                              "• Forcibly disconnect existing SMB sessions and IPC$ handles\n" +
                              "• Purge Kerberos and NTLM session tokens (klist purge)\n\n" +
                              "Use this whenever the username or password was changed on the host PC so you can connect cleanly with your new credentials.", host),
                "Clear Old Device Credentials", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (dr == DialogResult.Yes)
            {
                string details;
                bool ok = DriveMapper.ResetServerSession(host, out details);
                _txtPass.Clear();

                if (ok)
                {
                    _lblConnStatus.Text = string.Format("✔ Old credentials & sessions cleared for {0}. Ready for fresh login.", host);
                    _lblConnStatus.ForeColor = UITheme.SuccessColor;
                    MessageBox.Show(string.Format("All old cached credentials and sessions for '{0}' have been completely wiped.\n\nYou can now enter the new username and password and click 'Connect & Map Drive'.", host),
                        "Credentials & Sessions Cleared", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Notice during credential purge:\n" + details, "Notice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void OnResetAllSessionsClick(object sender, EventArgs e)
        {
            DialogResult dr = MessageBox.Show(
                "Are you sure you want to perform a COMPLETE RESET of ALL network sessions?\n\n" +
                "This will:\n" +
                "• Disconnect all mapped network drives\n" +
                "• Terminate all SMB sessions to all servers (net use * /delete /y)\n" +
                "• Flush Kerberos and NTLM ticket cache (klist purge)\n" +
                "• Clear all application-managed network credentials\n\n" +
                "Do you wish to proceed?",
                "Complete Network Sessions Reset", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (dr == DialogResult.Yes)
            {
                DriveMapper.ResetAllNetworkSessions();
                RefreshMappedDrives();
                LoadDriveLetters();
                _txtPass.Clear();
                _lblConnStatus.Text = "● All network sessions and credentials have been reset.";
                _lblConnStatus.ForeColor = UITheme.SuccessColor;
                MessageBox.Show("All network sessions, mapped drives, and cached credentials have been reset successfully.",
                    "Reset Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
