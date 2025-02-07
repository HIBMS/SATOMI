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
        public List<Slice> Slices = new List<Slice>();

        // Progress counters
        private int _total = 0;
        private int _current = 0;
        public double WW = 0;
        public double WL = 0;
        public SliceCollection()
        {
            Slices = new List<Slice>();
        }

        // 権限確認とリクエスト
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

            // Android以外のプラットフォームでは許可不要と仮定
            return true;
        }
        // With Check Permission
        public async Task FromFile(string fileLoc)
        {
            _progessModelReset();
            _infoModelUpdated = false;

            // Roots View Model
            UI.ClearRoots();
            DirectoryInfo? parentDirInfo = Directory.GetParent(fileLoc);
            if (parentDirInfo != null)
            {
                ImageRoot root = new ImageRoot(parentDirInfo.ToString(), fileLoc, Path.GetFileName(fileLoc));
                UI.ImageRoots.Add(root);
            }
            else
            {
                // fileLoc がルートディレクトリの場合の処理
                
            }
            // 権限確認
            bool isPermissionGranted = await CheckAndRequestStoragePermissionAsync();
            if (!isPermissionGranted)
            {
                //await MainThread.InvokeOnMainThreadAsync(static async () =>
                //{
                //    await Application.Current.MainPage.DisplayAlert("Permission error", "Storage management permission denied. Cancels processing.", "OK");
                //});
                return;
            }
            _actualFrameNo = 0;
            _fromFile(fileLoc, true);
            foreach (var _root in UI.ImageRoots)
                UI.RootList.Add(_root.DisplayString);
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
            if (current <= _total) // 進捗が超えないように制御
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
                DicomImage _imgs = new DicomImage(fileLoc);
                DicomFile file = DicomFile.Open(fileLoc);
                DicomDataset dataset = file.Dataset;  // DicomDatasetを取得
                DicomTags tags = new DicomTags(dataset);  // DicomTagsクラスにDatasetを渡す
                float[] imgPositionPatient = dataset.GetValues<float>(DicomTag.ImagePositionPatient).ToArray();
                WW = tags.WW;
                WL = tags.WL;

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
                    lock (Slices)
                    {
                        Slices.Add(new Slice(
                            fileLoc,
                            tags,
                            Interlocked.Increment(ref _actualFrameNo),
                            _imgs.Width,
                            _imgs.Height,       
                            imgPositionPatient,
                            ushortArray // メモリ内のJPEGデータを使う    
                        ));
                    }
                }
                UpdateProgressSafe();
            }
            catch (FellowOakDicom.Imaging.DicomImagingException)
            {
                if (onlyFile)
                {
                    // Remove from roots
                    int idxToRemove = UI.ImageRoots.FindIndex(root => root.FilePath == fileLoc);
                    if (idxToRemove != -1)
                        UI.ImageRoots.RemoveAt(idxToRemove);
                }
            }
            catch (PermissionException ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public async Task FromFiles(List<string> files)  // メソッドを非同期にするために`async`を追加
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

            foreach (string parentDir in parentDirs)
            {
                string lastFolderName = new DirectoryInfo(parentDir).Name;
                ImageRoot root = new ImageRoot(parentDir, "", lastFolderName);
                UI.ImageRoots.Add(root);
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
            await Task.WhenAll(tasks);  // awaitを追加してタスクが完了するのを待つ
            Slices = Slices
                .OrderBy(f => f.IMG_Patient_Position.Length > 2 ? f.IMG_Patient_Position[2] : 0)
                .Select((frame, index) =>
                {
                    frame.Number = index; // ソート後のインデックスをNumberにセット
                    return frame;
                }).ToList();
            List<string> lastParentFolderNameOnly = new List<string>();
            foreach (var _root in UI.ImageRoots)
                UI.RootList.Add(_root.DisplayString);
        }
    }
}