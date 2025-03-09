/*
 * ViewerPage.cs
 * 
 * Description:
 * This file is part of a DICOM Viewer built with .NET MAUI and FellowOakDicom.
 * It provides functionality to load, display, and interact with DICOM images.
 * 
 * Features:
 * - Loads DICOM files from a single file or a directory
 * - Displays image slices with window width/level adjustments
 * - Supports touch gestures: zoom (pinch), pan (drag), and frame navigation (slider)
 * - Extracts metadata such as patient and study details
 * - Utilizes FellowOakDicom for image processing
 * 
 * Author: s.harada@HIBMS
 */
using System.IO;
using Microsoft.Maui.Controls;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;
using System.Reflection.Metadata;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using FellowOakDicom.Network;
using static SATOMI.Pages.DICOMService;
using Microsoft.Maui.Controls.Internals;
using Microsoft.Maui.Devices;
namespace SATOMI.Pages
{
    [QueryProperty(nameof(Location), "Location")]
    public partial class ViewerPage : ContentPage
    {
        public ViewerPage()
        {
            InitializeComponent();
            DeviceDisplay.MainDisplayInfoChanged += OnMainDisplayInfoChanged;
            try
            {
                ProgBar.BindingContext = UI.ProgressView;
                GridProgBarLbls.BindingContext = UI.ProgressView;
                GridDicomInfo.BindingContext = UI.InfoView;
                GridHeader.BindingContext = UI.ImageInfo;
                WWWLManager.SelectedIndex = 0;
                var portNumberString = Preferences.Get("SCPPortNumber", ""); 
                int PortNumber = 4649;
                if (!string.IsNullOrEmpty(portNumberString) && int.TryParse(portNumberString, out int portset))
                {
                    PortNumber = portset;
                }
                if (DICOMService.StorageServer == null)
                {
                    DICOMService.StorageServer = DicomServerFactory.Create<DicomStorageServer>(PortNumber);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        public static SliceCollection slices = new SliceCollection();

        string _location = "";
        public string Location { get => _location; set => _location = Uri.UnescapeDataString(value); }
        private bool IsFolder { get => Location.EndsWith("/"); }

        private static int _totalSlices = -1;
        private static int _currentSlice = -1;

        public static float DesiredWidth = 0.0f;
        public static float DesiredHeight = 0.0f;
        public static double _offsetX = 0.0f;
        public static double _offsetY = 0.0f;

        public static bool CanDraw = false;
        private bool _servicesRegistered = false;
        public static int _rootIdx = 0;

        private void OnMainDisplayInfoChanged(object? sender, DisplayInfoChangedEventArgs e)
        {
            InitializeDrawPosition();
            GFX.Invalidate();
        }
        protected override async void OnAppearing()
        {
            try
            {
                this.Opacity = 0;
                _ = this.FadeTo(1, 700, Easing.SinIn);

                if (!_servicesRegistered)
                {
                    new DicomSetupBuilder()
                        .RegisterServices(s => s.AddFellowOakDicom()
                            .AddTranscoderManager<FellowOakDicom.Imaging.NativeCodec.NativeTranscoderManager>()
                            .AddImageManager<ImageSharpImageManager>())
                        .SkipValidation()
                        .Build();
                    _servicesRegistered = true;
                }
                await Task.Run(() => LoadLocation(Location));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        private void _loadFromRoot()
        {
            if (slices.View.Count == 0)
                return;
            _totalSlices = slices.View.Count();
            _currentSlice = 0;

            if (_totalSlices > 1)
            {
                SliderFrame.Minimum = 1.0d;
                SliderFrame.Value = 0.0d;
                SliderFrame.Maximum = (double)_totalSlices;
                LblEndSlider.Text = SliderFrame.Maximum.ToString("#");
                LblStartSlider.Text = SliderFrame.Minimum.ToString("#");
                var (averageWindowWidth, averageWindowCenter) = slices.CalculateAverageWindowValues();
                UI.ImageInfo.WW = averageWindowWidth;
                UI.ImageInfo.WL = averageWindowCenter;
                GFX.IsVisible = true;
            }
        }

        private async Task LoadLocation(string loc)
        {
            if (string.IsNullOrEmpty(loc))
                return;

            ResetVars();
            if (!IsFolder)
            {
                await slices.FromFile(loc);
            }
            else
            {
                List<string> files = new List<string>();
                try
                {
                    string[] get_files = Directory.GetFiles(loc, "*.*", SearchOption.TopDirectoryOnly);
                    foreach (string f in get_files)
                    {
                        string fileName = System.IO.Path.GetFileName(f);
                        if (fileName.EndsWith(".dcm", StringComparison.InvariantCultureIgnoreCase) || !fileName.Contains("."))
                            files.Add(f);
                    }
                }
                catch (UnauthorizedAccessException) { }

                await slices.FromFiles(files);
            }

            await _loadUI();
        }

        private async Task _loadUI()
        {
            await Dispatcher.DispatchAsync(() =>
            {
                if (slices.View.Count == 0)
                {
                    CanDraw = false;
                    GridProgBar.IsVisible = false;
                    GridHeader.IsVisible = true;
                    GridBottom.IsVisible = true;
                    _ = DisplayAlert("Failed", "No DICOM or image files found.", "OK");
                    return;
                }
                GridProgBar.IsVisible = false;
                _loadFromRoot();
                GridHeader.IsVisible = true;
                GridBottom.IsVisible = true;
                CanDraw = true;
                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
                GFX.IsVisible = true;

                InitializeDrawPosition();
                _currentSlice = (int)(slices.View.Count * 0.5);
                SliderFrame.Value = _currentSlice;
                _drawFrame(_currentSlice);
            });
        }
        private void InitializeDrawPosition()
        {
            if (slices.View.Count == 0) return;
            _delta_panX = 0;
            _delta_panY = 0;
            _pivotX = 0;
            _pivotY = 0;
            _startX = 0;
            _startY = 0;
            _offsetX = 0;
            _offsetY = 0;
            var mainDisplayInfo = DeviceDisplay.MainDisplayInfo;
            var uiWidth = mainDisplayInfo.Width / mainDisplayInfo.Density;
            var uiHeight = mainDisplayInfo.Height / mainDisplayInfo.Density;
            if (uiWidth < uiHeight)
            {
                _pivotX = uiWidth * 0.5;
                _pivotY = _pivotX;
                _delta_panY = (uiHeight - uiWidth) * 0.5;
                
                aspectRatioX = (float)(uiWidth  /  Math.MaxMagnitude(slices.View[_currentSlice].Width , slices.View[_currentSlice].Height));
                aspectRatioY = aspectRatioX;
            }
            else
            {
                _pivotY = uiHeight * 0.5;
                _pivotX = _pivotY;
                _delta_panX = (uiWidth - uiHeight) * 0.5;
                aspectRatioX = (float)(uiHeight / Math.MaxMagnitude(slices.View[_currentSlice].Width, slices.View[_currentSlice].Height));
                aspectRatioY = aspectRatioX;
            }
            if (slices.View[_currentSlice].PixelSpacingX > slices.View[_currentSlice].PixelSpacingY)
            {
                aspectRatioX *= slices.View[_currentSlice].PixelSpacingX / slices.View[_currentSlice].PixelSpacingY;
            }
            else if (slices.View[_currentSlice].PixelSpacingX < slices.View[_currentSlice].PixelSpacingY)
            {
                aspectRatioY *= slices.View[_currentSlice].PixelSpacingY / slices.View[_currentSlice].PixelSpacingX;
            }
            DesiredWidth = slices.View[_currentSlice].Width * aspectRatioX * _currentScale;
            DesiredHeight = slices.View[_currentSlice].Height * aspectRatioY * _currentScale;
            if (DesiredWidth != DesiredHeight)
            {
                float widthDifference = DesiredWidth - DesiredHeight;
                if (widthDifference > 0)
                {
                    _delta_panY += widthDifference * 0.5;
                }
                else
                {
                    _delta_panX += widthDifference * -0.5;
                }
            }
            _offsetX = _delta_panX - (_pivotX * (_currentScale - 1));
            _offsetY = _delta_panY - (_pivotY * (_currentScale - 1));
        }
        private void ResetVars()
        {
            CanDraw = false;
            _currentSlice = 0;
            slices.Clear();
            Dispatcher.DispatchAsync(() =>
            {
                SliderFrame.Minimum = 1.0d;
                SliderFrame.Value = 1.0d;
                SliderFrame.Maximum = 1.0d;

                GridHeader.IsVisible = false;
                GridBottom.IsVisible = false;
                GridProgBar.IsVisible = true;
            });
        }

        private void _drawFrame(int val)
        {
            if (!CanDraw)
                return;

            if (val < _totalSlices && val >= 0)
            {
                _currentSlice = val;
                LblFrameNo.Text = $"[{_currentSlice + 1}/{_totalSlices}]";
                SetImageInfo();
                GFX.Invalidate();
            }
        }
        public static float _currentScale = 1;
        private bool isPinchInProgress = false;
        private double _startX;
        private double _startY;
        private double _pivotX;
        private double _pivotY;
        private double _delta_panX;
        private double _delta_panY;
        private float aspectRatioX;
        private float aspectRatioY;

        private void OnPickerSelectedIndexChanged(object sender, EventArgs e)
        {
            var picker = (Microsoft.Maui.Controls.Picker)sender;
            int selectedIndex = picker.SelectedIndex;
            switch (selectedIndex)
            {
                case 0:
                    var (averageWindowWidth, averageWindowCenter) = slices.CalculateAverageWindowValues();
                    UI.ImageInfo.WW = averageWindowWidth;
                    UI.ImageInfo.WL = averageWindowCenter;
                    break;
                case 1:
                    UI.ImageInfo.WW = 80;
                    UI.ImageInfo.WL = 40;
                    break;
                case 2:
                    UI.ImageInfo.WW = 1300;
                    UI.ImageInfo.WL = -350;
                    break;
                case 3:
                    UI.ImageInfo.WW = 160;
                    UI.ImageInfo.WL = 40;
                    break;
                case 4:
                    UI.ImageInfo.WW = 2500;
                    UI.ImageInfo.WL = 500;
                    break;
            }
            UI.ImageInfo._current_img = null;
            GFX.Invalidate();
        }
        private void Entry_Completed(object sender, EventArgs e)
        {
            UI.ImageInfo._current_img = null;
            UI.ImageInfo.WL = double.Parse(EntryWindowLevel.Text);
            UI.ImageInfo.WW = double.Parse(EntryWindowWidth.Text);
            GFX.Invalidate();
        }

        private void OnGridHeaderTapped(object sender, TappedEventArgs e)
        {
            if (e is TappedEventArgs tapEvent)
            {
                var touchPosition = tapEvent.GetPosition(GFX);
            }
        }
        private void OnGraphicsViewTapped(object sender, TappedEventArgs e)
        {
            if (e is TappedEventArgs tapEvent)
            {
                var touchPosition = tapEvent.GetPosition(GFX);

                if (touchPosition != null && GFX != null)
                {
                    double bottomAreaHeight = GridBottom.Height + 0;
                    double topAreaHeight = GridHeader.Height + 0;
                    double touchY = touchPosition.Value.Y;
                    if (touchY <= topAreaHeight)
                    {
                        if (!GridHeader.IsVisible || !GridBottom.IsVisible)
                        {
                            GridBottom.IsVisible = true;
                            GridHeader.IsVisible = true;
                        }
                    }
                    else if (touchY >= GFX.Height - bottomAreaHeight)
                    {
                        if (!GridBottom.IsVisible || !GridHeader.IsVisible)
                        {
                            GridBottom.IsVisible = true;
                            GridHeader.IsVisible = true;
                        }
                    }
                    else
                    {
                        if (GridBottom.IsVisible || GridHeader.IsVisible)
                        {
                            GridBottom.IsVisible = false;
                            GridHeader.IsVisible = false;
                        }
                    }
                }
            }
        }
        private void OnPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            GridHeader.IsVisible = false;
            GridBottom.IsVisible = false;
            if (isPinchInProgress) return;
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _startX = _delta_panX;
                    _startY = _delta_panY;
                    break;
                case GestureStatus.Running:
                    _delta_panX = _startX + e.TotalX;
                    _delta_panY = _startY + e.TotalY;
                    _offsetX = _delta_panX - (_pivotX * (_currentScale - 1));
                    _offsetY = _delta_panY - (_pivotY * (_currentScale - 1));
                    GFX.InvalidateMeasure();
                    break;

                case GestureStatus.Completed:
                    break;
            }
        }
        private void OnPinchUpdated(object sender, PinchGestureUpdatedEventArgs e)
        {
            GridHeader.IsVisible = false;
            GridBottom.IsVisible = false;
            switch (e.Status)
            {
                case GestureStatus.Started:
                    isPinchInProgress = true;
                    break;
                case GestureStatus.Running:
                    _currentScale += (float)(e.Scale - 1.0F);
                    _currentScale = Math.Max(1, _currentScale);
                    DesiredWidth = slices.View[_currentSlice].Width * aspectRatioX * _currentScale;
                    DesiredHeight = slices.View[_currentSlice].Height * aspectRatioY * _currentScale;
                    _offsetX = _delta_panX - (_pivotX * (_currentScale - 1));
                    _offsetY = _delta_panY - (_pivotY * (_currentScale - 1));
                    GFX.InvalidateMeasure();
                    break;
                case GestureStatus.Completed:
                    isPinchInProgress = false;
                    break;
            }
        }
        private void SliderFrame_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            if (isPinchInProgress)
                return;
            UI.ImageInfo._current_img = null;
            int val = (int)e.NewValue - 1;
            _drawFrame(val);
        }

        public async Task<FileResult?> ImportFile(PickOptions options)
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(options);
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error picking file: {ex.Message}");
            }

            return null;
        }

