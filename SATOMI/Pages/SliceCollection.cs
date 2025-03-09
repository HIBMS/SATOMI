/*
 * SliceCollection.cs
 * 
 * This class manages a collection of DICOM image slices.
 * It handles reading DICOM files, extracting metadata, and organizing slices for display.
 * 
 * Features:
 * - Reads DICOM images using FellowOakDicom.
 * - Extracts metadata such as patient info, study details, and window width/level.
 * - Handles permission requests on Android for file access.
 * - Supports loading single or multiple DICOM files in parallel.
 * - Orders slices based on the Z-axis (ImagePositionPatient).
 * - Tracks and updates loading progress.
 * 
 * Properties:
 * - `Slices`: List of `Slice` objects representing image slices.
 * - `WW`, `WL`: Window width and level for image contrast adjustment.
 * - `_total`, `_current`: Tracking variables for progress updates.
 * 
 * Methods:
 * - `FromFile(string fileLoc)`: Loads a single DICOM file.
 * - `FromFiles(List<string> files)`: Loads multiple DICOM files asynchronously.
 * - `CheckAndRequestStoragePermissionAsync()`: Ensures necessary file permissions on Android.
 * - `_fromFile(string fileLoc, bool onlyFile)`: Internal method to process a DICOM file.
 * - `_infoModelUpdate(DicomTags tags)`: Updates UI with patient and study details.
 * - `_progessModelUpdate(int val, int total)`: Updates UI progress indicators.
 * 
 * Author: s.harada@HIBMS
 */
using FellowOakDicom.Imaging;
using FellowOakDicom;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using Microsoft.Maui.Controls.PlatformConfiguration;
using SixLabors.ImageSharp.Formats.Jpeg;
using System.IO;
using SkiaSharp;
using FellowOakDicom.IO.Buffer;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;

namespace SATOMI.Pages
{
    public class SliceCollection
    {
        private List<Slice> Raw = new List<Slice>();
        public List<RTST> RTContour = new List<RTST>();
        public List<Slice> View = new List<Slice>();
        private int _total = 0;
        private int _current = 0;
        public SliceCollection()
        {
            Raw = new List<Slice>();
            RTContour = new List<RTST>();
            View = new List<Slice>();
        }
        public void Clear()
        {
            Raw.Clear();
            RTContour.Clear();
            View.Clear();
        }
        public (double averageWindowWidth, double averageWindowCenter) CalculateAverageWindowValues()
        {
            if (View.Count == 0)
            {
                return (0, 0);
            }
            double totalWindowWidth = 0;
            double totalWindowCenter = 0;
            foreach (var slice in View)
            {
                totalWindowWidth += slice.WindowWidth;
                totalWindowCenter += slice.WindowCenter;
            }

            double averageWindowWidth = totalWindowWidth / View.Count;
            double averageWindowCenter = totalWindowCenter / View.Count;

            averageWindowWidth = Math.Round(averageWindowWidth, 3);
            averageWindowCenter = Math.Round(averageWindowCenter, 3);

            return (averageWindowWidth, averageWindowCenter);
        }
        private async Task<bool> CheckAndRequestStoragePermissionAsync()
        {
            if (DeviceInfo.Current.Platform == DevicePlatform.Android)
            {
                var status_write = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
                var status_read = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
                if (status_write != PermissionStatus.Granted)
                {
                    status_write = await Permissions.RequestAsync<Permissions.StorageWrite>();
                }
                if (status_read != PermissionStatus.Granted)
                {
                    status_read = await Permissions.RequestAsync<Permissions.StorageWrite>();
                }

                return status_write == PermissionStatus.Granted && status_read == PermissionStatus.Granted;
            }
            return true;
        }
        public async Task FromFile(string fileLoc)
        {
            _progessModelReset();
            _infoModelUpdated = false;
            UI.ClearRoots();
            DirectoryInfo? parentDirInfo = Directory.GetParent(fileLoc);
            bool isPermissionGranted = await CheckAndRequestStoragePermissionAsync();
            if (!isPermissionGranted)
            {
                return;
            }
            _actualFrameNo = 0;
            _fromFile(fileLoc, true);
        }


