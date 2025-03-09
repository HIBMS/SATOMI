/*
 * DICOMData.cs
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
    public class RTST
    {
        public readonly string DicomLoc;
        public readonly List<Contor> Contours;
        public readonly string Ref_FrameUID;
        public RTST(string originalLoc, List<Contor> contours, string ref_frameUID)
        {
            DicomLoc = originalLoc;
            Contours = contours;
            Ref_FrameUID = ref_frameUID;
        }
    }
    public class Contor
    {
        public readonly string Name;
        public readonly string Color;
        public readonly List<List<Point>> ContourData;
        public readonly List<string> Ref_ImageUID;
        public readonly double? ObservationValue;
        public Contor(string name, string color, List<List<Point>> contourData, List<string> ref_imageUID, double? observationValue)
        {
            Name = name;
            Color = color;
            ContourData = contourData;
            Ref_ImageUID = ref_imageUID;
            ObservationValue = observationValue;
        }
    }
    public class Point
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;
        public Point(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
    public enum SliceOrientation
    {
        Axial,
        Sagittal,
        Coronal,
        Unknown
    }
    public class Slice
    {
        public readonly string DicomLoc;
        public readonly string OriginalDirectory; 
        public readonly DicomTags Info;
        public readonly int Width, Height;
        public readonly string PatientPosition;
        public readonly float[] IMG_Patient_Position;
        public readonly float Slope;
        public readonly float Intercept;
        public readonly float[] IMG_Orientation_Patient;
        public readonly double WindowWidth;
        public readonly double WindowCenter;
        public readonly float PixelSpacingX;
        public readonly float PixelSpacingY;
        public readonly float SliceThickness;
        public readonly SliceOrientation SliceOrientation;
        public ushort[] Pixeldata { get; }
        public int Number { get; set; }
        public Slice(Slice other)
        {
            DicomLoc = other.DicomLoc;
            OriginalDirectory = other.OriginalDirectory;
            Info = other.Info; 
            Number = other.Number;
            Width = other.Width;
            Height = other.Height;
            Slope = other.Slope;
            Intercept = other.Intercept;
            PatientPosition = other.PatientPosition;

            IMG_Patient_Position = (float[])other.IMG_Patient_Position.Clone();
            IMG_Orientation_Patient = (float[])other.IMG_Orientation_Patient.Clone();

            SliceOrientation = other.SliceOrientation;
            WindowWidth = other.WindowWidth;
            WindowCenter = other.WindowCenter;
            PixelSpacingX = other.PixelSpacingX;
            PixelSpacingY = other.PixelSpacingY;
            SliceThickness = other.SliceThickness;
            Pixeldata = (ushort[])other.Pixeldata.Clone();
        }
        public Slice(
            string originalLoc,
            DicomTags info,
            int number,
            int width,
            int height,
            float slope,
            float intercept,
            string patientpositon,
            float[] img_patient_position,
            float[] img_orientation_patient,
            SliceOrientation sliceorientation,
            double windowwidth,
            double windowcenter,
            float pixelspacingx,
            float pixelspacingy,
            float slicethickness,
            ushort[] pixeldata
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
            PatientPosition = patientpositon;
            IMG_Patient_Position = img_patient_position;
            IMG_Orientation_Patient = img_orientation_patient;
            SliceOrientation = sliceorientation;
            WindowWidth = windowwidth;
            WindowCenter = windowcenter;
            PixelSpacingX = pixelspacingx;
            PixelSpacingY = pixelspacingy;
            SliceThickness = slicethickness;
            Pixeldata = pixeldata;
        }
    }
}