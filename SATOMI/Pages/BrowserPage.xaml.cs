using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System;
using System.Windows.Input;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace SATOMI.Pages
{
    public partial class BrowserPage : ContentPage
    {
        string _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        public ObservableCollection<FileFolderView> DirInfo { get; } = new();
        public ICommand SwipeOpenCommand { get; }

        public BrowserPage()
        {
            InitializeComponent();
            _ = Initialize();
        }

        private async void OnSwipeOpen(object sender, EventArgs e)
        {
            FileFolderView? selectedItem = LstView.SelectedItem as FileFolderView;
            if (selectedItem == null)
            {
                Debug.Write("selectedItem is null");
                return;
            }

            // 状態を更新
            foreach (var item in DirInfo)
            {
                if (item.Location == selectedItem.Location)
                {
                    item.IsSelected = !item.IsSelected;
                }
            }

            if (selectedItem.IsFolder && selectedItem.HasChildren)
            {
                await _loadLstView(selectedItem.Location);
            }
        }

        private async Task Initialize()
        {
            LstView.ItemsSource = DirInfo;
            try
            {
#if ANDROID
                if ((int)Android.OS.Build.VERSION.SdkInt < 23)
                {
                    return;
                }
                else
                {
                    var externalStorage = Android.OS.Environment.ExternalStorageDirectory;
                    _currentPath = externalStorage?.AbsolutePath ?? Android.App.Application.Context.GetExternalFilesDir(null)?.AbsolutePath ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }
#else
                _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
#endif
                await _loadLstView(_currentPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ストレージ初期化エラー: {ex.Message}");
                _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                await _loadLstView(_currentPath);
            }
        }

        private async Task _loadLstView(string? path)
        {
            try
            {
                if (path == null) return;

                _ = MainThread.InvokeOnMainThreadAsync(() =>
                {
                    LstView.BeginRefresh();
                });

                var dirs = await Task.Run(() => Directory.GetDirectories(path)
                                                         .Where(d => !d.Contains("System") &&
                                                                     !d.EndsWith("Thumbs.db") &&
                                                                     !d.Contains(".thumbnails"))
                                                         .ToArray());

                DirInfo.Clear();

                var parentDir = Directory.GetParent(path)?.FullName;
                if (parentDir != null)
                {
                    DirInfo.Add(new FileFolderView(parentDir, true, false, true, true));
                }

                foreach (var dir in dirs)
                {
                    DirInfo.Add(new FileFolderView(dir, true));
                }

                _currentPath = path;

                _ = MainThread.InvokeOnMainThreadAsync(() =>
                {
                    LblTitle.Text = path;
                });
            }
            catch (UnauthorizedAccessException)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlert("アクセス拒否", "このディレクトリにアクセスする権限がありません。", "OK");
                });
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlert("エラー", $"エラーが発生しました: {ex.Message}", "OK");
                });
            }
            finally
            {
                await Task.Delay(100); // UI スレッドに処理の余裕を与える
                _ = MainThread.InvokeOnMainThreadAsync(() =>
                {
                    LstView.EndRefresh();
                });
            }
        }

        private async void OnItemDoubleTapped(object sender, EventArgs e)
        {
            var tappedItem = (sender as View)?.BindingContext as FileFolderView;
            if (tappedItem != null)
            {
                if (tappedItem.Backbtn)
                {
                    var parentDir = Directory.GetParent(_currentPath)?.FullName;
                    if (parentDir != null && parentDir != _currentPath)
                    {
                        await _loadLstView(parentDir);
                    }
                }
                else
                {
                    string loc = tappedItem.Location;
                    if (tappedItem.IsFolder && !loc.EndsWith("/"))
                    {
                        loc = string.Concat(loc, "/");
                    }
                    await Shell.Current.GoToAsync($"//ViewerPage?Location={loc}");
                }
            }
        }

        public class FileFolderView : INotifyPropertyChanged
        {
            public string Location { get; }
            public string SwipeText { get; }
            public Color SwipeColor { get; }
            public bool IsSelected { get; set; }
            public bool IsFolder { get; }
            public bool IsParent { get; }
            public string Name => IsParent ? "..（親フォルダへ戻る）" : Path.GetFileName(Location);
            public string Icon => IsParent ? "up_folder_icon.png" : (IsSelected ? "selected_folder_icon.png" : "folder_icon.png");
            public bool HasChildren { get; }
            public bool CanSwipe { get; }
            public bool Backbtn { get; }
            public event PropertyChangedEventHandler PropertyChanged;

            public FileFolderView(string location, bool isFolder, bool isSelected = false, bool isParent = false, bool back = false)
            {
                Location = location;
                IsFolder = isFolder;
                IsSelected = isSelected;
                IsParent = isParent;

                if (back)
                {
                    Backbtn = true;
                    CanSwipe = false;
                    SwipeText = "          cannot perform a swipe action.";
                    SwipeColor = Colors.Gray;
                }
                else
                {
                    Backbtn = false;
                    if (isFolder)
                    {
                        try
                        {
                            HasChildren = Directory.GetDirectories(Location)
                                                                .Where(d => !d.Contains("System") &&
                                                                !d.EndsWith("Thumbs.db") &&
                                                                !d.Contains(".thumbnails"))
                                                                .Any();
                            CanSwipe = HasChildren;
                            SwipeText = HasChildren ? "          open directory" : "          not found child directory";
                            SwipeColor = HasChildren ? Colors.DarkBlue : Colors.Gray;
                        }
                        catch (Exception)
                        {
                            HasChildren = false;
                            CanSwipe = false;
                            SwipeText = "          cannot perform a swipe action.";
                            SwipeColor = Colors.Gray;
                        }
                    }
                    else
                    {
                        HasChildren = false;
                        SwipeText = "          cannot perform a swipe action.";
                        SwipeColor = Colors.Gray;
                    }
                }
            }
        }
    }
}
