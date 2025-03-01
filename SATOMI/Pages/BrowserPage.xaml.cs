/*
 * BrowserPage.cs
 * 
 * This file defines a directory browser page in a .NET MAUI application.
 * 
 * Features:
 * - `BrowserPage`: A content page that displays a list of files and folders.
 * - `_loadLstView(string?)`: Loads directories asynchronously and updates the UI.
 * - `OnSwipeOpen(object, EventArgs)`: Handles swipe actions to select folders.
 * - `OnItemDoubleTapped(object, EventArgs)`: Navigates to a selected folder or returns to the parent directory.
 * - `Initialize()`: Initializes the directory view, handling platform-specific storage paths.
 * 
 * The implementation supports directory navigation, selection, swipe actions, and error handling for access permissions.
 *
 * Author: s.harada@HIBMS
 */
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System;
using System.Windows.Input;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using FellowOakDicom;

namespace SATOMI.Pages
{
    public partial class BrowserPage : ContentPage
    {
        string _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        
        public BrowserPage()
        {
            InitializeComponent();
            _ = Initialize();
            this.Opacity = 0;
        }
        protected override void OnAppearing()
        {
            base.OnAppearing();
            _ = this.FadeTo(1, 700, Easing.SinIn);
        }
        private async void OnSwipeOpen(object sender, EventArgs e)
        {
            FileFolderView? selectedItem = LstView.SelectedItem as FileFolderView;
            if (selectedItem == null)
            {
                Debug.Write("selectedItem is null");
                return;
            }
            foreach (var item in BrowserUI.DirListView.Items)
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
            LstView.ItemsSource = BrowserUI.DirListView.Items;
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
                await DisplayAlert("Error", $"Storage initialization error: {ex.Message}", "OK");
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

                BrowserUI.DirListView.Items.Clear();

                var parentDir = Directory.GetParent(path)?.FullName;
                if (parentDir != null)
                {
                    BrowserUI.DirListView.Items.Add(new FileFolderView(parentDir, true, false, true, true));
                }

                foreach (var dir in dirs)
                {
                    BrowserUI.DirListView.Items.Add(new FileFolderView(dir, true));
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
                    await DisplayAlert("Access Denied", "do not have permission to access this directory.", "OK");
                });
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
                    await Shell.Current.GoToAsync($"//ViewerPage?Location={""}");
                });
            }
            finally
            {
                await Task.Delay(100);
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
    }
}
