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
        public struct RECT
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
        public struct DWM_THUMBNAIL_PROPERTIES
        {
            public int dwFlags;
            public RECT rcDestination;
            public RECT rcSource;
            public byte opacity;
            public int fVisible;
            public int fSourceClientAreaOnly;
        }

        enum GetAncestorFlags
        {
            GetParent = 1,
            GetRoot = 2,
            GetRootOwner = 3
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
        static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        public static extern int DwmUpdateThumbnailProperties(IntPtr hThumbnail, ref DWM_THUMBNAIL_PROPERTIES props);

        [DllImport("dwmapi.dll")]
        static extern int DwmUnregisterThumbnail(IntPtr thumb);

        [DllImport("user32.dll", ExactSpelling = true)]
        static extern IntPtr GetAncestor(IntPtr hwnd, GetAncestorFlags flags);

        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern void DwmQueryThumbnailSourceSize(IntPtr hThumbnail, out Size size);

        public static IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size > 4)
                return GetClassLongPtr64(hWnd, nIndex);
            else
                return new IntPtr(GetClassLongPtr32(hWnd, nIndex));
        }

        [DllImport("user32.dll", EntryPoint = "GetClassLong")]
        public static extern uint GetClassLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetClassLongPtr")]
        public static extern IntPtr GetClassLongPtr64(IntPtr hWnd, int nIndex);

        #endregion

        #region Contants

        const uint DWM_TNP_SOURCECLIENTAREAONLY = 0x00000010;
        const uint DWM_TNP_VISIBLE = 0x00000008;
        const uint DWM_TNP_RECTDESTINATION = 0x00000001;

        const int GCL_HICON = -14;

        #endregion


        private IntPtr thumbnail = IntPtr.Zero;

        public Form1()
        {
            InitializeComponent();
        }

        private void notifyIconMain_Click(object sender, EventArgs e)
        {
            refreshWindowsList();
            //contextMenuStripWindows.Show();
        }

        private void toolStripMenuItemMain_Click(object sender, EventArgs e)
        {
            releaseMirror();
            setupMirror((IntPtr)((ToolStripMenuItem)sender).Tag);
        }

        private void Form1_ResizeEnd(object sender, EventArgs e)
        {
            refreshMirror();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            releaseMirror();
        }

        private void refreshWindowsList()
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
        }

        private void setupMirror(IntPtr hwnd)
        {
            releaseMirror();
            thumbnail = IntPtr.Zero;
            int hr = DwmRegisterThumbnail(Handle, hwnd, out thumbnail);
            if (hr == 0)
            {
                refreshMirror();
            }
        }

        private void refreshMirror()
        {
            if (thumbnail == IntPtr.Zero) return; 

            Size thumbnailSize = new Size();
            DwmQueryThumbnailSourceSize(thumbnail, out thumbnailSize);
            ClientSize = new Size(ClientSize.Width, (int)(thumbnailSize.Height * ((float)ClientSize.Width / thumbnailSize.Width)));

            RECT dest = new RECT(0, 0, ClientSize.Width, ClientSize.Height);
            DWM_THUMBNAIL_PROPERTIES dskThumbProps = new DWM_THUMBNAIL_PROPERTIES();
            dskThumbProps.dwFlags = (int)(DWM_TNP_SOURCECLIENTAREAONLY | DWM_TNP_VISIBLE | DWM_TNP_RECTDESTINATION);
            dskThumbProps.fSourceClientAreaOnly = 1;
            dskThumbProps.fVisible = 1;
            dskThumbProps.rcDestination = dest;
            DwmUpdateThumbnailProperties(thumbnail, ref dskThumbProps);
        }

        private void releaseMirror()
        {
            if (thumbnail != IntPtr.Zero)
            {
                DwmUnregisterThumbnail(thumbnail);
            }
        }

    }
}
