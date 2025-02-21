/*
 * UI.cs
 * 
 * Description:
 * This file defines UI-related models and structures for a DICOM Viewer application.
 * It includes data models for handling image information, DICOM metadata, and progress tracking.
 * Additionally, it manages root directories for image storage and provides utility functions.
 * 
 * Features:
 * - ImageInfoModel: Stores current DICOM image and windowing parameters (WW/WL)
 * - DicomInfoModel: Stores patient and study metadata with property change notifications
 * - ProgressModel: Tracks loading progress with text and percentage updates
 * - ImageRoot: Represents root folders and file paths for DICOM images
 * - UI (static class): Provides global access to UI-related models and methods
 * 
 * Author: s.harada@HIBMS
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SATOMI.Pages
{
    public static class UI
    {
        public static ProgressModel ProgressView = new ProgressModel();
        public static DicomInfoModel InfoView = new DicomInfoModel();
        public static ImageInfoModel ImageInfo = new ImageInfoModel();
        public static List<string> RootList = new List<string>();

        public static List<ImageRoot> ImageRoots = new List<ImageRoot>();

        public static void ClearRoots()
        {
            ImageRoots.Clear();
            RootList.Clear();
        }
    }
    public class ImageInfoModel : INotifyPropertyChanged
    {
        public ImageInfoModel() { }
        public event PropertyChangedEventHandler? PropertyChanged;

        public Microsoft.Maui.Graphics.IImage? _current_img = null;
        public int current_img_width;
        public int current_img_height;
        public float img_slope;
        public float img_intercept;
        private double _WW = 400.0;
        public double WW
        {
            get => _WW;
            set
            {
                if (_WW != value)
                {
                    _WW = value;
                    OnPropertyChanged(nameof(WW)); 
                }
            }
        }
        private double _WL = 40.0;
        public double WL
        {
            get => _WL;
            set
            {
                if (_WL != value)
                {
                    _WL = value;
                    OnPropertyChanged(nameof(WL)); 
                }
            }
        }
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    public class DicomInfoModel : INotifyPropertyChanged
    {
        public DicomInfoModel()
        {

        }
        public event PropertyChangedEventHandler? PropertyChanged;

        private string? patientInfo;
        public string? PatientInfo
        {
            get { return patientInfo; }
            set
            {
                if (patientInfo != value)
                {
                    patientInfo = value;
                    OnPropertyChanged(nameof(PatientInfo));
                }
            }
        }

        private string? studyInfo;
        public string? StudyInfo
        {
            get { return studyInfo; }
            set
            {
                if (studyInfo != value)
                {
                    studyInfo = value;
                    OnPropertyChanged(nameof(StudyInfo));
                }
            }
        }

        public void Clear()
        {
            PatientInfo = string.Empty;
            StudyInfo = string.Empty;
        }


        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ProgressModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public ProgressModel()
        {
            PFloat = 0.0f;
            PText = "[Image 0/0]";
            PPercent = "0%";
        }

        private string? ptext;
        public string? PText
        {
            get { return ptext; }
            set
            {
                if (ptext != value)
                {
                    ptext = value;
                    OnPropertyChanged(nameof(PText));
                }
            }
        }

        private string? ppercent;
        public string? PPercent
        {
            get { return ppercent; }
            set
            {
                if (ppercent != value)
                {
                    ppercent = value;
                    OnPropertyChanged(nameof(PPercent));
                }
            }
        }

        private float? pfloat;
        public float? PFloat
        {
            get { return pfloat; }
            set
            {
                if (pfloat != value)
                {
                    pfloat = value;
                    OnPropertyChanged(nameof(PFloat));
                }
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ImageRoot
    {
        public readonly string RootFolder;
        public readonly string FullFolderPath;
        public readonly string FilePath; 
        public readonly string DisplayString; 
        public bool IsFolder => FilePath == string.Empty;

        public ImageRoot(string fullFolderPath, string filePath, string displayString)
        {
            FullFolderPath = fullFolderPath;
            FilePath = filePath;
            DisplayString = displayString;
            if (IsFolder)
            {
                RootFolder = Path.GetFileName(FullFolderPath) ?? "Unknown";
            }
            else
            {
                RootFolder = ""; 
            }
        }
    }
}
