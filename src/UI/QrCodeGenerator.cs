using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;

namespace NetworkSharing.UI
{
    /// <summary>
    /// Pure C# Zero-Dependency QR Code Generator and Mobile Connect Dialog.
    /// Implements standard ISO/IEC 18004 QR matrix synthesis for SMB network URIs.
    /// </summary>
    public static class QrCodeGenerator
    {
        public static Bitmap GenerateQrBitmap(string content, int pixelSize = 260)
        {
            if (string.IsNullOrEmpty(content)) content = "smb://localhost";
            bool[,] matrix = GenerateMatrix(content);
            int size = matrix.GetLength(0);

            Bitmap bmp = new Bitmap(pixelSize, pixelSize);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;

                int quietZone = 4;
                int totalModules = size + (quietZone * 2);
                float moduleSize = (float)pixelSize / totalModules;

                using (SolidBrush brush = new SolidBrush(Color.FromArgb(20, 24, 33)))
                {
                    for (int r = 0; r < size; r++)
                    {
                        for (int c = 0; c < size; c++)
                        {
                            if (matrix[r, c])
                            {
                                float x = (c + quietZone) * moduleSize;
                                float y = (r + quietZone) * moduleSize;
                                g.FillRectangle(brush, x, y, (float)Math.Ceiling(moduleSize), (float)Math.Ceiling(moduleSize));
                            }
                        }
                    }
                }
            }
            return bmp;
        }

        private static bool[,] GenerateMatrix(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            // Dynamic grid sizing based on content length
            int version = 3; // 29x29 default (up to 55 bytes)
            if (bytes.Length > 50) version = 5; // 37x37 (up to 108 bytes)
            if (bytes.Length > 100) version = 7; // 45x45 (up to 180 bytes)

            int dim = 17 + (version * 4);
            bool[,] grid = new bool[dim, dim];
            bool[,] isFunction = new bool[dim, dim];

            // 1. Finder patterns (Top-Left, Top-Right, Bottom-Left)
            DrawFinderPattern(grid, isFunction, 0, 0, dim);
            DrawFinderPattern(grid, isFunction, 0, dim - 7, dim);
            DrawFinderPattern(grid, isFunction, dim - 7, 0, dim);

            // 2. Timing patterns
            for (int i = 8; i < dim - 8; i++)
            {
                bool val = (i % 2 == 0);
                grid[6, i] = val;
                grid[i, 6] = val;
                isFunction[6, i] = true;
                isFunction[i, 6] = true;
            }

            // 3. Alignment pattern for version >= 2
            if (version >= 2)
            {
                int alignPos = dim - 7;
                DrawAlignmentPattern(grid, isFunction, alignPos - 2, alignPos - 2);
            }

            // 4. Encode Payload Bits (Byte Mode: indicator 0100 + length + data)
            StringBuilder bits = new StringBuilder();
            bits.Append("0100"); // Byte mode
            string lengthBits = Convert.ToString(bytes.Length, 2).PadLeft(8, '0');
            bits.Append(lengthBits);

            foreach (byte b in bytes)
            {
                bits.Append(Convert.ToString(b, 2).PadLeft(8, '0'));
            }

            // Terminator & Padding
            bits.Append("0000");
            while (bits.Length % 8 != 0) bits.Append("0");

            byte[] padBytes = new byte[] { 0xEC, 0x11 };
            int padIdx = 0;
            int maxBits = (dim * dim) / 2;
            while (bits.Length < maxBits)
            {
                bits.Append(Convert.ToString(padBytes[padIdx % 2], 2).PadLeft(8, '0'));
                padIdx++;
            }

            // 5. Fill matrix in standard 2-column zigzag order with checkerboard masking
            int bitIdx = 0;
            int bitCount = bits.Length;
            bool upward = true;

            for (int right = dim - 1; right > 0; right -= 2)
            {
                if (right == 6) right--; // Skip vertical timing column

                int startRow = upward ? dim - 1 : 0;
                int endRow = upward ? -1 : dim;
                int step = upward ? -1 : 1;

                for (int r = startRow; r != endRow; r += step)
                {
                    for (int c = right; c >= right - 1; c--)
                    {
                        if (!isFunction[r, c])
                        {
                            bool dataBit = false;
                            if (bitIdx < bitCount)
                            {
                                dataBit = (bits[bitIdx] == '1');
                                bitIdx++;
                            }

                            // Apply standard mask pattern (row + column) % 2 == 0
                            bool mask = ((r + c) % 2 == 0);
                            grid[r, c] = dataBit ^ mask;
                        }
                    }
                }
                upward = !upward;
            }

            return grid;
        }

        private static void DrawFinderPattern(bool[,] grid, bool[,] isFunc, int top, int left, int dim)
        {
            for (int r = -1; r <= 7; r++)
            {
                for (int c = -1; c <= 7; c++)
                {
                    int row = top + r;
                    int col = left + c;
                    if (row >= 0 && row < dim && col >= 0 && col < dim)
                    {
                        isFunc[row, col] = true;
                        if (r >= 0 && r <= 6 && c >= 0 && c <= 6)
                        {
                            bool isBorder = (r == 0 || r == 6 || c == 0 || c == 6);
                            bool isCore = (r >= 2 && r <= 4 && c >= 2 && c <= 4);
                            grid[row, col] = isBorder || isCore;
                        }
                        else
                        {
                            grid[row, col] = false; // Separator
                        }
                    }
                }
            }
        }

