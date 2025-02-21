/*
 * Slice.cs
 * 
 * This class represents a single DICOM image slice in a medical imaging study.
 * 
 * Features:
 * - Stores metadata extracted from the DICOM file.
 * - Holds pixel data and image dimensions.
 * - Keeps track of the slice position in a 3D volume.
 * 
 * Properties:
 * - `DicomLoc`: Path to the original DICOM file.
 * - `OriginalDirectory`: Directory containing the DICOM file.
 * - `Info`: DICOM metadata extracted into a `DicomTags` object.
 * - `Number`: Slice index in the series.
 * - `Width`, `Height`: Image dimensions.
 * - `IMG_Patient_Position`: Position of the slice in 3D space.
 * - `Pixeldata`: Raw 16-bit pixel data (optional).
 * 
 * Author: s.harada@HIBMS
 */
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
        public readonly string DicomLoc;
        public readonly string OriginalDirectory; 
        public readonly DicomTags Info;
        public readonly int Width, Height;
        public readonly float[] IMG_Patient_Position;
        public readonly float Slope;
        public readonly float Intercept;
        public ushort[]? Pixeldata { get; } 
        public int Number { get; set; }
        public Slice(
            string originalLoc,
            DicomTags info,
            int number,
            int width,
            int height,
            float slope,
            float intercept,
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
            Slope = slope;
            Intercept = intercept;
            IMG_Patient_Position = img_patient_position;
            Pixeldata = pixeldata;
        }
    }
}