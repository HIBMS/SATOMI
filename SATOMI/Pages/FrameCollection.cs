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
    public class FrameCollection
    {
        public List<Frame> Frames = new List<Frame>();

        // Progress counters
        private int _total = 0;
        private int _current = 0;
        public double WW = 0;
        public double WL = 0;
        public FrameCollection()
        {
            Frames = new List<Frame>();
        }

        // キャッシュをクリアし、再度ファイルから画像を読み込むメソッド
        public Task ClearCacheAndReload(string fileLoc)
        {
            // キャッシュクリアが有効な場合、キャッシュをクリア

            Caching.ClearCache(fileLoc);

            // キャッシュクリア後にファイルを再度読み込む
            //await FromFiles(files);
            return Task.CompletedTask;
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
                // 必要に応じて適切な処理を行ってください
            }
            // 権限確認
            bool isPermissionGranted = await CheckAndRequestStoragePermissionAsync();
            if (!isPermissionGranted)
            {
                Console.WriteLine("ストレージ管理権限が拒否されました。処理を中断します。");
                return;
            }
            _actualFrameNo = 0;
            _fromFile(fileLoc, true);

            // Populate ViewModel based on ImageRoots
            foreach (var _root in UI.ImageRoots)
                UI.RootList.Add(_root.DisplayString);
            //return Task.CompletedTask;
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

        // プログレス更新メソッド
        void UpdateProgressSafe()
        {
            int current = Interlocked.Increment(ref _current);
            if (current <= _total) // 進捗が超えないように制御
            {
                _progessModelUpdate(current, _total);
            }
        }
        private bool _infoModelUpdated = false;
        private int _actualFrameNo = 0; // Folder mode 
        private bool use_cash = false;
        private void _fromFile(string fileLoc, bool onlyFile = true)
        {
            if (use_cash)
            {
                //try
                //{
                //    DicomImage _imgs = new DicomImage(fileLoc);
                //    DicomFile file = DicomFile.Open(fileLoc);
                //    DicomDataset dataset = file.Dataset;  // DicomDatasetを取得
                //    DicomTags tags = new DicomTags(dataset);  // DicomTagsクラスにDatasetを渡す
                //    float[] imgPositionPatient = dataset.GetValues<float>(DicomTag.ImagePositionPatient).ToArray();
                //    _imgs.WindowWidth = 800;
                //    _imgs.WindowCenter = 40;

                //    if (onlyFile)
                //        _total = _imgs.NumberOfFrames;

                //    if (!_infoModelUpdated)
                //    {
                //        _infoModelUpdate(tags);
                //        _infoModelUpdated = true;
                //    }

                //    string saveDir = Caching.GetCachingDirectory(fileLoc);
                //    string fileName = Path.GetFileName(fileLoc);

                //    Caching.IncompleteCacheCheck(saveDir, fileName, _imgs.NumberOfFrames);

                //    // 並列処理
                //    for (int i = 0; i < _imgs.NumberOfFrames; i++)
                //    {
                //        string frameLoc = $"{saveDir}{fileName}_frame{i}.jpg";

                //        // キャッシュが存在する場合はスキップ

                //        if (File.Exists(frameLoc) && new FileInfo(frameLoc).Length > 0)
                //        {
                //            long fileSz = new FileInfo(frameLoc).Length;
                //            if (fileSz > 0) //simple integrity check
                //            {
                //                Frames.Add(new Frame(frameLoc, fileLoc, tags,
                //                    File.ReadAllBytes(frameLoc), Interlocked.Increment(ref _actualFrameNo), _imgs.Width, _imgs.Height, imgPositionPatient));
                //                UpdateProgressSafe();
                //                continue;
                //            }
                //        }

                //        // 画像処理
                //        try
                //        {
                //            if (_isTransferSyntaxSupported(dataset.InternalTransferSyntax))
                //            {
                //                var sw = new System.Diagnostics.Stopwatch();
                //                sw.Start();
                //                Image<Bgra32> sharpImg = _imgs.RenderImage(i).AsSharpImage();
                //                lock (sharpImg)
                //                {
                //                    sharpImg.SaveAsJpeg(frameLoc);
                //                }
                //                sw.Stop();
                //                Console.WriteLine("■_fromFileにかかった時間");
                //                TimeSpan ts = sw.Elapsed;
                //                Console.WriteLine($"　{ts}");
                //                Console.WriteLine($"　{ts.Hours}時間 {ts.Minutes}分 {ts.Seconds}秒 {ts.Milliseconds}ミリ秒");
                //                Console.WriteLine($"　{sw.ElapsedMilliseconds}ミリ秒");
                //            }
                //            else
                //            {
                //                var pixData = DicomPixelData.Create(dataset, false);
                //                var byteBuffer = pixData.GetFrame(i);

                //                using (var outStream = new FileStream(frameLoc, FileMode.Create, FileAccess.Write, FileShare.None))
                //                using (var jpgStream = new MemoryStream(byteBuffer.Data))
                //                using (var jpegImg = new JpegImage(jpgStream))
                //                {
                //                    jpegImg.WriteJpeg(outStream);
                //                }
                //            }
                //            Console.WriteLine(File.ReadAllBytes(frameLoc).Length);
                //            Frames.Add(new Frame(frameLoc, fileLoc, tags,
                //                  File.ReadAllBytes(frameLoc), Interlocked.Increment(ref _actualFrameNo), _imgs.Width, _imgs.Height, imgPositionPatient));

                //        }
                //        catch (Exception ex)
                //        {
                //            Console.WriteLine($"Error processing frame {i}: {ex.Message}");
                //        }

                //        // プログレス更新
                //        UpdateProgressSafe();
                //    };
                //}
                //catch (FellowOakDicom.Imaging.DicomImagingException)
                //{
                //    if (onlyFile)
                //    {
                //        // Remove from roots
                //        int idxToRemove = UI.ImageRoots.FindIndex(root => root.FilePath == fileLoc);
                //        if (idxToRemove != -1)
                //            UI.ImageRoots.RemoveAt(idxToRemove);
                //    }
                //}
                //catch (PermissionException ex)
                //{
                //    Console.WriteLine(ex.Message);
                //}
            }
            else
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
                        var swTotal = new System.Diagnostics.Stopwatch();
                        swTotal.Start();
                        var byteBuffer = pixData.GetFrame(i);
                        var ushortArray = new ushort[byteBuffer.Size / 2];
                        Buffer.BlockCopy(byteBuffer.Data, 0, ushortArray, 0, byteBuffer.Data.Length);
                        var swFrames = new System.Diagnostics.Stopwatch();
                        swFrames.Start();
                        lock (Frames)
                        {
                            Frames.Add(new Frame(
                                "",
                                fileLoc,
                                tags,
                                ushortArray,  // メモリ内のJPEGデータを使う
                                Interlocked.Increment(ref _actualFrameNo),
                                _imgs.Width,
                                _imgs.Height,
                                imgPositionPatient
                            ));
                        }
                        swFrames.Stop();
                        Console.WriteLine($"Adding to Frames took: {swFrames.ElapsedMilliseconds} ms");
                        swTotal.Stop();
                        Console.WriteLine("■_fromFile 全体の時間");
                        Console.WriteLine($"Total elapsed time: {swTotal.ElapsedMilliseconds} ms");
                        Console.WriteLine($"Total elapsed time: {swTotal.Elapsed.Hours} hours {swTotal.Elapsed.Minutes} minutes {swTotal.Elapsed.Seconds} seconds {swTotal.ElapsedMilliseconds} milliseconds");
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
        }

        public async Task FromFiles(List<string> files)  // メソッドを非同期にするために`async`を追加
        {
            _progessModelReset();
            _total = files.Count;
            _infoModelUpdated = false;

            // Assign 'Roots'
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

                // インデックスを渡す
                tasks.Add(Task.Run(() => _fromFile(file, false)));
            }

            // 全てのタスクが完了するまで待機
            await Task.WhenAll(tasks);  // awaitを追加してタスクが完了するのを待つ

            // タスク完了後にFramesをソート
            Frames = Frames
                .OrderBy(f => f.IMG_Patient_Position.Length > 2 ? f.IMG_Patient_Position[2] : 0)
                .Select((frame, index) =>
                {
                    frame.Number = index; // ソート後のインデックスをNumberにセット
                    return frame;
                }).ToList();
            // 完了後にUI更新処理
            List<string> lastParentFolderNameOnly = new List<string>();
            foreach (var _root in UI.ImageRoots)
                UI.RootList.Add(_root.DisplayString);

            //return Task.CompletedTask;  // Task.CompletedTaskを返す
        }
        private static string[] _supportedUIDs = new string[] {
            "1.2.840.10008.1.2",
            "1.2.840.10008.1.2.1",
            "1.2.840.10008.1.2.1.99",
            "1.2.840.10008.1.2.2",
            "1.2.840.10008.1.2.5"
        };

        private bool _isTransferSyntaxSupported(DicomTransferSyntax syntax)
        {
            // All codecs supported on Win, MAC, Linux, otherwise for mobile we have to use LibJPEG for unsupported formats
            return (DeviceInfo.Current.Idiom == DeviceIdiom.Desktop) ? true : _supportedUIDs.Contains(syntax.UID.UID);
        }
    }
}