using FellowOakDicom;
using FellowOakDicom.Imaging;
using Microsoft.Maui.Controls.Internals;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace SATOMI.Pages
{
    public partial class PatientListPage : ContentPage
    {
        public static bool updated_data = false;
        private bool firstAppearing = true;
        public ObservableCollection<PatientNode> PatientList { get; set; }
        private double _storageSize;
        public double StorageSize
        {
            get => _storageSize;
            set
            {
                if (_storageSize != value)
                {
                    _storageSize = value;
                    OnPropertyChanged();
                }
            }
        }
        private string _storagePath = "";
        public PatientListPage()
        {
            InitializeComponent();
            PatientList = new ObservableCollection<PatientNode>();
            BindingContext = this;
            string dicomStoragePath = Path.Combine(FileSystem.AppDataDirectory, "DICOMStorage");
            if (Directory.Exists(dicomStoragePath))
            {
                foreach (string file in Directory.GetFiles(dicomStoragePath))
                {
                    File.Delete(file);
                }
            }
            else
            {
                Directory.CreateDirectory(dicomStoragePath);
            }
            _storagePath = dicomStoragePath;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            this.Opacity = 0;
            await this.FadeTo(1, 700, Easing.SinIn);
            var appHeight = this.Height;
            if (!double.IsNaN(appHeight) && appHeight > 0 && firstAppearing ==true) 
            {
                PatientCollectionView.HeightRequest = appHeight - 110;
                await Task.Delay(100);
                firstAppearing = false;
            }
            await LoadPatientListAsync();
            if (updated_data)
            {
                await Task.Run(async () =>
                {
                    await LoadDicomFilesAsync();
                    await SavePatientListAsync();
                    updated_data = false;
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        OnPropertyChanged(nameof(PatientList));
                    });
                });
            }
        }
        private static readonly Dictionary<string, string> ModalityShortNames = new Dictionary<string, string>
        {
            { "RTSTRUCT", "RS" },
            { "RTPLAN", "RP" },
            { "RTDOSE", "RD" },
            { "CT", "CT" },
            { "MR", "MR" }
        };
        private async Task SavePatientListAsync()
        {
            string json = JsonSerializer.Serialize(PatientList);
            string path = Path.Combine(FileSystem.AppDataDirectory, "patient_list.json");
            await File.WriteAllTextAsync(path, json);
        }

        private async Task LoadPatientListAsync()
        {
            string filePath = Path.Combine(FileSystem.AppDataDirectory, "patient_list.json");
            if (File.Exists(filePath))
            {
                string json = await File.ReadAllTextAsync(filePath);
                var list = JsonSerializer.Deserialize<ObservableCollection<PatientNode>>(json);
                if (list != null)
                {
                    PatientList = new ObservableCollection<PatientNode>(list);
                    OnPropertyChanged(nameof(PatientList));
                }
            }
            else
            {
                await LoadDicomFilesAsync();
            }
            StorageSize = GetFolderSizeGB(_storagePath);
        }
        private Dictionary<string, DateTime> fileTimestamps = new();
        public async Task LoadDicomFilesAsync()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LoadingIndicator.IsRunning = true;
                LoadingIndicator.IsVisible = true;
                PatientCollectionView.IsVisible = false;
            });
            await Task.Delay(100);
            Dictionary<string, DateTime> newFileTimestamps = new Dictionary<string, DateTime>();
            try
            {
                foreach (var directory in Directory.GetDirectories(_storagePath))
                {
                    var patientNode = new PatientNode
                    {
                        Images = new List<ImageNode>(),
                        IsImagesVisible = false
                    };

                    string? firstDicomFile = Directory.GetFiles(directory, "*.dcm").FirstOrDefault();
                    if (firstDicomFile != null)
                    {
                        var dicomFile = DicomFile.Open(firstDicomFile);
                        patientNode.PatientID = dicomFile.Dataset.GetString(DicomTag.PatientID) ?? "Unknown ID";
                        patientNode.PatientName = dicomFile.Dataset.GetString(DicomTag.PatientName) ?? "Unknown Name";
                        patientNode.StudyUID = dicomFile.Dataset.GetString(DicomTag.StudyInstanceUID) ?? "Unknown StudyUID";
                    }
                    else
                    {
                        patientNode.PatientID = "Unknown ID";
                        patientNode.PatientName = Path.GetFileName(directory);
                        patientNode.StudyUID = "Unknown StudyUID";
                    }
                    int index = PatientList.Select((patient, idx) => new { patient, idx })
                       .FirstOrDefault(x => x.patient.StudyUID == patientNode.StudyUID)?.idx ?? -1;
                    if (index != -1)
                    {
                        await AddDicomFilesFromDirectoryAsync(directory, PatientList[index]);
                    }
                    else
                    {
                        await AddDicomFilesFromDirectoryAsync(directory, patientNode);
                        if (patientNode.Images.Count > 0)
                        {
                            PatientList.Add(patientNode);
                        }
                    }
                }
                StorageSize = GetFolderSizeGB(_storagePath);
            }
            finally
            {
                updated_data = false;
                await SavePatientListAsync();
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    OnPropertyChanged(nameof(PatientList));
                    LoadingIndicator.IsRunning = false;
                    LoadingIndicator.IsVisible = false;
                    PatientCollectionView.IsVisible = true;
                });
                await Task.Delay(100);
            }
        }
        private async Task AddDicomFilesFromDirectoryAsync(string directory, PatientNode patientNode)
        {
            foreach (var file in Directory.GetFiles(directory, "*.dcm"))
            {
                if (patientNode.Images != null)
                {
                    if (patientNode.Images.Any(img => img.ImagePath == file))
                    {
                        continue; 
                    }
                    var dicomFile = DicomFile.Open(file);

                    string? referencedSOPInstanceUID = null;
                    if (dicomFile.Dataset.Contains(DicomTag.ReferencedFrameOfReferenceSequence))
                    {
                        var refFrameSeq = dicomFile.Dataset.GetSequence(DicomTag.ReferencedFrameOfReferenceSequence)?.FirstOrDefault();
                        var refStudySeq = refFrameSeq?.GetSequence(DicomTag.RTReferencedStudySequence)?.FirstOrDefault();
                        var refSeriesSeq = refStudySeq?.GetSequence(DicomTag.RTReferencedSeriesSequence)?.FirstOrDefault();
                        referencedSOPInstanceUID = refSeriesSeq?.GetString(DicomTag.SeriesInstanceUID);
                    }
                    var imageTypeShort = ModalityShortNames.TryGetValue(dicomFile.Dataset.GetString(DicomTag.Modality), out var shortName) ? shortName : dicomFile.Dataset.GetString(DicomTag.Modality);
                    var imageNode = new ImageNode
                    {
                        ImageType = imageTypeShort,
                        SeriesInstanceUID = dicomFile.Dataset.GetString(DicomTag.SeriesInstanceUID),
                        ReferenceSeriesInstanceUID = referencedSOPInstanceUID,
                        ImagePath = file
                    };

                    patientNode.Images.Add(imageNode);
                }
            }
            foreach (var subDirectory in Directory.GetDirectories(directory))
            {
                await AddDicomFilesFromDirectoryAsync(subDirectory, patientNode);
            }
            OnPropertyChanged(nameof(patientNode.ImageCount));
        }
        public static double GetFolderSizeGB(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException($"The directory '{folderPath}' does not exist.");
            }

            double totalSize = 0;

            string[] files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                FileInfo fileInfo = new FileInfo(file);
                totalSize += fileInfo.Length; 
            }
            return Math.Round(totalSize / (1024.0 * 1024.0 * 1024.0), 3);
        }
        private async void OnPatientButtonClicked(object sender, EventArgs e)
        {
            
            if (sender is Button button && button.BindingContext is PatientNode patientNode)
            {
                var seriesList = patientNode.ImageSeriesSummary;
                if (seriesList.Count == 1)
                {
                    var selectedStudy = seriesList.First(); 
                    ProcessSelectedStudy(selectedStudy);
                }
                else
                {
                    var options = seriesList.Select(s => s.ToString()).ToArray();

                    string selected_option = await DisplayActionSheet("ActionSheet: Open ?", "Cancel", null, options);
                    if (selected_option != "Cancel" && !string.IsNullOrEmpty(selected_option))
                    {
                        var selectedStudy = seriesList.FirstOrDefault(s => s.ToString() == selected_option);
                        if (selectedStudy != null)
                        {
                            ProcessSelectedStudy(selectedStudy); 
                        }
                    }
                }
            }
        }
        private async void ProcessSelectedStudy(SeriesSummary selectedStudy)
        {
            string application_path = FileSystem.AppDataDirectory;
            string work_space = Path.Combine(application_path, "WORKSPACE");

            if (Directory.Exists(work_space))
            {
                Directory.Delete(work_space, true); 
            }

            Directory.CreateDirectory(work_space); 

            foreach (var imagePath in selectedStudy.ImagePaths)
            {
                if (File.Exists(imagePath))
                {
                    var destinationPath = Path.Combine(work_space, Path.GetFileName(imagePath));
                    File.Copy(imagePath, destinationPath, overwrite: true); 
                }
            }

            foreach (var referencedSeries in selectedStudy.ReferencedImages)
            {
                foreach (var imagePath in referencedSeries.ImagePaths)
                {
                    if (File.Exists(imagePath))
                    {
                        var destinationPath = Path.Combine(work_space, Path.GetFileName(imagePath));
                        File.Copy(imagePath, destinationPath, overwrite: true); 
                    }
                }
            }
            if (!work_space.EndsWith("/"))
            {
                work_space = string.Concat(work_space, "/");
            }
            await Shell.Current.GoToAsync($"//ViewerPage?Location={work_space}");
        }
        private async void OnDeletePatientClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.BindingContext is PatientNode patientNode)
            {
                bool confirm = await DisplayAlert("Confirmation", $"Are you sure you want to delete {patientNode.PatientName}?", "Delete", "Cancel");
                if (confirm)
                {
                    PatientList.Remove(patientNode);
                    var node_StudyUID = patientNode.StudyUID;
                    if (node_StudyUID != null)
                    {
                        string patientDirectory = Path.Combine(_storagePath, node_StudyUID);
                        if (Directory.Exists(patientDirectory))
                        {
                            foreach (var file in Directory.GetFiles(patientDirectory, "*.dcm"))
                            {
                                File.Delete(file);
                            }
                            if (Directory.GetFiles(patientDirectory).Length == 0 &&
                                Directory.GetDirectories(patientDirectory).Length == 0)
                            {
                                Directory.Delete(patientDirectory);
                            }
                        }
                        StorageSize = GetFolderSizeGB(_storagePath);
                        await SavePatientListAsync();
                    }                    
                }
            }
        }
        private async void Navigation_Clicked(object sender, EventArgs e)
        {
            var page = new NavigationPage();
            page.Opacity = 0;
            var currentWindow = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
            if (currentWindow?.Page?.Navigation != null)
            {
                await currentWindow.Page.Navigation.PushModalAsync(page);
                await page.FadeTo(1, 700);
            }
        }
    }

    public class PatientNode : INotifyPropertyChanged
    {
        public string? PatientID { get; set; }
        public string? PatientName { get; set; }
        public List<ImageNode> Images { get; set; } = new();
        public bool IsImagesVisible { get; set; }
        public string? StudyUID { get; set; }

        public string ModalityList => Images.Any()
            ? string.Join(", ", Images.Select(img => img.ImageType).Distinct())
            : string.Empty;

        public List<SeriesSummary> ImageSeriesSummary { get; private set; } = new();

        private string _imageCount = "No images";
        public string ImageCount
        {
            get
            {
                UpdateImageSeriesSummary();
                var newCount = string.Join("\n", ImageSeriesSummary.Select(s => s.ToString()));
                if (_imageCount != newCount)
                {
                    _imageCount = newCount;
                    OnPropertyChanged(nameof(ImageCount));
                }
                return _imageCount;
            }
        }

        private void UpdateImageSeriesSummary()
        {
            if (Images == null || !Images.Any())
            {
                ImageSeriesSummary.Clear();
                return;
            }

            var imagesWithReferencedSeries = Images
                .Where(img => !string.IsNullOrEmpty(img.ReferenceSeriesInstanceUID))
                .GroupBy(img => img.SeriesInstanceUID)
                .Select(group => new
                {
                    SeriesInstanceUID = group.Key,
                    Images = group.ToList(),
                    ImageTypes = group.Select(img => img.ImageType).Distinct().ToList(),
                    ImagePaths = group.Select(img => img.ImagePath).Distinct().ToList()
                })
                .ToList();

            var imagesWithoutReferencedSeries = Images
                .Where(img => string.IsNullOrEmpty(img.ReferenceSeriesInstanceUID))
                .GroupBy(img => img.SeriesInstanceUID)
                .Select(group => new
                {
                    SeriesInstanceUID = group.Key,
                    Images = group.ToList(),
                    ImageTypes = group.Select(img => img.ImageType).Distinct().ToList(),
                    ImagePaths = group.Select(img => img.ImagePath).Distinct().ToList()
                })
                .ToList();

            ImageSeriesSummary.Clear();

            foreach (var group in imagesWithoutReferencedSeries)
            {
                var referencedImages = imagesWithReferencedSeries
                    .Where(referencedGroup => referencedGroup.Images
                        .Any(img => img.ReferenceSeriesInstanceUID == group.SeriesInstanceUID))
                    .Select(referencedGroup => new ReferencedSeries
                    {
                        SeriesInstanceUID = referencedGroup.SeriesInstanceUID,
                        ImageTypes = referencedGroup.ImageTypes.Select(x => x ?? "").ToList(),
                        ImagePaths = referencedGroup.ImagePaths,
                        ImageCount = referencedGroup.Images.Count
                    })
                    .ToList();

                ImageSeriesSummary.Add(new SeriesSummary
                {
                    ImageType = group.ImageTypes.FirstOrDefault() ?? "Unknown",
                    ImageCount = group.Images.Count,
                    ImagePaths = group.ImagePaths,
                    ReferencedImages = referencedImages
                });
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ImageNode
    {
        public string? ImageType { get; set; }
        public string? SeriesInstanceUID { get; set; }
        public string? ReferenceSeriesInstanceUID { get; set; }
        public string? ImagePath { get; set; }
    }

    public class SeriesSummary
    {
        public string ImageType { get; set; } = "Unknown";
        public int ImageCount { get; set; }
        public List<string?> ImagePaths { get; set; } = new();
        public List<ReferencedSeries> ReferencedImages { get; set; } = new();
        public override string ToString()
        {
            var referencedInfo = ReferencedImages.Any()
                ? string.Join(" ", ReferencedImages.Select(r => $"{string.Join(" ", r.ImageTypes)} {r.ImageCount}"))
                : string.Empty;

            return $"{ImageType}: {ImageCount} {referencedInfo}".Trim();
        }
    }

    public class ReferencedSeries
    {
        public List<string?> ImagePaths { get; set; } = new();
        public string? SeriesInstanceUID { get; set; }
        public List<string> ImageTypes { get; set; } = new();
        public int ImageCount { get; set; }
    }
}