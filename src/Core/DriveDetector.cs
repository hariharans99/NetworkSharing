using System;
using System.Collections.Generic;
using System.IO;
using NetworkSharing.Logging;

namespace NetworkSharing.Core
{
    public class DriveItem
    {
        public string Name { get; set; }           // e.g. "C:\"
        public string Letter { get; set; }         // e.g. "C"
        public string Label { get; set; }          // e.g. "Windows"
        public string DriveTypeStr { get; set; }   // "Fixed", "Removable"
        public string Format { get; set; }         // "NTFS"
        public long TotalBytes { get; set; }
        public long FreeBytes { get; set; }
        public bool IsSystemDrive { get; set; }

        public double TotalGB
        {
            get { return Math.Round((double)TotalBytes / (1024 * 1024 * 1024), 1); }
        }

        public double FreeGB
        {
            get { return Math.Round((double)FreeBytes / (1024 * 1024 * 1024), 1); }
        }

        public int PercentFree
        {
            get { return TotalBytes > 0 ? (int)((FreeBytes * 100) / TotalBytes) : 0; }
        }

        public override string ToString()
        {
            string sysTag = IsSystemDrive ? " [OS Drive]" : "";
            return string.Format("{0} ({1}) - {2} GB free of {3} GB ({4}%){5}", 
                Name, string.IsNullOrEmpty(Label) ? "Local Disk" : Label, FreeGB, TotalGB, PercentFree, sysTag);
        }
    }

    public static class DriveDetector
    {
        public static List<DriveItem> GetAvailableDrives()
        {
            List<DriveItem> result = new List<DriveItem>();
            string systemRoot = Path.GetPathRoot(Environment.SystemDirectory);

            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                foreach (DriveInfo d in drives)
                {
                    if (!d.IsReady) continue;
                    if (d.DriveType != DriveType.Fixed && d.DriveType != DriveType.Removable) continue;

                    bool isSys = d.Name.Equals(systemRoot, StringComparison.OrdinalIgnoreCase);

                    DriveItem item = new DriveItem
                    {
                        Name = d.Name,
                        Letter = d.Name.Substring(0, 1).ToUpperInvariant(),
                        Label = string.IsNullOrEmpty(d.VolumeLabel) ? "Local Disk" : d.VolumeLabel,
                        DriveTypeStr = d.DriveType.ToString(),
                        Format = d.DriveFormat,
                        TotalBytes = d.TotalSize,
                        FreeBytes = d.AvailableFreeSpace,
                        IsSystemDrive = isSys
                    };
                    result.Add(item);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error enumerating drives", ex);
            }

            return result;
        }
    }
}