        private async void Navigation_Clicked(object sender, EventArgs e)
        {
            GridHeader.IsVisible = false;
            var page = new NavigationPage();
            page.Opacity = 0;
            var currentWindow = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
            if (currentWindow?.Page?.Navigation != null)
            {
                await currentWindow.Page.Navigation.PushModalAsync(page);
                await page.FadeTo(1, 700);
            }
            GridHeader.IsVisible = true;
        }
        private bool isPressingReturnSlice = false;
        private bool isPressingNextSlice = false;
        private bool isButtonPressedDelayed = false;
        private bool isButtonClicked = false;
        private double sliderIncrement = 5;
        private const int ClickDebounceTime = 500;

        private async void ReturnSlice_Pressed(object sender, EventArgs e)
        {
            if (isButtonPressedDelayed || isButtonClicked) return;
            isPressingReturnSlice = true;
            isButtonPressedDelayed = true;
            await Task.Delay(800);
            StartSliderValueChange();
        }
        private async void NextSlice_Pressed(object sender, EventArgs e)
        {
            if (isButtonPressedDelayed || isButtonClicked) return;
            isPressingNextSlice = true;
            isButtonPressedDelayed = true;
            await Task.Delay(800);
            StartSliderValueChange();
        }

        private void ReturnSlice_Released(object sender, EventArgs e)
        {
            isButtonPressedDelayed = false;
            isPressingReturnSlice = false;
            if (!isPressingReturnSlice && isButtonClicked)
            {
                SliderFrame.Value -= 1;
            }
        }
        private void NextSlice_Released(object sender, EventArgs e)
        {
            isButtonPressedDelayed = false;
            isPressingNextSlice = false;
            if (!isPressingNextSlice && isButtonClicked)
            {
                SliderFrame.Value += 1;
            }
        }
        private void StartSliderValueChange()
        {
            Dispatcher.StartTimer(TimeSpan.FromMilliseconds(100), () =>
            {
                if (isPressingReturnSlice)
                {
                    SliderFrame.Value = Math.Max(SliderFrame.Minimum, SliderFrame.Value - sliderIncrement);
                }
                else if (isPressingNextSlice)
                {
                    SliderFrame.Value = Math.Min(SliderFrame.Maximum, SliderFrame.Value + sliderIncrement);
                }
                return isPressingReturnSlice || isPressingNextSlice;
            });
        }

