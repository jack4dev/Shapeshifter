﻿using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace Shapeshifter.WindowsDesktop.Controls.Clipboard.Unwrappers
{
    using System;
    using System.Drawing;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Interop;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;

    using Interfaces;

    using Native;
    using Native.Interfaces;

    using Services.Images.Interfaces;

    class BitmapUnwrapper: IMemoryUnwrapper
    {
        readonly IImagePersistenceService imagePersistenceService;

        readonly IClipboardNativeApi clipboardNativeApi;

        readonly IGeneralNativeApi generalNativeApi;

        readonly IImageNativeApi imageNativeApi;

        public BitmapUnwrapper(
            IImagePersistenceService imagePersistenceService,
            IClipboardNativeApi clipboardNativeApi,
            IGeneralNativeApi generalNativeApi,
            IImageNativeApi imageNativeApi)
        {
            this.imagePersistenceService = imagePersistenceService;
            this.clipboardNativeApi = clipboardNativeApi;
            this.generalNativeApi = generalNativeApi;
            this.imageNativeApi = imageNativeApi;
        }

        public bool CanUnwrap(uint format)
        {
            return (format == ClipboardNativeApi.CF_DIBV5) ||
                   (format == ClipboardNativeApi.CF_DIB) ||
                   (format == ClipboardNativeApi.CF_BITMAP) ||
                   (format == ClipboardNativeApi.CF_DIF);
        }

        public byte[] UnwrapStructure(uint format)
        {
            //TODO: it is very sad that we invoke System.Drawing here to get the job done. probably not very optimal.

            var bitmapVersionFivePointer = clipboardNativeApi.GetClipboardData(ClipboardNativeApi.CF_DIBV5);
            var bitmapVersionFiveHeader =
                (ImageNativeApi.BITMAPV5HEADER)
                Marshal.PtrToStructure(bitmapVersionFivePointer, typeof (ImageNativeApi.BITMAPV5HEADER));

            if (bitmapVersionFiveHeader.bV5Compression != ImageNativeApi.BI_RGB)
            {
                return HandleBitmapVersionFive(bitmapVersionFivePointer, bitmapVersionFiveHeader);
            }

            var bitmapVersionOneBytes = clipboardNativeApi.GetClipboardDataBytes(ClipboardNativeApi.CF_DIB);
            var bitmapVersionOneHeader =
                generalNativeApi.ByteArrayToStructure<ImageNativeApi.BITMAPINFOHEADER>(bitmapVersionOneBytes);

            return HandleBitmapVersionOne(bitmapVersionOneBytes, bitmapVersionOneHeader);
        }

        byte[] HandleBitmapVersionOne(
            byte[] bitmapVersionOneBytes,
            ImageNativeApi.BITMAPINFOHEADER bitmapVersionOneHeader)
        {
            var bitmap = CreateBitmapVersionOne(bitmapVersionOneBytes, bitmapVersionOneHeader);
            return imagePersistenceService.ConvertBitmapSourceToByteArray(bitmap);
        }

        BitmapFrame CreateBitmapVersionOne(
            byte[] bitmapVersionOneBytes,
            ImageNativeApi.BITMAPINFOHEADER bitmapVersionOneHeader)
        {
            var fileHeaderSize = Marshal.SizeOf(typeof (ImageNativeApi.BITMAPFILEHEADER));
            var infoHeaderSize = bitmapVersionOneHeader.biSize;
            var fileSize = fileHeaderSize + bitmapVersionOneHeader.biSize +
                           bitmapVersionOneHeader.biSizeImage;

            var fileHeader = new ImageNativeApi.BITMAPFILEHEADER
            {
                bfType = ImageNativeApi.BITMAPFILEHEADER.BM,
                bfSize = fileSize,
                bfReserved1 = 0,
                bfReserved2 = 0,
                bfOffBits = fileHeaderSize + infoHeaderSize + bitmapVersionOneHeader.biClrUsed*4
            };

            var fileHeaderBytes = generalNativeApi.StructureToByteArray(fileHeader);

            var bitmapStream = new MemoryStream();
            bitmapStream.Write(fileHeaderBytes, 0, fileHeaderSize);
            bitmapStream.Write(bitmapVersionOneBytes, 0, bitmapVersionOneBytes.Length);
            bitmapStream.Seek(0, SeekOrigin.Begin);

            var bitmap = BitmapFrame.Create(bitmapStream);
            return bitmap;
        }

        byte[] HandleBitmapVersionFive(IntPtr pointer, ImageNativeApi.BITMAPV5HEADER infoHeader)
        {
            using (var drawingBitmap = CreateDrawingBitmapFromVersionOnePointer(pointer, infoHeader)
                )
            {
                var renderTargetBitmapSource = new RenderTargetBitmap(
                    infoHeader.bV5Width,
                    infoHeader.bV5Height,
                    96,
                    96,
                    PixelFormats.Pbgra32);
                var visual = new DrawingVisual();
                var drawingContext = visual.RenderOpen();

                drawingContext.DrawImage(
                    CreateBitmapSourceFromBitmap(drawingBitmap),
                    new Rect(
                        0,
                        0,
                        infoHeader.bV5Width,
                        infoHeader.bV5Height));

                renderTargetBitmapSource.Render(visual);

                return
                    imagePersistenceService.ConvertBitmapSourceToByteArray(renderTargetBitmapSource);
            }
        }

        static Bitmap ConvertBitmapTo32Bit(Bitmap sourceBitmap)
        {
            if (sourceBitmap.PixelFormat == DrawingPixelFormat.Format32bppPArgb)
            {
                return sourceBitmap;
            }

            var targetBitmap = new Bitmap(
                sourceBitmap.Width,
                sourceBitmap.Height,
                DrawingPixelFormat.Format32bppPArgb);
            using (var graphics = Graphics.FromImage(targetBitmap))
                graphics.DrawImage(
                    sourceBitmap,
                    new Rectangle(0, 0, sourceBitmap.Width, sourceBitmap.Height));
            sourceBitmap.Dispose();
            return targetBitmap;
        }

        static Bitmap CreateDrawingBitmapFromVersionOnePointer(
            IntPtr pointer,
            ImageNativeApi.BITMAPV5HEADER infoHeader)
        {
            var stride = (int) (infoHeader.bV5SizeImage/infoHeader.bV5Height);
            var bitmap = new Bitmap(
                infoHeader.bV5Width,
                infoHeader.bV5Height,
                stride,
                infoHeader.bV5BitCount == 24
                    ? DrawingPixelFormat.Format24bppRgb
                    : DrawingPixelFormat.Format32bppPArgb,
                new IntPtr(pointer.ToInt64() + infoHeader.bV5Size));
            return ConvertBitmapTo32Bit(bitmap);
        }

        BitmapSource CreateBitmapSourceFromBitmap(Bitmap bitmap)
        {
            if (bitmap == null)
            {
                throw new ArgumentNullException(nameof(bitmap));
            }

            var bitmapHandle = bitmap.GetHbitmap();

            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(
                    bitmapHandle,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                imageNativeApi.DeleteObject(bitmapHandle);
            }
        }
    }
}