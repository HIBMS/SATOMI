using SATOMI.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SATOMI.Pages
{
    public class Slice
    {
        public readonly string DicomLoc; // Original file path
        public readonly string OriginalDirectory; // Original file directory
        public readonly DicomTags Info;
        public readonly int Width, Height;
        public readonly float[] IMG_Patient_Position;
        public ushort[]? Pixeldata { get; } // `null` の可能性があるため `?` を追加
        public int Number { get; set; } // `readonly` を削除

        public Slice(
            string originalLoc,
            DicomTags info,
            int number,
            int width,
            int height,
            float[] img_patient_position,
            ushort[]? pixeldata = null
        )
        {
            DicomLoc = originalLoc;
            OriginalDirectory = Path.GetDirectoryName(originalLoc) ?? string.Empty;
            Info = info;
            Number = number;
            Width = width;
            Height = height;
            IMG_Patient_Position = img_patient_position;
            Pixeldata = pixeldata;
        }
    }
}