        private void ReturnSlice_Clicked(object sender, EventArgs e)
        {
            if (isButtonClicked) return;
            isButtonClicked = true;
            SliderFrame.Value -= 1;
            isButtonClicked = false;
        }

        private void NextSlice_Clicked(object sender, EventArgs e)
        {
            if (isButtonClicked) return;
            isButtonClicked = true;
            SliderFrame.Value += 1;
            isButtonClicked = false;
        }
        private async void Axial_Clicked(object sender, EventArgs e)
        {
            if (CanDraw)
            {
                if (!slices.MatchesOrientation(SliceOrientation.Axial))
                { 
                    LoadingIndicator.IsVisible = true;
                    LoadingIndicator.IsRunning = true;
                    GFX.IsVisible = false;
                    await Task.Run(() => slices.ChangeOrientation(SliceOrientation.Axial));
                    await _loadUI();
                }
            }
        }
        private async void Sagittal_Clicked(object sender, EventArgs e)
        {
            if (CanDraw)
            {
                if (!slices.MatchesOrientation(SliceOrientation.Sagittal))
                {
                    LoadingIndicator.IsVisible = true;
                    LoadingIndicator.IsRunning = true;
                    GFX.IsVisible = false;
                    await Task.Run(() => slices.ChangeOrientation(SliceOrientation.Sagittal));
                    await _loadUI();
                }
            }
        }
        private async void Coronal_Clicked(object sender, EventArgs e)
        {
            if (CanDraw)
            {
                if (!slices.MatchesOrientation(SliceOrientation.Coronal))
                {
                    LoadingIndicator.IsVisible = true;
                    LoadingIndicator.IsRunning = true;
                    GFX.IsVisible = false;
                    await Task.Run(() => slices.ChangeOrientation(SliceOrientation.Coronal));
                    await _loadUI();
                }
            }
        }
        private void SetImageInfo()
        {
            if (_totalSlices <= 0 || _currentSlice < -1 || _currentSlice >= _totalSlices || _rootIdx < 0)
            {
                return;
            }
            int actualFrameIdx = slices.View.FindIndex(f => f.Number == _currentSlice);

            if (actualFrameIdx >= slices.View.Count)
            {
                return;
            }
            if (actualFrameIdx == -1)
            {
                return;
            }
            UI.InfoView.PatientInfo = slices.View[actualFrameIdx].Info.PatientDetails;
            UI.InfoView.StudyInfo = slices.View[actualFrameIdx].Info.StudyDetails;
        }

        public static ushort[]? GetCurrentSliceOfPixeldata()
        {
            if (_totalSlices <= 0 || _currentSlice < -1 || _currentSlice >= _totalSlices || _rootIdx < 0)
                return null;

            int actualFrameIdx = slices.View.FindIndex(f => f.Number == _currentSlice);

            if (actualFrameIdx >= slices.View.Count)
                return null;

            if (actualFrameIdx == -1)
            {
                return null;
            }
            UI.ImageInfo.current_img_height = slices.View[actualFrameIdx].Height;
            UI.ImageInfo.current_img_width = slices.View[actualFrameIdx].Width;
            UI.ImageInfo.img_slope = slices.View[actualFrameIdx].Slope;
            UI.ImageInfo.img_intercept = slices.View[actualFrameIdx].Intercept;
            return slices.View[actualFrameIdx].Pixeldata;
        }
    }
}


