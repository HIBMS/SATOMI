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
            //MainPage = new AppShell(); // 初期ページとして AppShell を指定
        }

        protected override Window CreateWindow(IActivationState? activationState) // NULL 許容を削除
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
            if ((int)Build.VERSION.SdkInt >= 33) // Android 13以降
            {
#pragma warning disable CA1416 // プラットフォームの互換性を検証
                if (Android.OS.Environment.IsExternalStorageManager)
                {
                    // 既に権限が付与されている
                    return true;
                }
#pragma warning restore CA1416 // プラットフォームの互換性を検証

                _permissionTaskCompletionSource = new TaskCompletionSource<bool>();
                try
                {
#pragma warning disable CA1416 // プラットフォームの互換性を検証
                    var manage = Android.Provider.Settings.ActionManageAppAllFilesAccessPermission;
#pragma warning restore CA1416 // プラットフォームの互換性を検証
                    Intent intent = new Intent(manage);
                    intent.AddFlags(ActivityFlags.NewTask);

                    // アプリのパッケージ名を設定
                    Android.Net.Uri? uri = Android.Net.Uri.Parse("package:" + Android.App.Application.Context.PackageName);
                    intent.SetData(uri);

                    // Platform.CurrentActivity の null チェックを追加
                    if (Platform.CurrentActivity != null)
                    {
                        Platform.CurrentActivity.StartActivityForResult(intent, 1001);
                    }
                    else
                    {
                        Console.WriteLine("Platform.CurrentActivity が null です。");
                        _permissionTaskCompletionSource.TrySetResult(false);
                    }

                    // ここで非同期コンテキストを維持するために await を追加
                    await Task.Yield();
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
            // 現在Android以外のプラットフォームでは対応しない
            await Task.CompletedTask; 
            return false;
#endif
        }

#if ANDROID
        // アクティビティ結果のコールバックをハンドリング
        public void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            if ((int)Build.VERSION.SdkInt >= 33)
            {
                if (requestCode == 1001)
                {
#pragma warning disable CA1416 // プラットフォームの互換性を検証
                    bool isPermissionGranted = Android.OS.Environment.IsExternalStorageManager;
#pragma warning restore CA1416 // プラットフォームの互換性を検証
                    _permissionTaskCompletionSource?.TrySetResult(isPermissionGranted);
                }
            }
        }
#endif
    }
}