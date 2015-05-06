﻿using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Shapeshifter.UserInterface.WindowsDesktop.Services.Interfaces;
using static Shapeshifter.UserInterface.WindowsDesktop.Services.Api.IconApi;

namespace Shapeshifter.UserInterface.WindowsDesktop.Services
{
    class FileIconService : IFileIconService
    {
        public byte[] GetIcon(string path, bool allowThumbnails, int dimensions = 256)
        {
            var bitmapHandle = IntPtr.Zero;

            var uniqueId = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");

            IShellItem rawFactory;
            SHCreateItemFromParsingName(path, IntPtr.Zero, uniqueId, out rawFactory);

            var factory = (IShellItemImageFactory)rawFactory;

            var iconOnlyScope = SIIGBF.SIIGBF_ICONONLY;
            var sizeScope = SIIGBF.SIIGBF_BIGGERSIZEOK;
            var scope = (allowThumbnails ? SIIGBF.SIIGBF_THUMBNAILONLY : iconOnlyScope) | sizeScope;

            try
            {
                factory.GetImage(new SIZE(dimensions, dimensions), scope, ref bitmapHandle);
            }
            catch (COMException ex) when (ex.ErrorCode == -2147175936 && allowThumbnails)
            {
                //no thumbnail available. fetch the icon instead.
                factory.GetImage(new SIZE(dimensions, dimensions), iconOnlyScope | sizeScope, ref bitmapHandle);
            }

            if (bitmapHandle != IntPtr.Zero)
            {
                var bitmap = new BITMAP();
                var bufferSize = Marshal.SizeOf(bitmap);

                GetObject(bitmapHandle, bufferSize, out bitmap);

                var bytes = new byte[bitmap.WidthBytes * bitmap.Height];
                Marshal.Copy(bitmap.Bits, bytes, 0, bytes.Length);

                DeleteObject(bitmapHandle);

                return bytes;
            }
            else
            {

                //the handle is 0, assume that the data comes from Windows itself.
                return null;

            }
        }
    }
}