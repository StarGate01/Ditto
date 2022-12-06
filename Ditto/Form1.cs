using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;

namespace Ditto
{
    public partial class Form1 : Form
    {

        #region Structs and Enums

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;

            public RECT(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DWM_THUMBNAIL_PROPERTIES
        {
            public DwmThumbnailPropertiesFlags dwFlags;
            public RECT rcDestination;
            public RECT rcSource;
            public byte opacity;
            public int fVisible;
            public int fSourceClientAreaOnly;

            public DWM_THUMBNAIL_PROPERTIES(DwmThumbnailPropertiesFlags dwFlags, 
                RECT rcDestination = new RECT(), RECT rcSource = new RECT(), 
                byte opacity = 255, int fVisible = 1, int fSourceClientAreaOnly = 1)
            {
                this.dwFlags = dwFlags;
                this.rcDestination = rcDestination;
                this.rcSource = rcSource;
                this.opacity = opacity;
                this.fVisible = fVisible;
                this.fSourceClientAreaOnly = fSourceClientAreaOnly;
            }
        }

        private enum GetAncestorFlags
        {
            GetParent = 1,
            GetRoot = 2,
            GetRootOwner = 3
        }

        private enum DwmThumbnailPropertiesFlags
        {
            RectDestination = 1,
            RectSource = 2,
            Opacity = 4,
            Visible = 8,
            SourceClientAreaOnly = 16
        }

        #endregion

        #region Imports

        private delegate bool EnumWindowsProc(IntPtr hWnd, int lParam);

        [DllImport("USER32.DLL")]
        private static extern bool EnumWindows(EnumWindowsProc enumFunc, int lParam);

        [DllImport("USER32.DLL")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("USER32.DLL")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("USER32.DLL")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("USER32.DLL")]
        private static extern IntPtr GetShellWindow();

        [DllImport("dwmapi.dll", SetLastError = true)]
        private static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmUpdateThumbnailProperties(IntPtr hThumbnail, ref DWM_THUMBNAIL_PROPERTIES props);

        [DllImport("dwmapi.dll")]
        private static extern int DwmUnregisterThumbnail(IntPtr thumb);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr GetAncestor(IntPtr hwnd, GetAncestorFlags flags);

        [DllImport("dwmapi.dll", PreserveSig = false)]
        private static extern void DwmQueryThumbnailSourceSize(IntPtr hThumbnail, out Size size);

        private static IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size > 4) return GetClassLongPtr64(hWnd, nIndex);
            else return new IntPtr(GetClassLongPtr32(hWnd, nIndex));
        }

        [DllImport("user32.dll", EntryPoint = "GetClassLong")]
        private static extern uint GetClassLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetClassLongPtr")]
        private static extern IntPtr GetClassLongPtr64(IntPtr hWnd, int nIndex);

        #endregion

        #region Contants

        const int GCL_HICON = -14;

        #endregion


