/*
 * CanvasDraw.cs
 * 
 * This file defines a custom drawable class for rendering medical images using 16-bit grayscale pixel data.
 * 
 * Features:
 * - `Convert16BitTo8Bit(ushort[], int, int, double, double)`: Converts 16-bit pixel data to 8-bit grayscale.
 * - `Draw(ICanvas, RectF)`: Draws the processed image onto the canvas.
 * - Android-specific functions:
 *   - `ConvertToBitmap(byte[], int, int)`: Converts 8-bit grayscale data to an Android Bitmap.
 *   - `ScaleBitmap(Bitmap, float)`: Scales an Android Bitmap.
 *   - `ConvertBitmapToIImage(Bitmap)`: Converts an Android Bitmap to a MAUI-compatible image.
 * 
 * This implementation ensures efficient image rendering by utilizing parallel processing and platform-specific optimizations.
 *
 * Author: s.harada@HIBMS
 */
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics.Platform;
using Microsoft.Maui.Controls.PlatformConfiguration;
using SkiaSharp;
#if ANDROID
using Android.Graphics;
#endif
namespace SATOMI.Pages
{
    internal class CanvasDraw : IDrawable
    {
        private static byte[] Convert16BitTo8Bit(ushort[] pixelData, int width, int height, float slope, float intercept, double windowWidth, double windowCenter)
        {
            var output = new byte[width * height];
            int min = (int)(windowCenter - (windowWidth / 2));
            int max = (int)(windowCenter + (windowWidth / 2));

            Parallel.For(0, pixelData.Length, i =>
            {
                double realValue = ((short)pixelData[i] * slope) + intercept;
                int value = (int)( (realValue - min) * 255 / (max - min) );
                output[i] = (byte)((value < 0) ? 0 : (value > 255) ? 255 : value);
            });

            return output;
        }
        public void Draw(ICanvas canvas, Microsoft.Maui.Graphics.RectF dirtyRect)
        {
            if (!ViewerPage.CanDraw) return;
            if (UI.ImageInfo._current_img == null)
            {
                ushort[]? data = ViewerPage.GetCurrentSliceOfPixeldata();
                if (data == null)
                    return;
                var image_8bit = Convert16BitTo8Bit(data, UI.ImageInfo.current_img_width, UI.ImageInfo.current_img_height,UI.ImageInfo.img_slope,UI.ImageInfo.img_intercept, UI.ImageInfo.WW, UI.ImageInfo.WL);
#if ANDROID
                var bitmap = ConvertToBitmap(image_8bit,  UI.ImageInfo.current_img_width,  UI.ImageInfo.current_img_height);
                if (bitmap == null)
                {
                    UI.ImageInfo._current_img = null;
                }
                else
                {
                    UI.ImageInfo._current_img = ConvertBitmapToIImage(bitmap);
                }
#endif
            }

            canvas.DrawImage(UI.ImageInfo._current_img, (float)ViewerPage._offsetX, (float)ViewerPage._offsetY, ViewerPage.DesiredWidth, ViewerPage.DesiredHeight);
        }
#if ANDROID
        private static Android.Graphics.Bitmap ScaleBitmap(Android.Graphics.Bitmap bitmap, float scale)
        {
            int scaledWidth = (int)(bitmap.Width * scale);
            int scaledHeight = (int)(bitmap.Height * scale);

            return Android.Graphics.Bitmap.CreateScaledBitmap(bitmap, scaledWidth, scaledHeight, true);
        }
        private static Android.Graphics.Bitmap? ConvertToBitmap(byte[] pixelData, int width, int height)
        {
            var bmp_conf = Android.Graphics.Bitmap.Config.Argb8888;
            if (bmp_conf == null)
            {
                return null;
            }
            var bitmap = Android.Graphics.Bitmap.CreateBitmap(width, height, bmp_conf);
            int[] pixels = new int[width * height];
            Parallel.For(0, pixelData.Length, i =>
            {
                int gray = pixelData[i];
                pixels[i] = Android.Graphics.Color.Argb(255, gray, gray, gray); 
            });

            bitmap.SetPixels(pixels, 0, width, 0, 0, width, height);
            return bitmap;
        }
        private static Microsoft.Maui.Graphics.IImage? ConvertBitmapToIImage(Android.Graphics.Bitmap bitmap)
        {
            using (var stream = new MemoryStream())
            {
                var bmpformat = Android.Graphics.Bitmap.CompressFormat.Png;
                if (bmpformat == null)
                {
                    return null;
                }
                else
                { 
                    bitmap.Compress(bmpformat, 100, stream);
                    stream.Seek(0, SeekOrigin.Begin);
                    return PlatformImage.FromStream(stream);
                }
            }
        }
#endif
        }
}
