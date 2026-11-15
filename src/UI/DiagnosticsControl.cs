using System;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetworkSharing.Core;
using NetworkSharing.Logging;

namespace NetworkSharing.UI
{
    public class DiagnosticsControl : UserControl
    {
        private FlowLayoutPanel _pnlFlow;
        private Panel _pnlHeader;

        // 1. Host Info Card
        private ModernCard _cardHost;
        private Label _lblHostDetails;

        // 2. Ping Test Card (Actual Ping in ms)
        private ModernCard _cardPing;
        private TextBox _txtPingHost;
        private ModernButton _btnPing;
        private ModernButton _btnTestPort;
        private Label _lblPingSummary;
        private TextBox _txtPingOutput;

        // 3. Speed Test Card (Actual speed between 2 computers with unit toggle)
        private ModernCard _cardSpeed;
        private ModernButton _btnModeIperf;
        private ModernButton _btnModeSmb;
        private bool _isIperfMode = true;

        private Label _lblSpeedTarget;
        private ComboBox _cmbSpeedTarget;
        private Label _lblDurationOrPayload;
        private ComboBox _cmbDuration;
        private ComboBox _cmbPayloadSize;
        private ModernButton _btnStartSpeedTest;
        private ProgressBar _speedProgress;
        private Label _lblSpeedStatus;
        private TextBox _txtIperfLog;

        // Unit Toggle Buttons
        private ModernButton _btnUnitMBps;
        private ModernButton _btnUnitMbps;
        private ModernButton _btnUnitGBps;
        private ModernButton _btnUnitGbps;
        private SpeedUnit _currentUnit = SpeedUnit.Mbps;

        // Speed Metrics Display
        private Label _lblWriteSpeed;
        private Label _lblReadSpeed;
        private Label _lblAvgSpeed;
        private Label _lblSpeedDetails;
        private Panel _pnlMetrics;
        private SpeedTestResult _lastSpeedResult;

        // 4. Logs Card
        private ModernCard _cardLogs;
        private TextBox _txtLogs;
        private ModernButton _btnClearLogs;
        private ModernButton _btnCopyLogs;