        private IntPtr thumbnail = IntPtr.Zero;
        private bool fullscreen = false;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            RefreshWindowsList();
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F11 || 
                (e.KeyCode == Keys.Escape && fullscreen)) ToggleFullscreen();
        }

        private void notifyIconMain_Click(object sender, EventArgs e)
        {
            RefreshWindowsList();
        }

        private void toolStripMenuItemMain_Click(object sender, EventArgs e)
        {
            ReleaseMirror();
            SetupMirror((IntPtr)((ToolStripMenuItem)sender).Tag);
            Text = "Ditto - " + ((ToolStripMenuItem)sender).Text;
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            RefreshMirror();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            ReleaseMirror();
        }

        private void RefreshWindowsList()
        {
            List<ToolStripItem> windows = new List<ToolStripItem>();

            IntPtr shellWindow = GetShellWindow();
            EnumWindows(delegate (IntPtr hwnd, int lParam)
            {
                if (hwnd == shellWindow) return true;
                if (!IsWindowVisible(hwnd)) return true;

                int length = GetWindowTextLength(hwnd);
                if (length == 0) return true;

                if (hwnd != GetAncestor(hwnd, GetAncestorFlags.GetRoot)) return true;

                StringBuilder builder = new StringBuilder(length);
                GetWindowText(hwnd, builder, length + 1);
                string name = builder.ToString();
                if (name.StartsWith("Ditto")) return true;
                if (name.Length > 50) name = name.Substring(0, 50) + "...";

                ToolStripMenuItem item = new ToolStripMenuItem(name);
                item.Tag = hwnd;
                item.Click += toolStripMenuItemMain_Click;

                IntPtr hicon = GetClassLongPtr(hwnd, GCL_HICON);
                try
                {
                    Icon icon = Icon.FromHandle(hicon);
                    item.Image = icon.ToBitmap();
                }
                catch { }

                windows.Add(item);

                return true;
            }, 0);

            contextMenuStripWindows.Items.Clear();
            contextMenuStripWindows.Items.AddRange(windows.ToArray());
            contextMenuStripWindows.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem fullscreen = new ToolStripMenuItem("Toggle Fullscreen (F11)");
            fullscreen.Click += Fullscreen_Click;
            fullscreen.Image = Properties.Resources.FullScreen_16x;
            contextMenuStripWindows.Items.Add(fullscreen);
            ToolStripMenuItem about = new ToolStripMenuItem("About Ditto");
            about.Click += About_Click;
            about.Image = Properties.Resources.ditto.ToBitmap();
            contextMenuStripWindows.Items.Add(about);
        }

        private void Fullscreen_Click(object sender, EventArgs e)
        {
            ToggleFullscreen();
        }

        private void About_Click(object sender, EventArgs e)
        {
            MessageBox.Show(this, "Ditto Window Mirror v0.2" + Environment.NewLine + Environment.NewLine +
                "Copyright © Christoph Honal 2022" + Environment.NewLine +
                "https://github.com/StarGate01/Ditto" + Environment.NewLine +
                "Distributed under the MIT license",
                "About Ditto", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SetupMirror(IntPtr hwnd)
        {
            ReleaseMirror();
            thumbnail = IntPtr.Zero;
            int hr = DwmRegisterThumbnail(Handle, hwnd, out thumbnail);
            if (hr == 0) RefreshMirror();
        }
        
        private void RefreshMirror()
        {
            if (thumbnail == IntPtr.Zero) return;

            DwmQueryThumbnailSourceSize(thumbnail, out Size thumbnailSize);
            float thumbnailAR = (float)thumbnailSize.Width / thumbnailSize.Height;
            float displayAR = (float)ClientSize.Width / ClientSize.Height;
            int displayHeight = (int)(thumbnailSize.Height * ((float)ClientSize.Width / thumbnailSize.Width));
            int displayWidth = ClientSize.Width;
            if (thumbnailAR < displayAR)
            {
                displayWidth = (int)(thumbnailSize.Width * ((float)ClientSize.Height / thumbnailSize.Height));
                displayHeight = ClientSize.Height;
            }
            int displayLeft = (ClientSize.Width - displayWidth) / 2;
            int displayTop = (ClientSize.Height - displayHeight) / 2;

            DWM_THUMBNAIL_PROPERTIES dskThumbProps = new DWM_THUMBNAIL_PROPERTIES(
                DwmThumbnailPropertiesFlags.SourceClientAreaOnly | 
                DwmThumbnailPropertiesFlags.Visible | 
                DwmThumbnailPropertiesFlags.RectDestination,
                new RECT(displayLeft, displayTop, displayWidth + displayLeft, displayHeight + displayTop));
            DwmUpdateThumbnailProperties(thumbnail, ref dskThumbProps);
        }

        private void ReleaseMirror()
        {
            if (thumbnail != IntPtr.Zero) DwmUnregisterThumbnail(thumbnail);
            Text = "Ditto";
        }

        private void ToggleFullscreen()
        {
            fullscreen = !fullscreen;
            if (fullscreen)
            {
                WindowState = FormWindowState.Normal;
                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Maximized;
            }
            else
            {
                FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
                WindowState = FormWindowState.Normal;
            }
        }

    }
}
