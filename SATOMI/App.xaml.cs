// This code is part of a Microsoft.Maui application that handles external storage management
// permission requests on Android. The app checks if the necessary permissions are granted 
// to manage external storage, particularly for Android 13 and higher, where a specific 
// permission screen is required for managing storage access. The permission request process 
// ensures that the app can access and modify external storage when granted by the user.
#if ANDROID
using Android.Content.PM;
using Android.App;
using System.Threading.Tasks;
using Android.Content;
using Android.OS;
#endif
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;
using System;
using System.Reflection.Metadata;

namespace SATOMI
{
    public partial class App : Microsoft.Maui.Controls.Application
    {
#if ANDROID
        private TaskCompletionSource<bool> _permissionTaskCompletionSource = new TaskCompletionSource<bool>();
#endif
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)  
        {
            var window = new Window(new AppShell());
            window.Created += async (s, e) =>
            {
                bool isPermissionGranted = await RequestManageExternalStoragePermissionAsync();
                if (isPermissionGranted)
                {
                    Console.WriteLine("ストレージ管理権限が許可されました。");
                }
                else
                {
                    Console.WriteLine("ストレージ管理権限が拒否されました。");
                }
            };

            return window;
        }

        private async Task<bool> RequestManageExternalStoragePermissionAsync()
        {
#if ANDROID
            if ((int)Build.VERSION.SdkInt >= 33) 
            {
#pragma warning disable CA1416 
                if (Android.OS.Environment.IsExternalStorageManager)
                {
                    return true;
                }
#pragma warning restore CA1416 
                _permissionTaskCompletionSource = new TaskCompletionSource<bool>();
                try
                {
#pragma warning disable CA1416 
                    var manage = Android.Provider.Settings.ActionManageAppAllFilesAccessPermission;
#pragma warning restore CA1416
                    Intent intent = new Intent(manage);
                    intent.AddFlags(ActivityFlags.NewTask);

                    Android.Net.Uri? uri = Android.Net.Uri.Parse("package:" + Android.App.Application.Context.PackageName);
                    intent.SetData(uri);

                    if (Platform.CurrentActivity != null)
                    {
                        Platform.CurrentActivity.StartActivityForResult(intent, 1001);
                    }
                    else
                    {
                        Console.WriteLine("Platform.CurrentActivity が null です。");
                        _permissionTaskCompletionSource.TrySetResult(false);
                    }

                    await Task.Yield();
                }
                catch (ActivityNotFoundException)
                {
                    Console.WriteLine("権限設定画面が見つかりませんでした。");
                    _permissionTaskCompletionSource.TrySetResult(false);
                }

                return await _permissionTaskCompletionSource.Task;
            }
            else
            {
                var status = await Permissions.RequestAsync<Permissions.StorageRead>();
                return status == PermissionStatus.Granted;
            }
#else
            await Task.CompletedTask; 
            return false;
#endif
        }

#if ANDROID
        public void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            if ((int)Build.VERSION.SdkInt >= 33)
            {
                if (requestCode == 1001)
                {
#pragma warning disable CA1416
                    bool isPermissionGranted = Android.OS.Environment.IsExternalStorageManager;
#pragma warning restore CA1416 
                    _permissionTaskCompletionSource?.TrySetResult(isPermissionGranted);
                }
            }
        }
#endif
    }
}