        public DiagnosticsControl()
        {
            InitializeComponent();
            LoadDiagnostics();
            Logger.LogAppended += OnLogAppended;
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
                Text = "Network Speed & Connectivity Diagnostics",
                Font = UITheme.HeaderFont,
                AutoSize = true,
                Location = new Point(0, 0),
                UseMnemonic = false
            };
            Label lblSubtitle = new Label
            {
                Text = "Measure actual ping latency in ms, test network transfer speed between 2 computers, and review logs.",
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
            // CARD 1: HOST & SYSTEM PROFILE
            // -------------------------------------------------------------
            _cardHost = new ModernCard { Height = 105 };
            Label lblHdr = new Label
            {
                Text = "LOCAL HOST & NETWORK PROFILE",
                Font = UITheme.SubHeaderFont,
                ForeColor = UITheme.AccentColor,
                Location = new Point(18, 12),
                AutoSize = true,
                UseMnemonic = false
            };
            _cardHost.Controls.Add(lblHdr);

            _lblHostDetails = new Label
            {
                Text = "Loading network profile...",
                Location = new Point(18, 38),
                Height = 55,
                Font = UITheme.RegularFont,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                UseMnemonic = false
            };
            _cardHost.Controls.Add(_lblHostDetails);
            _pnlFlow.Controls.Add(_cardHost);

            // -------------------------------------------------------------
            // CARD 2: REAL-TIME PING TEST (ACTUAL PING IN MS)
            // -------------------------------------------------------------
            _cardPing = new ModernCard { Height = 210 };
            Label lblPingHdr = new Label
            {
                Text = "ACTUAL PING TEST – MEASURE REAL-TIME LATENCY (MS)",
                Font = UITheme.SubHeaderFont,
                ForeColor = UITheme.AccentColor,
                Location = new Point(18, 12),
                AutoSize = true,
                UseMnemonic = false
            };
            _cardPing.Controls.Add(lblPingHdr);

            Label lblTarget = new Label { Text = "Target IP/Host:", Location = new Point(18, 42), AutoSize = true, Font = UITheme.BoldFont, UseMnemonic = false };
            _cardPing.Controls.Add(lblTarget);

            string localIp = NetworkManager.GetLocalIpAddress();
            _txtPingHost = new TextBox { Location = new Point(135, 39), Height = 28, Font = UITheme.RegularFont, Text = localIp, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _cardPing.Controls.Add(_txtPingHost);

            _btnPing = new ModernButton { Text = "Run Ping Test", Height = 29, Width = 135, Anchor = AnchorStyles.Top | AnchorStyles.Right, BorderRadius = 6 };
            _btnPing.Click += OnPingClick;
            _cardPing.Controls.Add(_btnPing);

            _btnTestPort = new ModernButton { Text = "Test SMB 445", Height = 29, Width = 120, Anchor = AnchorStyles.Top | AnchorStyles.Right, BorderRadius = 6 };
            _btnTestPort.Click += OnTestPortClick;
            _cardPing.Controls.Add(_btnTestPort);

            _lblPingSummary = new Label
            {
                Text = "● Ready to measure actual ping latency. Click 'Run Ping Test' to start.",
                Location = new Point(18, 76),
                AutoSize = true,
                Font = UITheme.BoldFont,
                ForeColor = UITheme.SubTextColor,
                Tag = "SubText",
                UseMnemonic = false
            };
            _cardPing.Controls.Add(_lblPingSummary);

            _txtPingOutput = new TextBox
            {
                Location = new Point(18, 102),
                Height = 92,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9f),
                BackColor = UITheme.InputBgColor,
                ForeColor = UITheme.TextColor,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _cardPing.Controls.Add(_txtPingOutput);
            _pnlFlow.Controls.Add(_cardPing);

            // -------------------------------------------------------------
            // CARD 3: SPEED TEST BETWEEN 2 COMPUTERS (IP-TO-IP / SMB)
            // -------------------------------------------------------------
            _cardSpeed = new ModernCard { Height = 430 };
            Label lblSpeedHdr = new Label
            {
                Text = "REAL SPEED BENCHMARK BETWEEN 2 COMPUTERS (IP-TO-IP / SMB)",
                Font = UITheme.SubHeaderFont,
                ForeColor = UITheme.AccentColor,
                Location = new Point(18, 12),
                AutoSize = true,
                UseMnemonic = false
            };
            _cardSpeed.Controls.Add(lblSpeedHdr);

            // Row 1: Mode Switcher (iPerf3 Direct IP vs SMB Share)
            Label lblMode = new Label { Text = "Mode:", Location = new Point(18, 42), AutoSize = true, Font = UITheme.BoldFont, UseMnemonic = false };
            _cardSpeed.Controls.Add(lblMode);

            _btnModeIperf = new ModernButton
            {
                Text = "⚡ Direct IP-to-IP (30s iPerf3 Benchmark)",
                Location = new Point(145, 38),
                Width = 275,
                Height = 28,
                IsPill = true,
                IsPrimary = true,
                BorderRadius = 14,
                Font = UITheme.BoldFont
            };
            _btnModeIperf.Click += (s, e) => SetBenchmarkMode(true);
            _cardSpeed.Controls.Add(_btnModeIperf);

            _btnModeSmb = new ModernButton
            {
                Text = "📂 SMB Shared Folder (Read/Write)",
                Location = new Point(428, 38),
                Width = 240,
                Height = 28,
                IsPill = true,
                IsPrimary = false,
                BorderRadius = 14,
                Font = UITheme.BoldFont
            };
            _btnModeSmb.Click += (s, e) => SetBenchmarkMode(false);
            _cardSpeed.Controls.Add(_btnModeSmb);

            // Row 2: Target Host IP / Share Path
            _lblSpeedTarget = new Label { Text = "Target IP / Host:", Location = new Point(18, 77), AutoSize = true, Font = UITheme.BoldFont, UseMnemonic = false };
            _cardSpeed.Controls.Add(_lblSpeedTarget);

            _cmbSpeedTarget = new ComboBox
            {
                Location = new Point(145, 74),
                Height = 28,
                Font = UITheme.RegularFont,
                DropDownStyle = ComboBoxStyle.DropDown,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _cardSpeed.Controls.Add(_cmbSpeedTarget);

            _btnStartSpeedTest = new ModernButton
            {
                Text = "Start iPerf3 Test",
                IconType = ButtonIcon.Rocket,
                Height = 32,
                Width = 175,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                IsPrimary = true,
                BorderRadius = 8
            };
            _btnStartSpeedTest.Click += OnStartSpeedTestClick;
            _cardSpeed.Controls.Add(_btnStartSpeedTest);

            // Row 3: Duration / Payload + Unit Toggle
            _lblDurationOrPayload = new Label { Text = "Duration:", Location = new Point(18, 113), AutoSize = true, Font = UITheme.BoldFont, UseMnemonic = false };
            _cardSpeed.Controls.Add(_lblDurationOrPayload);

            _cmbDuration = new ComboBox
            {
                Location = new Point(145, 110),
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = UITheme.RegularFont
            };
            _cmbDuration.Items.AddRange(new object[] { "10 Seconds (Quick)", "30 Seconds (iPerf3 Standard)", "60 Seconds (Stress Test)" });
            _cmbDuration.SelectedIndex = 1; // 30s default
            _cardSpeed.Controls.Add(_cmbDuration);

            _cmbPayloadSize = new ComboBox
            {
                Location = new Point(145, 110),
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = UITheme.RegularFont,
                Visible = false
            };
            _cmbPayloadSize.Items.AddRange(new object[] { "10 MB (Quick)", "50 MB (Balanced)", "100 MB (Accurate)", "250 MB (Gigabit Stress)" });
            _cmbPayloadSize.SelectedIndex = 1;
            _cardSpeed.Controls.Add(_cmbPayloadSize);

            // Unit Toggle Label & Pills
            Label lblUnit = new Label { Text = "Unit Toggle:", Location = new Point(360, 113), AutoSize = true, Font = UITheme.BoldFont, UseMnemonic = false };
            _cardSpeed.Controls.Add(lblUnit);

            _btnUnitMBps = CreateUnitPill("MB/s", SpeedUnit.MBps, 445);
            _btnUnitMbps = CreateUnitPill("Mbps", SpeedUnit.Mbps, 515);
            _btnUnitGBps = CreateUnitPill("GB/s", SpeedUnit.GBps, 585);
            _btnUnitGbps = CreateUnitPill("Gbps", SpeedUnit.Gbps, 655);

            _cardSpeed.Controls.Add(_btnUnitMBps);
            _cardSpeed.Controls.Add(_btnUnitMbps);
            _cardSpeed.Controls.Add(_btnUnitGBps);
            _cardSpeed.Controls.Add(_btnUnitGbps);
            UpdateUnitPills();

            // Row 4: Progress Bar
            _speedProgress = new ProgressBar
            {
                Location = new Point(18, 148),
                Height = 8,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _cardSpeed.Controls.Add(_speedProgress);

            // Row 5: Status Label
            _lblSpeedStatus = new Label
            {
                Text = "● Ready for 30s peer-to-peer TCP network speed benchmark with target IP.",
                Location = new Point(18, 162),
                AutoSize = true,
                Font = UITheme.SmallFont,
                ForeColor = UITheme.SubTextColor,
                Tag = "SubText",
                UseMnemonic = false
            };
            _cardSpeed.Controls.Add(_lblSpeedStatus);

            // Row 6: Live 1-Second Interval Stream Log Box (iPerf3 Monospace Console)
            _txtIperfLog = new TextBox
            {
                Location = new Point(18, 184),
                Height = 115,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9f),
                BackColor = UITheme.InputBgColor,
                ForeColor = UITheme.TextColor,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _cardSpeed.Controls.Add(_txtIperfLog);

            // Row 7: Metrics Display Cards / Labels
            _pnlMetrics = new Panel
            {
                Location = new Point(18, 308),
                Height = 72,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            TableLayoutPanel gridMetrics = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2
            };
            gridMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            gridMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            gridMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));

            gridMetrics.Controls.Add(new Label { Text = "WRITE (UPLOAD) SPEED", Font = UITheme.SmallFont, ForeColor = UITheme.SubTextColor, Tag = "SubText", TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill }, 0, 0);
            gridMetrics.Controls.Add(new Label { Text = "READ (DOWNLOAD) SPEED", Font = UITheme.SmallFont, ForeColor = UITheme.SubTextColor, Tag = "SubText", TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill }, 1, 0);
            gridMetrics.Controls.Add(new Label { Text = "AVERAGE THROUGHPUT", Font = UITheme.SmallFont, ForeColor = UITheme.SubTextColor, Tag = "SubText", TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill }, 2, 0);

            _lblWriteSpeed = new Label { Text = "---", Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = UITheme.AccentColor, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill };
            _lblReadSpeed = new Label { Text = "---", Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = UITheme.SuccessColor, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill };
            _lblAvgSpeed = new Label { Text = "---", Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = UITheme.TextColor, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill };

            gridMetrics.Controls.Add(_lblWriteSpeed, 0, 1);
            gridMetrics.Controls.Add(_lblReadSpeed, 1, 1);
            gridMetrics.Controls.Add(_lblAvgSpeed, 2, 1);

            _pnlMetrics.Controls.Add(gridMetrics);
            _cardSpeed.Controls.Add(_pnlMetrics);

            // Row 8: Details & Classification
            _lblSpeedDetails = new Label
            {
                Text = "",
                Location = new Point(18, 388),
                AutoSize = true,
                Font = UITheme.SmallFont,
                ForeColor = UITheme.SubTextColor,
                Tag = "SubText"
            };
            _cardSpeed.Controls.Add(_lblSpeedDetails);

            _pnlFlow.Controls.Add(_cardSpeed);

            // -------------------------------------------------------------
            // CARD 4: SANITIZED ACTIVITY LOGS
            // -------------------------------------------------------------
            _cardLogs = new ModernCard { Height = 220 };
            Label lblLogHdr = new Label
            {
                Text = "SECURITY & DIAGNOSTIC EVENT LOGS (SANITIZED)",
                Font = UITheme.SubHeaderFont,
                ForeColor = UITheme.AccentColor,
                Location = new Point(18, 12),
                AutoSize = true,
                UseMnemonic = false
            };
            _cardLogs.Controls.Add(lblLogHdr);

            _txtLogs = new TextBox
            {
                Location = new Point(18, 38),
                Height = 130,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Font = new Font("Consolas", 9f),
                BackColor = UITheme.InputBgColor,
                ForeColor = UITheme.TextColor,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _cardLogs.Controls.Add(_txtLogs);

            _btnClearLogs = new ModernButton { Text = "Clear Logs", IconType = ButtonIcon.Delete, Location = new Point(18, 176), Width = 115, Height = 32, BorderRadius = 6 };
            _btnClearLogs.Click += (s, e) => { Logger.Clear(); _txtLogs.Clear(); };
            _cardLogs.Controls.Add(_btnClearLogs);

            _btnCopyLogs = new ModernButton { Text = "Copy Logs", IconType = ButtonIcon.Copy, Location = new Point(142, 176), Width = 115, Height = 32, BorderRadius = 6 };
            _btnCopyLogs.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(_txtLogs.Text))
                {
                    Clipboard.SetText(_txtLogs.Text);
                    MessageBox.Show("Logs copied to clipboard.", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            _cardLogs.Controls.Add(_btnCopyLogs);

            _pnlFlow.Controls.Add(_cardLogs);
            this.Controls.Add(_pnlFlow);
        }

        private void SetBenchmarkMode(bool isIperf)
        {
            _isIperfMode = isIperf;
            _btnModeIperf.IsPrimary = isIperf;
            _btnModeSmb.IsPrimary = !isIperf;
            _btnModeIperf.Invalidate();
            _btnModeSmb.Invalidate();

            if (_isIperfMode)
            {
                _lblSpeedTarget.Text = "Target IP / Host:";
                _lblDurationOrPayload.Text = "Duration:";
                _cmbDuration.Visible = true;
                _cmbPayloadSize.Visible = false;
                _btnStartSpeedTest.Text = "Start iPerf3 Test";
                _lblSpeedStatus.Text = "● Ready for 30s peer-to-peer TCP network speed benchmark with target IP.";
                _lblSpeedStatus.ForeColor = UITheme.SubTextColor;
            }
            else
            {
                _lblSpeedTarget.Text = "Target Share Path:";
                _lblDurationOrPayload.Text = "Payload:";
                _cmbDuration.Visible = false;
                _cmbPayloadSize.Visible = true;
                _btnStartSpeedTest.Text = "Start SMB Test";
                _lblSpeedStatus.Text = "● Ready to benchmark SMB file read/write speed on target shared folder or drive.";
                _lblSpeedStatus.ForeColor = UITheme.SubTextColor;
            }
        }

        private ModernButton CreateUnitPill(string text, SpeedUnit unit, int left)
        {
            ModernButton btn = new ModernButton
            {
                Text = text,
                Location = new Point(left, 108),
                Width = 62,
                Height = 28,
                IsPill = true,
                BorderRadius = 14,
                Font = UITheme.BoldFont
            };
            btn.Click += (s, e) =>
            {
                _currentUnit = unit;
                UpdateUnitPills();
                RenderSpeedMetrics();
            };
            return btn;
        }

        private void UpdateUnitPills()
        {
            _btnUnitMBps.IsPrimary = (_currentUnit == SpeedUnit.MBps);
            _btnUnitMbps.IsPrimary = (_currentUnit == SpeedUnit.Mbps);
            _btnUnitGBps.IsPrimary = (_currentUnit == SpeedUnit.GBps);
            _btnUnitGbps.IsPrimary = (_currentUnit == SpeedUnit.Gbps);

            _btnUnitMBps.Invalidate();
            _btnUnitMbps.Invalidate();
            _btnUnitGBps.Invalidate();
            _btnUnitGbps.Invalidate();
        }

        private void RenderSpeedMetrics()
        {
            if (_lastSpeedResult == null || !_lastSpeedResult.Success)
            {
                _lblWriteSpeed.Text = "---";
                _lblReadSpeed.Text = "---";
                _lblAvgSpeed.Text = "---";
                return;
            }

            _lblWriteSpeed.Text = NetworkManager.FormatSpeed(_lastSpeedResult.WriteSpeedBytesPerSec, _currentUnit);
            _lblReadSpeed.Text = NetworkManager.FormatSpeed(_lastSpeedResult.ReadSpeedBytesPerSec, _currentUnit);
            _lblAvgSpeed.Text = NetworkManager.FormatSpeed(_lastSpeedResult.AverageSpeedBytesPerSec, _currentUnit);
        }

        private void OnFlowResize(object sender, EventArgs e)
        {
            int targetWidth = Math.Max(_pnlFlow.ClientSize.Width - 65, 550);

            _pnlHeader.Width = targetWidth;
            _cardHost.Width = targetWidth;
            _cardPing.Width = targetWidth;
            _cardSpeed.Width = targetWidth;
            _cardLogs.Width = targetWidth;

            _lblHostDetails.Width = targetWidth - 36;

            // Ping layout
            _txtPingHost.Width = targetWidth - 135 - 135 - 120 - 32;
            _btnPing.Location = new Point(targetWidth - 120 - 135 - 12, 38);
            _btnTestPort.Location = new Point(targetWidth - 120 - 4, 38);
            _txtPingOutput.Width = targetWidth - 36;

            // Speed layout
            _cmbSpeedTarget.Width = targetWidth - 145 - 195;
            _btnStartSpeedTest.Location = new Point(targetWidth - 180, 72);
            _speedProgress.Width = targetWidth - 36;
            _txtIperfLog.Width = targetWidth - 36;
            _pnlMetrics.Width = targetWidth - 36;
            _txtLogs.Width = targetWidth - 36;
        }

        private void LoadDiagnostics()
        {
            string comp = NetworkManager.GetComputerName();
            string ip = NetworkManager.GetLocalIpAddress();
            string mask = NetworkManager.GetSubnetMask();
            bool isPriv = FirewallManager.IsPrivateNetwork();

            _lblHostDetails.Text = string.Format(
                "Computer Name : {0}\nLocal IPv4    : {1}   (Subnet Mask: {2})\nFirewall Mode : {3}",
                comp, ip, mask, isPriv ? "Private Network (SMB Inbound Active & Protected)" : "Public Network (SMB Inbound Blocked by Windows Firewall)");

            // Auto-populate Speed Target with real options
            _cmbSpeedTarget.Items.Clear();
            _cmbSpeedTarget.Items.Add(ip);

            var localShares = ShareManager.GetActiveShares();
            foreach (var s in localShares)
            {
                string unc = string.Format(@"\\{0}\{1}", ip, s.ShareName);
                if (!_cmbSpeedTarget.Items.Contains(unc))
                {
                    _cmbSpeedTarget.Items.Add(unc);
                }
            }

            var mappedDrives = DriveMapper.GetMappedDrives();
            foreach (var md in mappedDrives)
            {
                string d = md.DriveLetter;
                if (!string.IsNullOrEmpty(d) && !_cmbSpeedTarget.Items.Contains(d))
                {
                    _cmbSpeedTarget.Items.Add(d);
                }
            }

            if (localShares.Count > 0)
            {
                _cmbSpeedTarget.Text = string.Format(@"\\{0}\{1}", ip, localShares[0].ShareName);
            }
            else
            {
                _cmbSpeedTarget.Text = ip;
            }

            SetBenchmarkMode(true);
            _cmbSpeedTarget.Text = ip;

            StringBuilder sb = new StringBuilder();
            foreach (var l in Logger.GetRecentEntries())
            {
                sb.AppendLine(l.ToString());
            }
            _txtLogs.Text = sb.ToString();
            _txtLogs.SelectionStart = _txtLogs.Text.Length;
            _txtLogs.ScrollToCaret();
        }

        private void OnLogAppended(LogEntry entry)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            try
            {
                this.BeginInvoke(new Action(() =>
                {
                    _txtLogs.AppendText(entry.ToString() + Environment.NewLine);
                }));
            }
            catch { }
        }

        private void OnPingClick(object sender, EventArgs e)
        {
            string host = _txtPingHost.Text.Trim();
            if (string.IsNullOrEmpty(host))
            {
                MessageBox.Show("Please enter a target IP address or hostname.", "Host Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _btnPing.Enabled = false;
            _lblPingSummary.Text = "Pinging " + host + " with 32 bytes of data (measuring actual latency)...";
            _lblPingSummary.ForeColor = UITheme.SubTextColor;
            _txtPingOutput.Clear();

            StringBuilder sbOutput = new StringBuilder();
            sbOutput.AppendLine("Pinging " + host + " [32-byte ICMP packets]:");

            Task.Factory.StartNew(() =>
            {
                try
                {
                    var stats = NetworkManager.PingHostMulti(host, 4, 1500, (result, current, total) =>
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            _txtPingOutput.AppendText(string.Format("Ping #{0}: {1}\r\n", current, result.Message));
                        }));
                    });

                    this.BeginInvoke(new Action(() =>
                    {
                        if (stats.Received > 0)
                        {
                            _lblPingSummary.Text = string.Format(
                                "✔ Ping Latency: {0:F1} ms (Avg)   |   Min: {1} ms   |   Max: {2} ms   |   Loss: {3:F0}% ({4}/{5} received)",
                                stats.AvgMs, stats.MinMs, stats.MaxMs, stats.LossPercentage, stats.Received, stats.Sent);
                            _lblPingSummary.ForeColor = UITheme.SuccessColor;

                            // Automatically suggest this responding host for the speed test if in IP mode!
                            if (_isIperfMode && (_cmbSpeedTarget.Text == NetworkManager.GetLocalIpAddress() || string.IsNullOrEmpty(_cmbSpeedTarget.Text)))
                            {
                                _cmbSpeedTarget.Text = host;
                            }
                        }
                        else
                        {
                            _lblPingSummary.Text = string.Format("✖ Ping Failed. 100% packet loss (0/{0} received). Host is unreachable or dropping ICMP echo requests.", stats.Sent);
                            _lblPingSummary.ForeColor = UITheme.DangerColor;
                        }
                    }));
                }
                finally
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        _btnPing.Enabled = true;
                    }));
                }
            });
        }

        private void OnTestPortClick(object sender, EventArgs e)
        {
            string host = _txtPingHost.Text.Trim();
            if (string.IsNullOrEmpty(host))
            {
                MessageBox.Show("Please enter a host IP or hostname to test port 445.", "Host Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _btnTestPort.Enabled = false;
            _lblPingSummary.Text = "Probing TCP Port 445 on " + host + "...";
            _lblPingSummary.ForeColor = UITheme.SubTextColor;

            Task.Factory.StartNew(() =>
            {
                try
                {
                    bool success = NetworkManager.TestSmbPort(host);
                    this.BeginInvoke(new Action(() =>
                    {
                        if (success)
                        {
                            _lblPingSummary.Text = "✔ TCP Port 445 is OPEN! SMB file server is active and accessible.";
                            _lblPingSummary.ForeColor = UITheme.SuccessColor;
                            _txtPingOutput.AppendText(string.Format("TCP 445 Probe: Port is OPEN on {0} (SMB 2/3 service reachable)\r\n", host));
                        }
                        else
                        {
                            _lblPingSummary.Text = "✖ TCP Port 445 is CLOSED or FILTERED by Windows Firewall.";
                            _lblPingSummary.ForeColor = UITheme.DangerColor;
                            _txtPingOutput.AppendText(string.Format("TCP 445 Probe: Port is CLOSED or BLOCKED on {0}\r\n", host));
                        }
                    }));
                }
                finally
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        _btnTestPort.Enabled = true;
                    }));
                }
            });
        }

        private void OnStartSpeedTestClick(object sender, EventArgs e)
        {
            string target = _cmbSpeedTarget.Text.Trim();
            if (string.IsNullOrEmpty(target))
            {
                MessageBox.Show("Please enter a target host IP address (e.g. 192.168.137.62) or network share path.", "Target Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int duration = 30;
            if (_cmbDuration.SelectedIndex == 0) duration = 10;
            else if (_cmbDuration.SelectedIndex == 1) duration = 30;
            else if (_cmbDuration.SelectedIndex == 2) duration = 60;

            int payloadMb = 50;
            switch (_cmbPayloadSize.SelectedIndex)
            {
                case 0: payloadMb = 10; break;
                case 1: payloadMb = 50; break;
                case 2: payloadMb = 100; break;
                case 3: payloadMb = 250; break;
            }

            int paramValue = _isIperfMode ? duration : payloadMb;

            _btnStartSpeedTest.Enabled = false;
            _btnStartSpeedTest.Text = "Benchmarking...";
            _speedProgress.Value = 0;
            _txtIperfLog.Clear();
            _lblWriteSpeed.Text = "---";
            _lblReadSpeed.Text = "---";
            _lblAvgSpeed.Text = "---";
            _lblSpeedDetails.Text = "";

            if (_isIperfMode)
            {
                _lblSpeedStatus.Text = string.Format("Starting 100% real {0}s iPerf3 TCP benchmark with {1}...", duration, target);
                _txtIperfLog.AppendText(string.Format("Connecting to {0}:{1} [{2}-Second iPerf3 Continuous Benchmark]...\r\n", target, NetworkManager.SpeedTestPort, duration));
            }
            else
            {
                _lblSpeedStatus.Text = string.Format("Starting SMB file read/write speed test on {0} ({1} MB)...", target, payloadMb);
                _txtIperfLog.AppendText(string.Format("Starting SMB file read/write transfer test on {0}...\r\n", target));
            }
            _lblSpeedStatus.ForeColor = UITheme.SubTextColor;

            Task.Factory.StartNew(() =>
            {
                try
                {
                    _lastSpeedResult = NetworkManager.RunSpeedTest(target, paramValue, _isIperfMode, (progress) =>
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            _speedProgress.Value = Math.Min(100, Math.Max(0, progress.Percent));
                            _lblSpeedStatus.Text = progress.Stage;
                            if (progress.CurrentSpeedBytesPerSec > 0)
                            {
                                _lblAvgSpeed.Text = NetworkManager.FormatSpeed(progress.CurrentSpeedBytesPerSec, _currentUnit);
                            }
                            if (!string.IsNullOrEmpty(progress.IntervalLog))
                            {
                                _txtIperfLog.AppendText(progress.IntervalLog + "\r\n");
                            }
                        }));
                    });

                    this.BeginInvoke(new Action(() =>
                    {
                        _speedProgress.Value = 100;
                        if (_lastSpeedResult.Success)
                        {
                            RenderSpeedMetrics();
                            string modeDesc = _lastSpeedResult.IsSocketMode 
                                ? string.Format("Real Physical TCP Stream ({0:F1}s)", _lastSpeedResult.DurationSeconds) 
                                : "Real Physical SMB File Transfer (Read/Write)";
                            _lblSpeedStatus.Text = string.Format("✔ Real physical speed benchmark completed ({0}) in {1:F2}s.", modeDesc, _lastSpeedResult.DurationSeconds);
                            _lblSpeedStatus.ForeColor = UITheme.SuccessColor;

                            // Estimate connection type
                            string rating = "Standard Network";
                            double mbps = (_lastSpeedResult.AverageSpeedBytesPerSec * 8.0) / 1000000.0;
                            if (mbps >= 8000) rating = "10 Gigabit Network (10 Gbps) Verified";
                            else if (mbps >= 2000) rating = "2.5 Gigabit Network (2.5 Gbps) Verified";
                            else if (mbps >= 800) rating = "Gigabit Local Network (1 Gbps) Verified";
                            else if (mbps >= 300) rating = "High-Speed Wi-Fi 5/6 (300+ Mbps) Verified";
                            else if (mbps >= 80) rating = "100 Mbps Ethernet / 2.4 GHz Wi-Fi";

                            string totalData = NetworkManager.FormatBytesCompact(_lastSpeedResult.TotalBytesTransferred);
                            _lblSpeedDetails.Text = string.Format("Classification: {0}   •   Transferred: {1} physically in {2:F1}s",
                                rating, totalData, _lastSpeedResult.DurationSeconds);
                        }
                        else
                        {
                            _lblSpeedStatus.Text = "✖ Speed test failed: " + _lastSpeedResult.ErrorMessage;
                            _lblSpeedStatus.ForeColor = UITheme.DangerColor;
                            _txtIperfLog.AppendText("\r\n[ERROR] " + _lastSpeedResult.ErrorMessage + "\r\n");
                        }
                    }));
                }
                finally
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        _btnStartSpeedTest.Enabled = true;
                        _btnStartSpeedTest.Text = _isIperfMode ? "Start iPerf3 Test" : "Start SMB Test";
                    }));
                }
            });
        }
    }
}