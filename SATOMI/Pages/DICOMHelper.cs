﻿using FellowOakDicom.Imaging.Render;
using Microsoft.Maui.Graphics.Platform;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SATOMI.Pages
{
    internal class DICOMHelper
    {
        private static byte[] Convert16BitTo8Bit(ushort[] pixelData, int width, int height, int windowWidth, int windowCenter)
        {
            var output = new byte[width * height];
            int min = windowCenter - (windowWidth / 2);
            int max = windowCenter + (windowWidth / 2);

            Parallel.For(0, pixelData.Length, i =>
            {
                int value = ((short)pixelData[i] - min) * 255 / (max - min);
                output[i] = (byte)((value < 0) ? 0 : (value > 255) ? 255 : value);
            });

            return output;
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
            // Bitmapを作成
            var bmp_conf = Android.Graphics.Bitmap.Config.Argb8888;
            if(bmp_conf == null)
            {
                return null;
            }
            var bitmap = Android.Graphics.Bitmap.CreateBitmap(width, height, bmp_conf);
            //var bitmap = Android.Graphics.Bitmap.CreateBitmap(width, height, Android.Graphics.Bitmap.Config.Rgb565);

            // Bitmapにピクセルデータを書き込み
            int[] pixels = new int[width * height];
            // Parallel.For を使って並列処理
            Parallel.For(0, pixelData.Length, i =>
            {
                int gray = pixelData[i];
                pixels[i] = Android.Graphics.Color.Argb(255, gray, gray, gray); // グレースケール
            });

            bitmap.SetPixels(pixels, 0, width, 0, 0, width, height);
            return bitmap;
        }
        private static ImageSource? ConvertBitmapToImageSource(Android.Graphics.Bitmap bitmap)
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
                    MemoryStream copyStream = new();
                    stream.CopyToAsync(copyStream);
                    copyStream.Position = 0;
                    return ImageSource.FromStream(() => new MemoryStream(copyStream.ToArray())); // <=== Include a new MemoryStream
                }
            }
        }
#endif
        public static ImageSource? ConvertDicomToImageSource(ushort[] dicomPixelData, int width, int height)
        {
            if (dicomPixelData == null || dicomPixelData.Length != width * height)
            {
                throw new ArgumentException("ピクセルデータのサイズが画像サイズに一致しません。");
            }

            byte[]? pixeldata = Convert16BitTo8Bit(dicomPixelData, 512, 512, 400, 40);
            // SkiaSharp を使用して画像を生成
            // 8bitデータをBitmapに変換
#if ANDROID
            var bitmap = ConvertToBitmap(pixeldata, 512, 512);
            float scale = 1.0F;
            if(bitmap==null)
            {
                return null;
            }
            else
            {
                var scaled_bitmap = ScaleBitmap(bitmap, scale);
                return ConvertBitmapToImageSource(scaled_bitmap);
            }
#else
            return null;
#endif
        }
    }
}
