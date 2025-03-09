/*
 * ApplicationSettingPage.cs
 * 
 * Overview:
 * This file defines the ApplicationSettingPage in a .NET MAUI application.
 * It manages the DICOM server settings and provides a UI for users to modify settings.
 * The page allows users to switch between different settings tabs and start/restart the DICOM storage server.
 * 
 * Features:
 * - Load and save DICOM server settings
 * - Tab switching for different settings sections
 * - Start and restart the DICOM storage server
 * - Fade-in animation on page appearance
 * - UI binding for configuration settings
 * 
 * Author: s.harada@HIBMS
 */
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
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            await CheckServerAliveAsync();

            Dispatcher?.StartTimer(TimeSpan.FromSeconds(1), () =>
            {
                _ = CheckServerAliveAsync(); 
                return true; 
            });
            _ = this.FadeTo(1, 700, Easing.SinIn);
        }

        private void OnTab1Clicked(object sender, EventArgs e)
        {

            DICOMServer.IsVisible = true;
            ApplicationSettings.IsVisible = false;

            Tab1Button.BackgroundColor = Colors.Transparent; 
            Tab2Button.BackgroundColor = Color.FromRgb(169, 169, 169);
        }

        private void OnTab2Clicked(object sender, EventArgs e)
        {
   
            DICOMServer.IsVisible = false;
            ApplicationSettings.IsVisible = true;

            
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
            ServerStatusLabel.Text = "Server is not started";
            ServerStatusLabel.TextColor = Colors.Red;
            if (DICOMService.StorageServer != null)
            {
                DICOMService.StorageServer.Dispose();
            }
            DCMServerSettings.SaveSettings();
            DICOMService.StorageServer = DicomServerFactory.Create<DicomStorageServer>(DCMServerSettings.PortNumber);
        }
        
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
        private Task CheckServerAliveAsync()
        {
            if (StorageServer == null)
            {
                ServerStatusLabel.Text = "Server is not started";
                ServerStatusLabel.TextColor = Colors.Red;
                return Task.CompletedTask;
            }
            try
            {
                bool isAlive = StorageServer.IsListening; 
                if (isAlive)
                {
                    ServerStatusLabel.Text = "Server is running";
                    ServerStatusLabel.TextColor = Colors.White;
                }
                else
                {
                    ServerStatusLabel.Text = "Server is not running";
                    ServerStatusLabel.TextColor = Colors.Red;
                }
            }
            catch
            {
                ServerStatusLabel.Text = "Server is not running";
                ServerStatusLabel.TextColor = Colors.Red;
            }

            return Task.CompletedTask;
        }
    }
}