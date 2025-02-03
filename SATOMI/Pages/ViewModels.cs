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
        private double _WW = 400.0;
        public double WW
        {
            get => _WW;
            set
            {
                if (_WW != value)
                {
                    _WW = value;
                    OnPropertyChanged(nameof(WW));  // プロパティが変更されたことを通知
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
                    OnPropertyChanged(nameof(WL));  // プロパティが変更されたことを通知
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
        public readonly string RootFolder; // Folder
        public readonly string FullFolderPath; // /storage/0/emulated/Folder
        public readonly string FilePath; // /storage/0/emulated/Folder/dicom.dcm
        public readonly string DisplayString; // what shows in picker
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
                // 例として"File"としていますが、適切な処理を行ってください
                RootFolder = "File"; 
            }
        }
    }
}
