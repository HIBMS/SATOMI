using System.Collections.ObjectModel;
using System.Reflection;

namespace SATOMI.Pages;

public partial class BrowserPage : ContentPage
{
    string _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    public ObservableCollection<FileFolderView> DirInfo { get; } = new() { };

    public BrowserPage()
    {
        InitializeComponent();
        _ = Initialize();
    }

    private async Task Initialize()
    {
        LstView.ItemsSource = DirInfo; //set the UI binding
        try
        {
#if ANDROID
            if ((int)Android.OS.Build.VERSION.SdkInt < 23)
            {
                return;
            }
            else
            {
            }
            // Get External Storage Directory
            var externalStorage = Android.OS.Environment.ExternalStorageDirectory;
            if (externalStorage != null && !string.IsNullOrEmpty(externalStorage.AbsolutePath))
            {
                _currentPath = externalStorage.AbsolutePath;
            }
            else
            {
                // ExternalStorageDirectoryがnullまたは空の場合のフォールバック処理
                _currentPath = Android.App.Application.Context.GetExternalFilesDir(null)?.AbsolutePath ?? string.Empty;
                if (string.IsNullOrEmpty(_currentPath))
                {
                    _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments); // 最終フォールバック
                }
            }
#else
             _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
#endif
            // Load List View
             await _loadLstView(_currentPath); 
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ストレージ初期化エラー: {ex.Message}");
            _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments); // フォールバック
            await _loadLstView(_currentPath); 
        }
    }


    private async Task _loadLstView(string? path)
    {
        try
        {
            if (path != null)
            {
                LstView.BeginRefresh();

                _prevTappedLoc = null;
                _prevTappedIsFolder = false;

                // 非同期でディレクトリとファイルを取得
                var dirs = await Task.Run(() => Directory.GetDirectories(path));
                var files = await Task.Run(() => Directory.GetFiles(path));

                if (dirs.Length + files.Length <= 0)
                    return;

                DirInfo.Clear();

                foreach (var dir in dirs)
                    DirInfo.Add(new FileFolderView(dir, true));
                foreach (var file in files)
                    DirInfo.Add(new FileFolderView(file, false));

                _currentPath = path;
                LblTitle.Text = path;
                string? parentDir = Directory.GetParent(path)?.ToString();
                if (parentDir != null)
                {
                    BtnBack.IsVisible = parentDir != path;
                }
                else
                {
                    BtnBack.IsVisible = false; // ルートディレクトリの場合はBackボタンを表示しない
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            _ = DisplayAlert("Insufficient Privileges", $"Unauthorized access to {path}", "Ok");
            BtnBack.IsVisible = false;
        }
        catch (TargetInvocationException)
        {
            await DisplayAlert("Insufficient Permissions", $"Read/Write permissions have not been granted.", "Ok");
            BtnBack.IsVisible = false;
        }
        finally
        {
            LstView.EndRefresh();
        }
    }


    private string? _prevTappedLoc = string.Empty;
    private bool _prevTappedIsFolder = false;
    private void LstView_ItemTapped(object sender, ItemTappedEventArgs e)
    {
        FileFolderView? selectedItem = LstView.SelectedItem as FileFolderView;
        if (selectedItem == null)
        {
            // selectedItem が null の場合は処理を中断
            return;
        }
        //Update IsSelected Property
        for (int i = 0; i < DirInfo.Count; i++)
        {
            bool isSelected = DirInfo[i].Location == selectedItem.Location;
            if (DirInfo[i].IsSelected != isSelected)
                DirInfo[i] = new FileFolderView(DirInfo[i].Location, DirInfo[i].IsFolder, isSelected);
        }

        //If Double Tapped -> Expand Folder
        if (_prevTappedLoc == selectedItem.Location)
        {
            if (selectedItem.IsFolder && selectedItem.HasChildren)
                _ = _loadLstView(selectedItem.Location);
        }
        _prevTappedLoc = selectedItem.Location;
        _prevTappedIsFolder = selectedItem.IsFolder;
    }

    private void BtnBack_Clicked(object sender, EventArgs e)
    {
        string? parentDir = Directory.GetParent(_currentPath)?.ToString();
        if (parentDir == null)
        {
            // _currentPath がルートディレクトリの場合や parentDir が null の場合の処理
            // 必要に応じて適切な処理を行ってください
        }
        _ = _loadLstView(parentDir);
    }

    private async void BtnSelected_Clicked(object sender, EventArgs e)
    {
        if (_prevTappedLoc != null)
        {
            string loc = _prevTappedLoc;
            if (_prevTappedIsFolder && !_prevTappedLoc.EndsWith("/"))
                loc = string.Concat(_prevTappedLoc, "/");
            await Shell.Current.GoToAsync($"//ViewerPage?Location={loc}");
        }
    }

    private async void BtnCashClear_Clicked(object sender, EventArgs e)
    {
        if (_prevTappedLoc != null)
        {
            string loc = _prevTappedLoc;
            if (_prevTappedIsFolder && !_prevTappedLoc.EndsWith("/"))
                loc = string.Concat(_prevTappedLoc, "/");
                    await Shell.Current.GoToAsync($"//ViewerPage?Location={loc}");
        }
    }
}
