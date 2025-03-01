using FellowOakDicom.Network;
using System.Net.NetworkInformation;
using static SATOMI.Pages.DICOMService;
namespace SATOMI.Pages
{
    public partial class ApplicationSettingPage : ContentPage
    {
        public static DICOMServerSettings DCMServerSettings = new DICOMServerSettings();
        public ApplicationSettingPage()
        {
            InitializeComponent();

            DICOMServer.BindingContext = DCMServerSettings;
            LoadSettings();
        }
        protected override void OnAppearing()
        {
            base.OnAppearing();
            _ = this.FadeTo(1, 700, Easing.SinIn);
        }
        // Tab1ボタンがクリックされた場合
        private void OnTab1Clicked(object sender, EventArgs e)
        {
            // タブ1を表示
            DICOMServer.IsVisible = true;
            ApplicationSettings.IsVisible = false;

            // ボタンの背景色を変更
            Tab1Button.BackgroundColor = Colors.Transparent; 
            Tab2Button.BackgroundColor = Color.FromRgb(169, 169, 169);
        }

        private void OnTab2Clicked(object sender, EventArgs e)
        {
            // タブ2を表示
            DICOMServer.IsVisible = false;
            ApplicationSettings.IsVisible = true;

            // ボタンの背景色を変更
            Tab1Button.BackgroundColor = Color.FromRgb(169, 169, 169);
            Tab2Button.BackgroundColor = Colors.Transparent;
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
        private void OnStartServerClicked(object sender, EventArgs e)
        {
            if(DICOMService.StorageServer != null)
            {
                DICOMService.StorageServer.Dispose();
            }
            DCMServerSettings.SaveSettings();
            DICOMService.StorageServer = DicomServerFactory.Create<DicomStorageServer>(DCMServerSettings.PortNumber);
        }
        // 設定の読み込み
        private void LoadSettings()
        {
            DCMServerSettings.AeTitle = Preferences.Get("SCPAeTitle", ""); 
            var portNumberString = Preferences.Get("SCPPortNumber", ""); 
            if (!string.IsNullOrEmpty(portNumberString) && int.TryParse(portNumberString, out int port))
            {
                DCMServerSettings.PortNumber = port;
            }
            else
            {
                DCMServerSettings.PortNumber = 4649;  
            }
        }
    }
}