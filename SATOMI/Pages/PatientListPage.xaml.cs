using FellowOakDicom;
using FellowOakDicom.Imaging;
using Microsoft.Maui.Controls.Internals;
using System.Collections.ObjectModel;
using System.Threading;

namespace SATOMI.Pages
{
    public partial class PatientListPage : ContentPage
    {
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
            await LoadDicomFilesAsync();
        }

        protected override void OnNavigatedTo(NavigatedToEventArgs args)
        {
            base.OnNavigatedTo(args);

            // ページがロードされた後にサイズを取得
            this.Loaded += (s, e) =>
            {
                var appHeight = this.Height;

                // NaN でないかチェックしてから設定
                if (!double.IsNaN(appHeight) && appHeight > 0)
                {
                    PatientCollectionView.HeightRequest = appHeight - 110;
                }
            };
        }

        public async Task LoadDicomFilesAsync()
        {
            LoadingIndicator.IsRunning = true;
            LoadingIndicator.IsVisible = true;
            PatientList.Clear();
            try
            {
                foreach (var directory in Directory.GetDirectories(_storagePath))
                {
                    var patientNode = new PatientNode
                    {
                        Images = new ObservableCollection<ImageNode>(),
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

                    await AddDicomFilesFromDirectoryAsync(directory, patientNode);

                    if (patientNode.Images.Count > 0)
                    {
                        PatientList.Add(patientNode);
                    }
                }
                StorageSize = GetFolderSizeGB(_storagePath);
            }
            finally
            {
                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
            }
        }
        private async Task AddDicomFilesFromDirectoryAsync(string directory, PatientNode patientNode)
        {
            // DICOMファイルを現在のディレクトリから非同期で取得
            foreach (var file in Directory.GetFiles(directory, "*.dcm"))
            {
                var dicomFile = DicomFile.Open(file);
                var imageNode = new ImageNode
                {
                    ImageType = dicomFile.Dataset.GetString(DicomTag.Modality),
                    ImagePath = file
                };
                if(patientNode.Images != null)
                {
                    patientNode.Images.Add(imageNode);
                }
            }

            // サブディレクトリも再帰的に非同期で検索
            foreach (var subDirectory in Directory.GetDirectories(directory))
            {
                await AddDicomFilesFromDirectoryAsync(subDirectory, patientNode);
            }
        }
        public static double GetFolderSizeGB(string folderPath)
        {
            // フォルダが存在しない場合
            if (!Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException($"The directory '{folderPath}' does not exist.");
            }

            double totalSize = 0;

            string[] files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                FileInfo fileInfo = new FileInfo(file);
                totalSize += fileInfo.Length;  // ファイルのサイズ（バイト単位）
            }
            return Math.Round(totalSize / (1024.0 * 1024.0 * 1024.0), 3);
        }
        private async void OnPatientButtonClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.BindingContext is PatientNode patientNode)
            {
                bool confirm = await DisplayAlert("Confirmation", $"Do you want to open {patientNode.PatientName}?", "Cancel", "Open");
                if (!confirm)
                {
                    var node_StudyUID = patientNode.StudyUID;
                    if (node_StudyUID != null)
                    {
                        string patientDirectory = Path.Combine(_storagePath, node_StudyUID);
                        if (Directory.Exists(patientDirectory))
                        {
                            if (!patientDirectory.EndsWith("/"))
                            {
                                patientDirectory = string.Concat(patientDirectory, "/");
                            }
                            await Shell.Current.GoToAsync($"//ViewerPage?Location={patientDirectory}");
                        }
                    }
                }
            }
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
                            // .dcmファイルを削除
                            foreach (var file in Directory.GetFiles(patientDirectory, "*.dcm"))
                            {
                                File.Delete(file);
                            }

                            // フォルダが空なら削除
                            if (Directory.GetFiles(patientDirectory).Length == 0 &&
                                Directory.GetDirectories(patientDirectory).Length == 0)
                            {
                                Directory.Delete(patientDirectory);
                            }
                        }
                        StorageSize = GetFolderSizeGB(_storagePath);
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

    public class PatientNode
    {
        public string? PatientID { get; set; } 
        public string? PatientName { get; set; }  
        public ObservableCollection<ImageNode>? Images { get; set; }  
        public bool IsImagesVisible { get; set; } 
        public string? StudyUID { get; set; }
        public string ModalityList => Images?.Any() == true
            ? string.Join(", ", Images.Select(img => img.ImageType).Distinct())
            : string.Empty;  
        public int ImageCount => Images?.Count ?? 0;
    }

    public class ImageNode
    {
        public string? ImageType { get; set; }  // Modality情報
        public string? ImagePath { get; set; }  // 画像ファイルパス
    }
}