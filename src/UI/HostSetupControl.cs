using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetworkSharing.Core;
using NetworkSharing.Logging;
using NetworkSharing.Recovery;
using NetworkSharing.Security;

namespace NetworkSharing.UI
{
    public class HostSetupControl : UserControl
    {
        private FlowLayoutPanel _pnlFlow;
        private Panel _pnlHeader;
        private ModernCard _card1;
        private ModernCard _card2;
        private ModernCard _card3;

        private ListView _lvDrives;
        private TextBox _txtFolderPath;
        private ModernButton _btnBrowseFolder;
        private Label _lblDriveWarning;

        private TextBox _txtShareName;
        private TextBox _txtUsername;
        private TextBox _txtPassword;
        private TextBox _txtConfirmPassword;
        private ModernButton _btnGenPassword;
        private CheckBox _chkShowPassword;
        private TextBox _txtDescription;

        private RadioButton _rbReadOnly;
        private RadioButton _rbReadWrite;
        private CheckBox _chkEncryptSmb;
        private CheckBox _chkMultichannel;

        private ModernButton _btnStartSharing;
        private ModernCard _pnlConfirmation;
        private Label _lblConfirmationDetails;
        private ModernButton _btnCopyUnc;
        private ModernButton _btnShowQrMobile;
        private string _lastCreatedUnc = string.Empty;
        private string _lastCreatedShareName = string.Empty;
        private string _lastCreatedUsername = string.Empty;
        private string _lastCreatedPassword = string.Empty;

        // Active Shares & Disconnect Card
        private ModernCard _cardActiveShares;
        private ListView _lvActiveShares;
        private ModernButton _btnStopSelectedShare;
        private ModernButton _btnRefreshActiveShares;
        private ModernButton _btnCopySelectedUnc;
        private ModernButton _btnShowActiveQr;

        // Live Connected Sessions Card
        private ModernCard _cardSessions;
        private ListView _lvSessions;
        private ModernButton _btnRefreshSessions;
        private ModernButton _btnKickSession;

        public event Action ShareCreated;

