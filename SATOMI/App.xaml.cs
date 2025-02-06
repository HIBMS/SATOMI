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
        private TaskCompletionSource<bool> _permissionTaskCompletionSource;

        public App()
        {
            InitializeComponent();
            //MainPage = new AppShell(); // 初期ページとして AppShell を指定
        }

        protected override Window CreateWindow(IActivationState activationState)
        {
            //Window window = base.CreateWindow(activationState);
            // ウィンドウを作成し、AppShell を初期ページとして設定
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
            if ((int)Build.VERSION.SdkInt >= 33) // Android 13以降
            {
                if (Android.OS.Environment.IsExternalStorageManager)
                {
                    // 既に権限が付与されている
                    return true;
                }

                _permissionTaskCompletionSource = new TaskCompletionSource<bool>();
                try
                {
                    var manage = Android.Provider.Settings.ActionManageAppAllFilesAccessPermission;
                    Intent intent = new Intent(manage);
                    intent.AddFlags(ActivityFlags.NewTask);

                    // アプリのパッケージ名を設定
                    Android.Net.Uri? uri = Android.Net.Uri.Parse("package:" + Android.App.Application.Context.PackageName);
                    intent.SetData(uri);

                    // 結果を待つためにアクティビティを開始
                    Platform.CurrentActivity.StartActivityForResult(intent, 1001);
                }
                catch (ActivityNotFoundException)
                {
                    Console.WriteLine("権限設定画面が見つかりませんでした。");
                    _permissionTaskCompletionSource.TrySetResult(false);
                }

                // 結果を待つ
                return await _permissionTaskCompletionSource.Task;
            }
            else
            {
                // Android 13未満の通常の権限リクエスト
                var status = await Permissions.RequestAsync<Permissions.StorageRead>();
                return status == PermissionStatus.Granted;
            }
#else
            return false;
#endif
        }

#if ANDROID
        // アクティビティ結果のコールバックをハンドリング
        public void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            if (requestCode == 1001)
            {
                bool isPermissionGranted = Android.OS.Environment.IsExternalStorageManager;
                _permissionTaskCompletionSource?.TrySetResult(isPermissionGranted);
            }
        }
#endif
    }
}