        private void _progessModelReset()
        {
            UI.ProgressView.PFloat = 0.0f;
            UI.ProgressView.PText = "";
            UI.ProgressView.PPercent = "0%";
        }

        private void _infoModelUpdate(DicomTags tags)
        {
            UI.InfoView.PatientInfo = tags.PatientDetails;
            UI.InfoView.StudyInfo = tags.StudyDetails;
        }

        private void _progessModelUpdate(int val, int total)
        {
            float prog = ((float)val * 1.0f) / (float)total;

            int percent = (int)Math.Floor(prog * 100);
            UI.ProgressView.PPercent = $"{percent.ToString("0")}%";
            UI.ProgressView.PText = $"Image: {val}/{total}";
            UI.ProgressView.PFloat = prog;
        }
        void UpdateProgressSafe()
        {
            int current = Interlocked.Increment(ref _current);
            if (current <= _total)
            {
                _progessModelUpdate(current, _total);
            }
        }
        private bool _infoModelUpdated = false;
        private int _actualFrameNo = 0;
        private void _fromFile(string fileLoc, bool onlyFile = true)
        {
            try
            {
                DicomFile file = DicomFile.Open(fileLoc);
                DicomDataset dataset = file.Dataset;
                string modality = dataset.GetSingleValueOrDefault(DicomTag.Modality, string.Empty);

                switch (modality)
                {
                    case "CT":
                        ParseCT(dataset, fileLoc, onlyFile);
                        break;
                    case "MR":
                        ParseMR(dataset, fileLoc, onlyFile);
                        break;
                    case "RTSTRUCT":
                        ParseRTStruct(dataset, fileLoc, onlyFile);
                        break;
                    default:
                        Console.WriteLine($"Unsupported modality: {modality}");
                        break;
                }
            }
            catch (FellowOakDicom.Imaging.DicomImagingException)
            {
            }
            catch (PermissionException ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        private void ParseCT(DicomDataset dataset, string fileLoc, bool onlyFile)
        {
            DicomImage _imgs = new DicomImage(fileLoc);
            DicomTags tags = new DicomTags(dataset);
            float[] imgPositionPatient = dataset.GetValues<float>(DicomTag.ImagePositionPatient).ToArray();
            float[] imgOrientationPatient = dataset.GetValues<float>(DicomTag.ImageOrientationPatient).ToArray();
            float rescaleSlope = dataset.GetSingleValueOrDefault(DicomTag.RescaleSlope, 1f);
            float rescaleIntercept = dataset.GetSingleValueOrDefault(DicomTag.RescaleIntercept, 0f);
            string patientPosition = dataset.GetSingleValueOrDefault(DicomTag.PatientPosition, "Unknown");
            float pixelSpacingX = dataset.GetValues<float>(DicomTag.PixelSpacing).ToArray()[0];
            float pixelSpacingY = dataset.GetValues<float>(DicomTag.PixelSpacing).ToArray()[1];
            float sliceThickness = dataset.GetSingleValueOrDefault(DicomTag.SliceThickness, -1.0f);
            SliceOrientation sliceOrientation = DetermineSliceOrientation(imgOrientationPatient, patientPosition);
            if (onlyFile)
                _total = _imgs.NumberOfFrames;

            if (!_infoModelUpdated)
            {
                _infoModelUpdate(tags);
                _infoModelUpdated = true;
            }

            var pixData = DicomPixelData.Create(dataset, false);
            for (int i = 0; i < _imgs.NumberOfFrames; i++)
            {
                var byteBuffer = pixData.GetFrame(i);
                var ushortArray = new ushort[byteBuffer.Size / 2];
                Buffer.BlockCopy(byteBuffer.Data, 0, ushortArray, 0, byteBuffer.Data.Length);

                lock (Raw)
                {
                    Raw.Add(new Slice(
                        fileLoc,
                        tags,
                        Interlocked.Increment(ref _actualFrameNo),
                        _imgs.Width,
                        _imgs.Height,
                        rescaleSlope,
                        rescaleIntercept,
                        patientPosition,
                        imgPositionPatient,
                        imgOrientationPatient,
                        sliceOrientation,
                        tags.WW,
                        tags.WL,
                        pixelSpacingX,
                        pixelSpacingY,
                        sliceThickness,
                        ushortArray
                    ));
                }
            }
            UpdateProgressSafe();
        }
        private void ParseMR(DicomDataset dataset, string fileLoc, bool onlyFile)
        {
            DicomImage _imgs = new DicomImage(fileLoc);
            DicomTags tags = new DicomTags(dataset);
            float[] imgPositionPatient = dataset.GetValues<float>(DicomTag.ImagePositionPatient).ToArray();
            float[] imgOrientationPatient = dataset.GetValues<float>(DicomTag.ImageOrientationPatient).ToArray();
            float rescaleSlope = dataset.GetSingleValueOrDefault(DicomTag.RescaleSlope, 1f);
            float rescaleIntercept = dataset.GetSingleValueOrDefault(DicomTag.RescaleIntercept, 0f);
            string patientPosition = dataset.GetSingleValueOrDefault(DicomTag.PatientPosition, "Unknown");
            float pixelSpacingX = dataset.GetValues<float>(DicomTag.PixelSpacing).ToArray()[0];
            float pixelSpacingY = dataset.GetValues<float>(DicomTag.PixelSpacing).ToArray()[1];
            float sliceThickness = dataset.GetSingleValueOrDefault(DicomTag.SliceThickness, -1.0f);
            SliceOrientation sliceOrientation = DetermineSliceOrientation(imgOrientationPatient, patientPosition);
            if (onlyFile)
                _total = _imgs.NumberOfFrames;

            if (!_infoModelUpdated)
            {
                _infoModelUpdate(tags);
                _infoModelUpdated = true;
            }

            var pixData = DicomPixelData.Create(dataset, false);
            for (int i = 0; i < _imgs.NumberOfFrames; i++)
            {
                var byteBuffer = pixData.GetFrame(i);
                var ushortArray = new ushort[byteBuffer.Size / 2];
                Buffer.BlockCopy(byteBuffer.Data, 0, ushortArray, 0, byteBuffer.Data.Length);

                lock (Raw)
                {
                    Raw.Add(new Slice(
                        fileLoc,
                        tags,
                        Interlocked.Increment(ref _actualFrameNo),
                        _imgs.Width,
                        _imgs.Height,
                        rescaleSlope,
                        rescaleIntercept,
                        patientPosition,
                        imgPositionPatient,
                        imgOrientationPatient,
                        sliceOrientation,
                        tags.WW,
                        tags.WL,
                        pixelSpacingX,
                        pixelSpacingY,
                        sliceThickness,
                        ushortArray
                    ));
                }
            }
            UpdateProgressSafe();
        }
        private void ParseRTStruct(DicomDataset dataset, string fileLoc, bool onlyFile)
        {
            var dicomFile = DicomFile.Open(fileLoc);
            string refFrameUID = dataset.GetSingleValueOrDefault(DicomTag.FrameOfReferenceUID, "");
            var roiContours = new List<Contor>();

            var roiSequence = dataset.GetSequence(DicomTag.StructureSetROISequence);
            var roiContourSequence = dataset.GetSequence(DicomTag.ROIContourSequence);

            if (roiSequence != null && roiContourSequence != null)
            {
                foreach (var roiItem in roiSequence)
                {
                    string roiName = roiItem.GetSingleValueOrDefault(DicomTag.ROIName, "Unknown");
                    string color = "#FFFFFF"; 
                    var refImageUIDs = new List<string>();
                    double? observationValue = null;
                    var contours = new List<List<Point>>();

                    foreach (var roiContourItem in roiContourSequence)
                    {
                        int contourROINumber = roiContourItem.GetSingleValueOrDefault(DicomTag.ReferencedROINumber, 0);
                        if (roiItem.GetSingleValueOrDefault(DicomTag.ROINumber, 0) == contourROINumber)
                        {
                            var contourSequence = roiContourItem.GetSequence(DicomTag.ContourSequence);
                            if (contourSequence != null)
                            {
                                foreach (var contourItem in contourSequence)
                                {
                                    var data = contourItem.GetValues<float>(DicomTag.ContourData);
                                    var points = new List<Point>();
                                    for (int i = 0; i < data.Length; i += 3)
                                    {
                                        points.Add(new Point(data[i], data[i + 1], data[i + 2]));
                                    }
                                    contours.Add(points);
                                }
                            }
                        }
                    }
                    roiContours.Add(new Contor(roiName, color, contours, refImageUIDs, observationValue));
                }
            }
            lock (RTContour)
            {
                RTContour.Add(new RTST(fileLoc, roiContours, refFrameUID));
            }
            UpdateProgressSafe();
        }

        public async Task FromFiles(List<string> files)
        {
            _progessModelReset();
            _total = files.Count;
            _infoModelUpdated = false;
            UI.ClearRoots();
            HashSet<string> parentDirs = new HashSet<string>();
            foreach (string file in files)
            {
                string? parentFolderPath = Directory.GetParent(file)?.ToString();
                if (parentFolderPath != null)
                { parentDirs.Add(parentFolderPath); }
            }

            string prevDir = string.Empty;
            List<Task> tasks = new List<Task>();
            for (int i = 0; i < files.Count; i++)
            {
                string file = files[i];
                string dir = Path.GetDirectoryName(file) ?? "";
                if (dir != prevDir)
                {
                    _actualFrameNo = 0;
                    prevDir = dir;
                }
                tasks.Add(Task.Run(() => _fromFile(file, false)));
            }
            await Task.WhenAll(tasks);
            Raw = Raw
                .OrderBy(f => f.IMG_Patient_Position.Length > 2 ? f.IMG_Patient_Position[2] : 0)
                .Select((frame, index) =>
                {
                    frame.Number = index;
                    return frame;
                }).ToList();
            foreach (var slice in Raw)
            {
                View.Add(new Slice(slice));
            }
        }
        private SliceOrientation _currentOrientation = SliceOrientation.Axial;

        public bool MatchesOrientation(SliceOrientation orientation) => _currentOrientation == orientation;

        public void ChangeOrientation(SliceOrientation newOrientation)
        {
            _currentOrientation = newOrientation;
            View.Clear();
            View = GetReorderedSlices(newOrientation);
        }

        public static SliceOrientation DetermineSliceOrientation(float[] imgOrientationPatient, string patientPosition)
        {
            if (imgOrientationPatient.Length < 6)
            {
                throw new ArgumentException("IMG_Orientation_Patient must contain 6 elements.");
            }

            float rowX = imgOrientationPatient[0];
            float rowY = imgOrientationPatient[1];
            float rowZ = imgOrientationPatient[2];

            float colX = imgOrientationPatient[3];
            float colY = imgOrientationPatient[4];
            float colZ = imgOrientationPatient[5];

            bool isHeadFirst = patientPosition.StartsWith("H");
            bool isFeetFirst = patientPosition.StartsWith("F");
            bool isSupine = patientPosition.Contains("S");
            bool isProne = patientPosition.Contains("P");

            float absRowX = Math.Abs(rowX), absRowY = Math.Abs(rowY), absRowZ = Math.Abs(rowZ);
            float absColX = Math.Abs(colX), absColY = Math.Abs(colY), absColZ = Math.Abs(colZ);

            if (absRowZ < 0.1 && absColZ < 0.1)
            {
                return SliceOrientation.Axial;
            }

            if (absRowX < 0.1 && absColX < 0.1)
            {
                return SliceOrientation.Sagittal;
            }

            if (absRowY < 0.1 && absColY < 0.1)
            {
                return SliceOrientation.Coronal;
            }

            throw new InvalidOperationException("Unknown orientation: Check ImageOrientationPatient and PatientPosition.");
        }
        private List<Slice> GetReorderedSlices(SliceOrientation targetOrientation)
        {
            if (Raw.Count == 0) return new List<Slice>();

            SliceOrientation originalOrientation = Raw[0].SliceOrientation;
            int depth = Raw.Count;
            int width = Raw[0].Width;
            int height = Raw[0].Height;

            List<Slice> reorderedSlices = new List<Slice>();
            if (originalOrientation == targetOrientation)
            {
                foreach (var slice in Raw)
                {
                    reorderedSlices.Add(new Slice(slice));
                }
            }
            if (originalOrientation == SliceOrientation.Axial)
            {
                if (targetOrientation == SliceOrientation.Sagittal)
                {
                    for (int x = 0; x < width; x++)
                    {
                        ushort[] newPixelData = new ushort[height * depth];
                        for (int y = 0; y < height; y++)
                        {
                            for (int z = 0; z < depth; z++)
                            {
                                newPixelData[(depth - 1 - z) * width + y] = Raw[z].Pixeldata[y * width + x];
                            }
                        }
                        float[] updatedPatientPosition = UpdatePatientPosition(originalOrientation, targetOrientation, 0);
                        reorderedSlices.Add(CreateSlice(x, height, depth, updatedPatientPosition, Raw[0].IMG_Orientation_Patient, Raw[0].PixelSpacingY, Raw[0].SliceThickness, Raw[0].PixelSpacingX, targetOrientation, newPixelData));
                    }
                }
                else if (targetOrientation == SliceOrientation.Coronal)
                {
                    for (int y = 0; y < height; y++)
                    {
                        ushort[] newPixelData = new ushort[width * depth];
                        for (int x = 0; x < width; x++)
                        {
                            for (int z = 0; z < depth; z++)
                            {
                                newPixelData[(depth - 1 - z) * width + x] = Raw[z].Pixeldata[y * width + x];
                            }
                        }
                        float[] updatedPatientPosition = UpdatePatientPosition(originalOrientation, targetOrientation, 0);

                        reorderedSlices.Add(CreateSlice(y, width, depth, updatedPatientPosition, Raw[0].IMG_Orientation_Patient, Raw[0].PixelSpacingX, Raw[0].SliceThickness, Raw[0].PixelSpacingY, targetOrientation, newPixelData));
                    }
                }
            }
            else if (originalOrientation == SliceOrientation.Sagittal)
            {
            }
            else if (originalOrientation == SliceOrientation.Coronal)
            {
            }
            return reorderedSlices;
        }
        private float[] UpdatePatientPosition(SliceOrientation originalOrientation, SliceOrientation targetOrientation, float depth)
        {
            float[] updatedPatientPosition = new float[3];

            if (originalOrientation == SliceOrientation.Axial)
            {
                if (targetOrientation == SliceOrientation.Sagittal)
                {
                    updatedPatientPosition[0] = -1 * Raw[0].IMG_Patient_Position[1];
                    updatedPatientPosition[1] = Raw[0].IMG_Patient_Position[0];
                    updatedPatientPosition[2] = depth;
                }
                else if (targetOrientation == SliceOrientation.Coronal)
                {
                    updatedPatientPosition[0] = Raw[0].IMG_Patient_Position[0];
                    updatedPatientPosition[1] = Raw[0].IMG_Patient_Position[1];
                    updatedPatientPosition[2] = depth;
                }
            }
            else if (originalOrientation == SliceOrientation.Sagittal)
            {
                if (targetOrientation == SliceOrientation.Axial)
                {
                    updatedPatientPosition[0] = Raw[0].IMG_Patient_Position[1];
                    updatedPatientPosition[1] = Raw[0].IMG_Patient_Position[0];
                    updatedPatientPosition[2] = depth;
                }
                else if (targetOrientation == SliceOrientation.Coronal)
                {
                    updatedPatientPosition[0] = Raw[0].IMG_Patient_Position[0];
                    updatedPatientPosition[1] = Raw[0].IMG_Patient_Position[2];
                    updatedPatientPosition[2] = depth;
                }
            }

            return updatedPatientPosition;
        }
        private Slice CreateSlice(int index, int width, int height, float[] img_patientposition, float[] img_orientation_patient, float pixelspacingx, float pixelspacingy, float slicethickness, SliceOrientation sliceOrientation, ushort[] pixelData)
        {
            return new Slice(
                "",
                Raw[0].Info,
                index,
                width,
                height,
                Raw[0].Slope,
                Raw[0].Intercept,
                Raw[0].PatientPosition,
                img_patientposition,
                img_orientation_patient,
                sliceOrientation,
                Raw[0].WindowWidth,
                Raw[0].WindowCenter,
                pixelspacingx,
                pixelspacingy,
                slicethickness,
                pixelData
            );
        }
    }
}