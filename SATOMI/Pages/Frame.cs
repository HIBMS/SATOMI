using SATOMI.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SATOMI.Pages
{
    public class Frame
    {
        public readonly string DicomLoc; //Original file path
        public readonly string SavedFrameLoc; //Cached path
        public readonly string OriginalDirectory = string.Empty; //Original file directory

        public readonly byte[] Buffer;
        public readonly ushort[] Pixeldata;
        public int Number { get; set; } // setアクセサを追加
        public readonly int Width, Height;
        public readonly float[] IMG_Patient_Position;
        public readonly DicomTags Info;

        public Frame(string fileLoc, string originalLoc, DicomTags info, byte[] buffer, int number, int width, int height, float[] img_patient_position)
        {
            SavedFrameLoc = fileLoc;
            DicomLoc = originalLoc;
            //Directory = Path.GetDirectoryName(fileLoc);
            OriginalDirectory = Path.GetDirectoryName(originalLoc) ?? "";
            Info = info;
            Buffer = buffer;
            Number = number;
            Width = width;
            Height = height;
            IMG_Patient_Position = img_patient_position;
        }
        public Frame(string fileLoc, string originalLoc, DicomTags info, ushort[] pixeldata, int number, int width, int height, float[] img_patient_position)
        {
            SavedFrameLoc = fileLoc;
            DicomLoc = originalLoc;
            //Directory = Path.GetDirectoryName(fileLoc);
            OriginalDirectory = Path.GetDirectoryName(originalLoc) ?? "";
            Info = info;
            Pixeldata = pixeldata;
            Number = number;
            Width = width;
            Height = height;
            IMG_Patient_Position = img_patient_position;
        }
    }
}