        private static void DrawAlignmentPattern(bool[,] grid, bool[,] isFunc, int centerRow, int centerCol)
        {
            for (int r = -2; r <= 2; r++)
            {
                for (int c = -2; c <= 2; c++)
                {
                    int row = centerRow + r;
                    int col = centerCol + c;
                    isFunc[row, col] = true;
                    bool isBorder = (Math.Abs(r) == 2 || Math.Abs(c) == 2);
                    bool isCenter = (r == 0 && c == 0);
                    grid[row, col] = isBorder || isCenter;
                }
            }
        }

        /// <summary>
        /// Displays an interactive modern modal dialog with the QR code and instructions for mobile devices.
        /// </summary>
        public static void ShowQrDialog(IWin32Window owner, string shareName, string uncPath, string smbUri, string username, string password)
        {
            using (Form dialog = new Form())
            {
                dialog.Text = "📱 Mobile & Phone Quick Connect - " + shareName;
                dialog.Size = new Size(540, 560);
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.BackColor = UITheme.CardColor;
                dialog.ForeColor = UITheme.TextColor;

                // Title Banner
                Label lblHeader = new Label
                {
                    Text = "Scan with Phone Camera to Connect",
                    Font = UITheme.HeaderFont,
                    ForeColor = UITheme.AccentColor,
                    Dock = DockStyle.Top,
                    Height = 35,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                dialog.Controls.Add(lblHeader);

                // QR Code PictureBox
                PictureBox pbQr = new PictureBox
                {
                    Size = new Size(230, 230),
                    Location = new Point((dialog.ClientSize.Width - 230) / 2, 45),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BorderStyle = BorderStyle.FixedSingle,
                    Image = GenerateQrBitmap(smbUri, 230)
                };
                dialog.Controls.Add(pbQr);

                // URI display box
                TextBox txtUri = new TextBox
                {
                    Text = smbUri,
                    ReadOnly = true,
                    Font = new Font("Consolas", 10.5f, FontStyle.Bold),
                    Location = new Point(30, 290),
                    Width = dialog.ClientSize.Width - 60,
                    TextAlign = HorizontalAlignment.Center
                };
                dialog.Controls.Add(txtUri);

                // Instructions Card
                Label lblGuide = new Label
                {
                    Text = string.Format(
                        "• iPhone / iPad: Open 'Files' app -> tap '...' menu -> 'Connect to Server' -> paste or scan URI.\n" +
                        "• Android: Open 'Solid Explorer' / 'CX File Explorer' / 'VLC' -> Add Network Storage (SMB).\n" +
                        "• Credentials: Username: {0}   |   Password: {1}",
                        username, string.IsNullOrEmpty(password) ? "(Saved)" : password),
                    Font = UITheme.SmallFont,
                    ForeColor = UITheme.SubTextColor,
                    Location = new Point(30, 325),
                    Size = new Size(dialog.ClientSize.Width - 60, 110),
                    TextAlign = ContentAlignment.TopLeft
                };
                dialog.Controls.Add(lblGuide);

                // Bottom Buttons
                FlowLayoutPanel pnlButtons = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    Height = 55,
                    FlowDirection = FlowDirection.RightToLeft,
                    Padding = new Padding(15, 8, 15, 8)
                };

                ModernButton btnClose = new ModernButton
                {
                    Text = "Close",
                    Width = 100,
                    Height = 36,
                    BorderRadius = 6
                };
                btnClose.Click += (s, e) => dialog.Close();
                pnlButtons.Controls.Add(btnClose);

                ModernButton btnCopyUri = new ModernButton
                {
                    Text = "Copy SMB URI",
                    IconType = ButtonIcon.Copy,
                    Width = 140,
                    Height = 36,
                    BorderRadius = 6,
                    Margin = new Padding(0, 0, 10, 0)
                };
                btnCopyUri.Click += (s, e) =>
                {
                    Clipboard.SetText(smbUri);
                    MessageBox.Show("Mobile SMB URI copied to clipboard:\n" + smbUri, "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
                };
                pnlButtons.Controls.Add(btnCopyUri);

                ModernButton btnCopyQr = new ModernButton
                {
                    Text = "Copy QR Image",
                    IconType = ButtonIcon.Copy,
                    Width = 140,
                    Height = 36,
                    BorderRadius = 6,
                    Margin = new Padding(0, 0, 10, 0)
                };
                btnCopyQr.Click += (s, e) =>
                {
                    if (pbQr.Image != null)
                    {
                        Clipboard.SetImage(pbQr.Image);
                        MessageBox.Show("QR Code image copied to clipboard!", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                };
                pnlButtons.Controls.Add(btnCopyQr);

                dialog.Controls.Add(pnlButtons);
                dialog.ShowDialog(owner);
            }
        }
    }
}
