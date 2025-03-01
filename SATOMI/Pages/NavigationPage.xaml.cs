using FellowOakDicom;

namespace SATOMI.Pages
{
    public partial class NavigationPage : ContentPage
    {
        public NavigationPage()
        {
            InitializeComponent();
        }
        protected override void OnAppearing()
        {
            base.OnAppearing();
        }
        private async void ApplicationSettings(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync($"//ApplicationSettingPage");
        }
        private async void PatientList(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync($"//PatientListPage");
        }
        private async void ImageViewer(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync($"//ViewerPage?Location={""}");
        }
        private async void ImportLocalFiles(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync($"//BrowserPage");
        }
    }
}