        public HostSetupControl()
        {
            InitializeComponent();
            LoadDrives();
            RefreshActiveShares();
            RefreshSessions();
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
                Text = "Host Sharing – Share Drives & Folders",
                Font = UITheme.HeaderFont,
                AutoSize = true,
                Location = new Point(0, 0),
                UseMnemonic = false
            };
            Label lblSubtitle = new Label
            {
                Text = "Choose any physical drive or folder to share across your local network with dedicated credentials.",
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

            // -------------------------------------------------------------
            // CARD 1: DRIVE & FOLDER SELECTION
            // -------------------------------------------------------------
            _card1 = new ModernCard { Height = 250 };

            Label lblStep1 = new Label
            {
                Text = "STEP 1: SELECT DRIVE OR FOLDER",
                Font = UITheme.SubHeaderFont,
                ForeColor = UITheme.AccentColor,
                AutoSize = true,
                Location = new Point(18, 14),
                UseMnemonic = false
            };
            _card1.Controls.Add(lblStep1);

            _lvDrives = new ListView
            {
                Location = new Point(18, 42),
                Height = 125,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                GridLines = true,
                Font = UITheme.RegularFont,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _lvDrives.Columns.Add("Drive", 80);
            _lvDrives.Columns.Add("Volume Label", 150);
            _lvDrives.Columns.Add("Free Space", 130);
            _lvDrives.Columns.Add("Total Capacity", 130);
            _lvDrives.Columns.Add("Status / Role", 180);
            _lvDrives.SelectedIndexChanged += OnDriveSelected;
            _card1.Controls.Add(_lvDrives);

            Label lblCustom = new Label
            {
                Text = "Or Specific Folder:",
                Font = UITheme.BoldFont,
                Location = new Point(18, 180),
                AutoSize = true
            };
            _card1.Controls.Add(lblCustom);

            _txtFolderPath = new TextBox
            {
                Location = new Point(155, 177),
                Height = 28,
                Font = UITheme.RegularFont,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _txtFolderPath.TextChanged += OnPathChanged;
            _card1.Controls.Add(_txtFolderPath);

            _btnBrowseFolder = new ModernButton
            {
                Text = "Browse...",
                Height = 29,
                Width = 100,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BorderRadius = 6
            };
            _btnBrowseFolder.Click += OnBrowseFolderClick;
            _card1.Controls.Add(_btnBrowseFolder);

            _lblDriveWarning = new Label
            {
                Text = "⚠ Warning: Windows System Drive (C:) selected. Read-Only mode is automatically recommended for operating system stability.",
                Font = UITheme.SmallFont,
                ForeColor = UITheme.WarningColor,
                Tag = "Warning",
                Location = new Point(18, 214),
                AutoSize = true,
                Visible = false
            };
            _card1.Controls.Add(_lblDriveWarning);
            _pnlFlow.Controls.Add(_card1);

            // -------------------------------------------------------------
            // CARD 2: SHARE DETAILS & CREDENTIALS
            // -------------------------------------------------------------
            _card2 = new ModernCard { Height = 240 };

            Label lblStep2 = new Label
            {
                Text = "STEP 2: CONFIGURE SHARE NAME & CREDENTIALS",
                Font = UITheme.SubHeaderFont,
                ForeColor = UITheme.AccentColor,
                AutoSize = true,
                Location = new Point(18, 14),
                UseMnemonic = false
            };
            _card2.Controls.Add(lblStep2);

            TableLayoutPanel gridCreds = new TableLayoutPanel
            {
                Location = new Point(18, 44),
                Height = 180,
                ColumnCount = 3,
                RowCount = 5,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            gridCreds.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            gridCreds.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            gridCreds.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));

            // Row 0: Share Name
            gridCreds.Controls.Add(new Label { Text = "Share Name:", Font = UITheme.BoldFont, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            _txtShareName = new TextBox { Dock = DockStyle.Fill, Font = UITheme.RegularFont, Margin = new Padding(0, 4, 8, 4) };
            gridCreds.Controls.Add(_txtShareName, 1, 0);
            gridCreds.Controls.Add(new Label { Text = "(Network identifier)", Font = UITheme.SmallFont, ForeColor = UITheme.SubTextColor, Tag = "SubText", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 2, 0);

            // Row 1: Username
            gridCreds.Controls.Add(new Label { Text = "Sharing User:", Font = UITheme.BoldFont, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
            _txtUsername = new TextBox { Dock = DockStyle.Fill, Font = UITheme.RegularFont, Text = "ShareUser", Margin = new Padding(0, 4, 8, 4) };
            gridCreds.Controls.Add(_txtUsername, 1, 1);
            gridCreds.Controls.Add(new Label { Text = "(Dedicated standard account)", Font = UITheme.SmallFont, ForeColor = UITheme.SubTextColor, Tag = "SubText", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 2, 1);

            // Row 2: Password
            gridCreds.Controls.Add(new Label { Text = "Password:", Font = UITheme.BoldFont, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
            _txtPassword = new TextBox { Dock = DockStyle.Fill, Font = UITheme.RegularFont, UseSystemPasswordChar = true, Margin = new Padding(0, 4, 8, 4) };
            gridCreds.Controls.Add(_txtPassword, 1, 2);

            _btnGenPassword = new ModernButton
            {
                Text = "Generate Strong Password",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 0, 2),
                BorderRadius = 6
            };
            _btnGenPassword.Click += (s, e) =>
            {
                string pwd = PasswordGenerator.Generate(14);
                _txtPassword.Text = pwd;
                _txtConfirmPassword.Text = pwd;
                _chkShowPassword.Checked = true;
            };
            gridCreds.Controls.Add(_btnGenPassword, 2, 2);

            // Row 3: Confirm Password
            gridCreds.Controls.Add(new Label { Text = "Confirm Password:", Font = UITheme.BoldFont, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 3);
            _txtConfirmPassword = new TextBox { Dock = DockStyle.Fill, Font = UITheme.RegularFont, UseSystemPasswordChar = true, Margin = new Padding(0, 4, 8, 4) };
            gridCreds.Controls.Add(_txtConfirmPassword, 1, 3);

            _chkShowPassword = new CheckBox { Text = "Show Password", Dock = DockStyle.Fill, Font = UITheme.SmallFont, Margin = new Padding(4, 4, 0, 0) };
            _chkShowPassword.CheckedChanged += (s, e) =>
            {
                _txtPassword.UseSystemPasswordChar = !_chkShowPassword.Checked;
                _txtConfirmPassword.UseSystemPasswordChar = !_chkShowPassword.Checked;
            };
            gridCreds.Controls.Add(_chkShowPassword, 2, 3);

            // Row 4: Description
            gridCreds.Controls.Add(new Label { Text = "Description:", Font = UITheme.BoldFont, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 4);
            _txtDescription = new TextBox { Dock = DockStyle.Fill, Font = UITheme.RegularFont, Text = "Local Network Folder Share", Margin = new Padding(0, 4, 8, 4) };
            gridCreds.Controls.Add(_txtDescription, 1, 4);
            gridCreds.SetColumnSpan(_txtDescription, 2);

            _card2.Controls.Add(gridCreds);
            _pnlFlow.Controls.Add(_card2);

            // -------------------------------------------------------------
            // CARD 3: PERMISSIONS
            // -------------------------------------------------------------
            _card3 = new ModernCard { Height = 175 };

            Label lblStep3 = new Label
            {
                Text = "STEP 3: ACCESS PERMISSIONS & ADVANCED OPTIONS",
                Font = UITheme.SubHeaderFont,
                ForeColor = UITheme.AccentColor,
                AutoSize = true,
                Location = new Point(18, 14),
                UseMnemonic = false
            };
            _card3.Controls.Add(lblStep3);

            _rbReadWrite = new RadioButton
            {
                Text = "Read & Write  (Full Collaboration – browse, download, create, edit, rename, and delete files)",
                Location = new Point(18, 44),
                Height = 30,
                Font = UITheme.BoldFont,
                Checked = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                UseMnemonic = false
            };
            _card3.Controls.Add(_rbReadWrite);

            _rbReadOnly = new RadioButton
            {
                Text = "Read-Only  (Protected / Safe Mode – browse and copy files, but cannot modify, edit, or delete)",
                Location = new Point(18, 76),
                Height = 30,
                Font = UITheme.BoldFont,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                UseMnemonic = false
            };
            _card3.Controls.Add(_rbReadOnly);

            _chkEncryptSmb = new CheckBox
            {
                Text = "🔒 Require SMB 3.1.1 Wire Encryption  (Protects all data transmitted over local Wi-Fi)",
                Location = new Point(18, 112),
                Height = 26,
                Font = UITheme.SmallFont,
                Checked = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _card3.Controls.Add(_chkEncryptSmb);

            _chkMultichannel = new CheckBox
            {
                Text = "⚡ Enable SMB Multichannel  (Aggregates Wi-Fi + Ethernet adapters for up to 2x speed)",
                Location = new Point(18, 140),
                Height = 26,
                Font = UITheme.SmallFont,
                Checked = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _card3.Controls.Add(_chkMultichannel);
            _pnlFlow.Controls.Add(_card3);

            // -------------------------------------------------------------
            // ACTION BUTTON
            // -------------------------------------------------------------
            _btnStartSharing = new ModernButton
            {
                Text = "Start Sharing Now",
                IconType = ButtonIcon.Rocket,
                Height = 46,
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                Margin = new Padding(0, 6, 0, 16),
                IsPrimary = true,
                BorderRadius = 8
            };
            _btnStartSharing.Click += OnStartSharingClick;
            _pnlFlow.Controls.Add(_btnStartSharing);

            // -------------------------------------------------------------
            // CONFIRMATION CARD
            // -------------------------------------------------------------
            _pnlConfirmation = new ModernCard { Height = 110 };
            _pnlConfirmation.Visible = false;

            Label lblSuccess = new Label
            {
                Text = "Share Successfully Activated & Live on Network!",
                Font = UITheme.SubHeaderFont,
                ForeColor = UITheme.SuccessColor,
                Tag = "Success",
                Location = new Point(18, 12),
                AutoSize = true,
                UseMnemonic = false
            };
            _pnlConfirmation.Controls.Add(lblSuccess);

            _lblConfirmationDetails = new Label
            {
                Text = "",
                Font = UITheme.RegularFont,
                Location = new Point(18, 42),
                Height = 50,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                UseMnemonic = false
            };
            _pnlConfirmation.Controls.Add(_lblConfirmationDetails);

            _btnCopyUnc = new ModernButton
            {
                Text = "Copy Path",
                IconType = ButtonIcon.Copy,
                Height = 36,
                Width = 110,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BorderRadius = 6
            };
            _btnCopyUnc.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(_lastCreatedUnc))
                {
                    Clipboard.SetText(_lastCreatedUnc);
                    MessageBox.Show("Network path copied to clipboard:\n" + _lastCreatedUnc, "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("No active network path to copy. Please create a share first.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            _pnlConfirmation.Controls.Add(_btnCopyUnc);

            _btnShowQrMobile = new ModernButton
            {
                Text = "📱 Mobile QR Code",
                Height = 36,
                Width = 150,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BorderRadius = 6,
                IsPrimary = true
            };
            _btnShowQrMobile.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(_lastCreatedUnc))
                {
                    string hostIp = NetworkManager.GetLocalIpAddress();
                    string uri = string.Format("smb://{0}/{1}", hostIp, _lastCreatedShareName);
                    QrCodeGenerator.ShowQrDialog(this, _lastCreatedShareName, _lastCreatedUnc, uri, _lastCreatedUsername, _lastCreatedPassword);
                }
                else
                {
                    MessageBox.Show("Please create or select an active share first.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            _pnlConfirmation.Controls.Add(_btnShowQrMobile);
            _pnlFlow.Controls.Add(_pnlConfirmation);

            // -------------------------------------------------------------
            // ACTIVE SHARES & DISCONNECT CARD
            // -------------------------------------------------------------
            _cardActiveShares = new ModernCard { Height = 210 };

            Label lblActiveHdr = new Label
            {
                Text = "ACTIVE HOST SHARES – DISCONNECT / STOP SHARING",
                Font = UITheme.SubHeaderFont,
                ForeColor = UITheme.AccentColor,
                AutoSize = true,
                Location = new Point(18, 14),
                UseMnemonic = false
            };
            _cardActiveShares.Controls.Add(lblActiveHdr);

            _lvActiveShares = new ListView
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
            _lvActiveShares.Columns.Add("Share Name", 140);
            _lvActiveShares.Columns.Add("Local Path", 200);
            _lvActiveShares.Columns.Add("Network UNC Path", 280);
            _lvActiveShares.Columns.Add("Managed By", 110);
            _cardActiveShares.Controls.Add(_lvActiveShares);

            FlowLayoutPanel pnlShareActions = new FlowLayoutPanel
            {
                Location = new Point(18, 160),
                Height = 40,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _btnStopSelectedShare = new ModernButton
            {
                Text = "Disconnect / Stop Share",
                IconType = ButtonIcon.Delete,
                Width = 200,
                Height = 34,
                IsDanger = true,
                BorderRadius = 6,
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnStopSelectedShare.Click += OnStopSelectedShareClick;
            pnlShareActions.Controls.Add(_btnStopSelectedShare);

            _btnRefreshActiveShares = new ModernButton
            {
                Text = "Refresh",
                IconType = ButtonIcon.Refresh,
                Width = 110,
                Height = 34,
                BorderRadius = 6,
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnRefreshActiveShares.Click += (s, e) => RefreshActiveShares();
            pnlShareActions.Controls.Add(_btnRefreshActiveShares);

            _btnCopySelectedUnc = new ModernButton
            {
                Text = "Copy UNC Path",
                IconType = ButtonIcon.Copy,
                Width = 140,
                Height = 34,
                BorderRadius = 6,
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnCopySelectedUnc.Click += OnCopySelectedUncClick;
            pnlShareActions.Controls.Add(_btnCopySelectedUnc);

            _btnShowActiveQr = new ModernButton
            {
                Text = "📱 Mobile QR Code",
                Width = 150,
                Height = 34,
                BorderRadius = 6
            };
            _btnShowActiveQr.Click += (s, e) =>
            {
                if (_lvActiveShares.SelectedItems.Count > 0)
                {
                    string sName = _lvActiveShares.SelectedItems[0].SubItems[0].Text;
                    string unc = _lvActiveShares.SelectedItems[0].SubItems[2].Text;
                    string hostIp = NetworkManager.GetLocalIpAddress();
                    string uri = string.Format("smb://{0}/{1}", hostIp, sName);
                    QrCodeGenerator.ShowQrDialog(this, sName, unc, uri, "ShareUser", "");
                }
                else
                {
                    MessageBox.Show("Please select an active share from the list first.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            pnlShareActions.Controls.Add(_btnShowActiveQr);

            _cardActiveShares.Controls.Add(pnlShareActions);
            _pnlFlow.Controls.Add(_cardActiveShares);

            // -------------------------------------------------------------
            // LIVE SESSIONS & CONNECTED CLIENTS CARD
            // -------------------------------------------------------------
            _cardSessions = new ModernCard { Height = 210 };

            Label lblSessionsHdr = new Label
            {
                Text = "CONNECTED CLIENT COMPUTERS & LIVE SESSIONS",
                Font = UITheme.SubHeaderFont,
                ForeColor = UITheme.AccentColor,
                AutoSize = true,
                Location = new Point(18, 14),
                UseMnemonic = false
            };
            _cardSessions.Controls.Add(lblSessionsHdr);

            _lvSessions = new ListView
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
            _lvSessions.Columns.Add("Client IP / Computer", 180);
            _lvSessions.Columns.Add("Connected User", 140);
            _lvSessions.Columns.Add("Client Type", 140);
            _lvSessions.Columns.Add("Idle Duration", 120);
            _cardSessions.Controls.Add(_lvSessions);

            FlowLayoutPanel pnlSessionActions = new FlowLayoutPanel
            {
                Location = new Point(18, 160),
                Height = 40,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _btnRefreshSessions = new ModernButton
            {
                Text = "Refresh Sessions",
                IconType = ButtonIcon.Refresh,
                Width = 150,
                Height = 34,
                BorderRadius = 6,
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnRefreshSessions.Click += (s, e) => RefreshSessions();
            pnlSessionActions.Controls.Add(_btnRefreshSessions);

            _btnKickSession = new ModernButton
            {
                Text = "Disconnect / Kick Client",
                IconType = ButtonIcon.Delete,
                Width = 190,
                Height = 34,
                IsDanger = true,
                BorderRadius = 6
            };
            _btnKickSession.Click += OnKickSessionClick;
            pnlSessionActions.Controls.Add(_btnKickSession);

            _cardSessions.Controls.Add(pnlSessionActions);
            _pnlFlow.Controls.Add(_cardSessions);

            this.Controls.Add(_pnlFlow);
        }

        private void OnFlowResize(object sender, EventArgs e)
        {
            int targetWidth = Math.Max(_pnlFlow.ClientSize.Width - 65, 550);

            _pnlHeader.Width = targetWidth;
            _card1.Width = targetWidth;
            _card2.Width = targetWidth;
            _card3.Width = targetWidth;
            _btnStartSharing.Width = targetWidth;
            _pnlConfirmation.Width = targetWidth;
            _cardActiveShares.Width = targetWidth;
            _cardSessions.Width = targetWidth;

            // Align inner controls
            _lvDrives.Width = targetWidth - 36;
            _lvActiveShares.Width = targetWidth - 36;
            _lvSessions.Width = targetWidth - 36;
            _txtFolderPath.Width = targetWidth - 155 - 120;
            _btnBrowseFolder.Location = new Point(targetWidth - 116, 176);

            _btnCopyUnc.Location = new Point(targetWidth - 280, 42);
            _btnShowQrMobile.Location = new Point(targetWidth - 160, 42);
            _lblConfirmationDetails.Width = targetWidth - 300;
        }

        public void RefreshSessions()
        {
            if (_lvSessions == null) return;
            _lvSessions.Items.Clear();
            List<SmbSessionInfo> sessions = SessionManager.GetActiveSessions();
            if (sessions.Count == 0)
            {
                ListViewItem lvi = new ListViewItem("No active remote sessions connected.");
                lvi.SubItems.Add("-");
                lvi.SubItems.Add("-");
                lvi.SubItems.Add("-");
                lvi.ForeColor = UITheme.SubTextColor;
                _lvSessions.Items.Add(lvi);
            }
            else
            {
                foreach (var sess in sessions)
                {
                    ListViewItem lvi = new ListViewItem(sess.ClientComputer);
                    lvi.SubItems.Add(sess.UserName);
                    lvi.SubItems.Add(sess.ClientType);
                    lvi.SubItems.Add(sess.IdleTime);
                    lvi.Tag = sess;
                    _lvSessions.Items.Add(lvi);
                }
            }
        }

        private void OnKickSessionClick(object sender, EventArgs e)
        {
            if (_lvSessions.SelectedItems.Count == 0 || _lvSessions.SelectedItems[0].Tag == null)
            {
                MessageBox.Show("Please select an active connected client from the list to disconnect.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SmbSessionInfo sess = _lvSessions.SelectedItems[0].Tag as SmbSessionInfo;
            if (sess == null) return;

            DialogResult dr = MessageBox.Show(
                string.Format("Are you sure you want to disconnect client '{0}' (User: {1})?", sess.ClientComputer, sess.UserName),
                "Confirm Disconnect", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (dr == DialogResult.Yes)
            {
                bool ok = SessionManager.DisconnectSession(sess.ClientComputer);
                RefreshSessions();
                if (ok)
                {
                    MessageBox.Show("Client session terminated successfully.", "Disconnected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Could not disconnect client session. Administrator elevation may be required.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        public void RefreshActiveShares()
        {
            if (_lvActiveShares == null) return;
            _lvActiveShares.Items.Clear();
            List<SharedFolderInfo> shares = ShareManager.GetActiveShares();
            string localIp = NetworkManager.GetLocalIpAddress();

            foreach (var s in shares)
            {
                ListViewItem lvi = new ListViewItem(s.ShareName);
                lvi.SubItems.Add(s.Path);
                lvi.SubItems.Add(string.Format(@"\\{0}\{1}", localIp, s.ShareName));
                lvi.SubItems.Add(s.CreatedByApp ? "NetworkSharing" : "Windows Native");
                lvi.Tag = s;
                _lvActiveShares.Items.Add(lvi);
            }
        }

        private void OnStopSelectedShareClick(object sender, EventArgs e)
        {
            if (_lvActiveShares.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select a share from the list to disconnect and stop sharing.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!Program.IsAdministrator())
            {
                DialogResult elevDr = MessageBox.Show(
                    "Disconnecting an SMB network share requires Administrator privileges.\n\n" +
                    "Would you like to restart NetworkSharing with Administrator privileges now?",
                    "Administrator Privileges Required",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (elevDr == DialogResult.Yes)
                {
                    NetworkSharing.Privileges.ElevationManager.RequestElevation("host");
                }
                return;
            }

            string shareName = _lvActiveShares.SelectedItems[0].SubItems[0].Text;
            DialogResult dr = MessageBox.Show(
                string.Format("Are you sure you want to disconnect and stop sharing '{0}'?\n\n(Your physical files on disk will NOT be deleted)", shareName),
                "Confirm Disconnect Share", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (dr == DialogResult.Yes)
            {
                _btnStopSelectedShare.Enabled = false;
                Task.Factory.StartNew(() =>
                {
                    bool ok = ShareManager.DeleteShare(shareName);
                    this.BeginInvoke(new Action(() =>
                    {
                        _btnStopSelectedShare.Enabled = true;
                        RefreshActiveShares();
                        if (ok)
                        {
                            MessageBox.Show(string.Format("Share '{0}' has been disconnected and stopped successfully.", shareName), "Share Disconnected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show(string.Format("Failed to disconnect share '{0}'.", shareName), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }));
                });
            }
        }

        private void OnCopySelectedUncClick(object sender, EventArgs e)
        {
            if (_lvActiveShares.SelectedItems.Count > 0)
            {
                string unc = _lvActiveShares.SelectedItems[0].SubItems[2].Text;
                Clipboard.SetText(unc);
                MessageBox.Show("Network path copied to clipboard:\n" + unc, "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Please select a share from the list first.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void LoadDrives()
        {
            _lvDrives.Items.Clear();
            List<DriveItem> drives = DriveDetector.GetAvailableDrives();
            foreach (var d in drives)
            {
                ListViewItem lvi = new ListViewItem(d.Name);
                lvi.SubItems.Add(d.Label);
                lvi.SubItems.Add(string.Format("{0} GB free", d.FreeGB));
                lvi.SubItems.Add(string.Format("{0} GB", d.TotalGB));
                lvi.SubItems.Add(d.IsSystemDrive ? "OS Drive (Protected)" : "Ready to Share");
                lvi.Tag = d;
                _lvDrives.Items.Add(lvi);
            }
        }

        private void OnDriveSelected(object sender, EventArgs e)
        {
            if (_lvDrives.SelectedItems.Count > 0)
            {
                DriveItem d = _lvDrives.SelectedItems[0].Tag as DriveItem;
                if (d != null)
                {
                    _txtFolderPath.Text = d.Name;
                    _txtShareName.Text = "Drive_" + d.Letter;

                    if (d.IsSystemDrive)
                    {
                        _lblDriveWarning.Text = "⚠ Security Notice: OS System Drive (C:) selected. Read-Only mode is automatically recommended.";
                        _lblDriveWarning.Visible = true;
                        _rbReadOnly.Checked = true;
                    }
                    else
                    {
                        _lblDriveWarning.Visible = false;
                    }
                }
            }
        }

        private void OnPathChanged(object sender, EventArgs e)
        {
            string p = _txtFolderPath.Text.Trim();
            if (p.Length >= 2 && p.EndsWith(@"\"))
            {
                string letter = p.Substring(0, 1).ToUpperInvariant();
                if (letter == "C")
                {
                    _lblDriveWarning.Text = "⚠ Security Notice: Windows OS Drive selected. Read-Only mode recommended.";
                    _lblDriveWarning.Visible = true;
                }
                else
                {
                    _lblDriveWarning.Visible = false;
                }
            }
            else if (!string.IsNullOrEmpty(p) && Directory.Exists(p))
            {
                _txtShareName.Text = Path.GetFileName(p.TrimEnd('\\'));
                _lblDriveWarning.Visible = false;
            }
        }

        private void OnBrowseFolderClick(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Select a folder to share on your local network:";
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    _txtFolderPath.Text = fbd.SelectedPath;
                    _txtShareName.Text = Path.GetFileName(fbd.SelectedPath);
                }
            }
        }

        private void OnStartSharingClick(object sender, EventArgs e)
        {
            string path = _txtFolderPath.Text.Trim();
            string shareName = _txtShareName.Text.Trim();
            string user = _txtUsername.Text.Trim();
            string pass = _txtPassword.Text;
            string confirm = _txtConfirmPassword.Text;
            string desc = _txtDescription.Text.Trim();
            SharePermissionType perm = _rbReadWrite.Checked ? SharePermissionType.ReadWrite : SharePermissionType.ReadOnly;
            bool encrypt = _chkEncryptSmb.Checked;

            // 1. Validations
            var pathVal = SecurityValidator.ValidatePath(path);
            if (!pathVal.IsValid)
            {
                MessageBox.Show(pathVal.Message, "Invalid Path", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (pathVal.IsWarning)
            {
                DialogResult dr = MessageBox.Show(pathVal.Message + "\n\nDo you want to continue?", "Security Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (dr != DialogResult.Yes) return;
            }

            var shareVal = SecurityValidator.ValidateShareName(shareName);
            if (!shareVal.IsValid)
            {
                MessageBox.Show(shareVal.Message, "Invalid Share Name", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var userVal = SecurityValidator.ValidateUsername(user);
            if (!userVal.IsValid)
            {
                MessageBox.Show(userVal.Message, "Invalid Username", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var passVal = SecurityValidator.ValidatePassword(pass);
            if (!passVal.IsValid)
            {
                MessageBox.Show(passVal.Message, "Password Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (pass != confirm)
            {
                MessageBox.Show("Password and Confirm Password do not match.", "Password Mismatch", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Check elevation for Host Setup operations
            if (!Program.IsAdministrator())
            {
                DialogResult dr = MessageBox.Show(
                    "Host sharing requires Administrator privileges to create Windows SMB shares, configure local security accounts, and adjust firewall rules.\n\n" +
                    "Would you like to restart NetworkSharing with Administrator privileges now?",
                    "Administrator Privileges Required",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (dr == DialogResult.Yes)
                {
                    Program.RelaunchAsAdmin("--host");
                }
                return;
            }

            // 2. Execution with rollback capability (runs asynchronously to keep UI responsive)
            _btnStartSharing.Enabled = false;
            _btnStartSharing.Text = "Configuring Sharing Services...";

            Task.Factory.StartNew(() =>
            {
                try
                {
                    if (!UserManager.CreateDedicatedUser(user, pass))
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            MessageBox.Show("Failed to create or configure dedicated sharing user account.", "Setup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            RollbackManager.RollbackAll();
                        }));
                        return;
                    }
                    RollbackManager.PushUserCreated(user);

                    if (!FirewallManager.ConfigureInboundSmbRule())
                    {
                        Logger.Warning("Firewall rule configuration produced warning, continuing...");
                    }
                    RollbackManager.PushFirewallRuleCreated(FirewallManager.RuleName);

                    if (!ShareManager.CreateShare(shareName, path, user, perm, desc, encrypt))
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            string err = !string.IsNullOrEmpty(ShareManager.LastError) ? ShareManager.LastError : "Could not register SMB share with Windows Server service.";
                            MessageBox.Show("Failed to create Windows SMB share:\n" + err + "\n\nOperation rolled back.", "Share Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            RollbackManager.RollbackAll();
                        }));
                        return;
                    }
                    RollbackManager.PushShareCreated(shareName);

                    RollbackManager.Commit();

                    string hostIp = NetworkManager.GetLocalIpAddress();
                    _lastCreatedUnc = string.Format(@"\\{0}\{1}", hostIp, shareName);
                    _lastCreatedShareName = shareName;
                    _lastCreatedUsername = user;
                    _lastCreatedPassword = pass;

                    this.BeginInvoke(new Action(() =>
                    {
                        _lblConfirmationDetails.Text = string.Format(
                            "Network Path : {0}\nUsername     : {1}    |    Permissions : {2}",
                            _lastCreatedUnc, user, perm == SharePermissionType.ReadWrite ? "Read & Write" : "Read-Only");

                        _pnlConfirmation.Visible = true;
                        RefreshActiveShares();
                        MessageBox.Show("Network share is live!\n\nOther devices on your Wi-Fi can now connect to:\n" + _lastCreatedUnc, 
                            "Share Created Successfully", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        if (ShareCreated != null) ShareCreated();
                    }));
                }
                catch (Exception ex)
                {
                    Logger.Error("Host setup exception", ex);
                    RollbackManager.RollbackAll();
                    this.BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show("Unexpected error during setup: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
                finally
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        _btnStartSharing.Enabled = true;
                        _btnStartSharing.Text = "Start Sharing Now";
                    }));
                }
            });
        }
    }
}
