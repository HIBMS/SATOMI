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

namespace SATOMI.Pages
{
    [QueryProperty(nameof(Location), "Location")]
    public partial class ViewerPage : ContentPage
    {
        public ViewerPage()
        {
            InitializeComponent();

            ProgBar.BindingContext = UI.ProgressView;
            GridProgBarLbls.BindingContext = UI.ProgressView;
            GridDicomInfo.BindingContext = UI.InfoView;
            GridHeader.BindingContext = UI.ImageInfo;
            WWWLManager.SelectedIndex = 0;
        }

        public static FrameCollection _framec = new FrameCollection();

        string _location = "";
        public string Location { get => _location; set => _location = Uri.UnescapeDataString(value); }
        private bool IsFolder { get => Location.EndsWith("/"); }

        private static int _totalFrames = -1;
        private static int _currentFrame = -1;

        //public static int current_img_width = 0;
        //public static int current_img_height = 0;
        private double _WW = 400.0;
        

        public static float DesiredWidth = 0.0f;
        public static float DesiredHeight = 0.0f;
        public static double _offsetX = 0.0f;
        public static double _offsetY = 0.0f;

        public static bool CanDraw = false;
        //public static byte[]? _current_img = null;
        //public static Microsoft.Maui.Graphics.IImage? _current_img = null;
        private bool _servicesRegistered = false;
        public static int _rootIdx = 0;


        protected override async void OnAppearing()
        {
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

        private void _loadFromRoot()
        {
            if (_framec.Frames.Count == 0)
                return;

            //int rootIdx = PickerRoot.SelectedIndex;
            int rootIdx = 0;
            if (rootIdx >= 0)
            {
                ImageRoot root = UI.ImageRoots[rootIdx];

                if (root.IsFolder)
                {
                    _totalFrames = _framec.Frames.Count(frame => frame.OriginalDirectory == root.FullFolderPath);
                    _currentFrame = 0;
                }
                else
                {
                    _totalFrames = _framec.Frames.Count(frame => frame.DicomLoc == root.FilePath);
                    _currentFrame = 0;
                }
            }

            if (_totalFrames > 1)
            {
                // Initilize
                SliderFrame.Minimum = 1.0d;
                SliderFrame.Value = 0.0d;
                SliderFrame.Maximum = (double)_totalFrames;
                LblEndSlider.Text = SliderFrame.Maximum.ToString("#");
                LblStartSlider.Text = SliderFrame.Minimum.ToString("#");
                var mainDisplayInfo = DeviceDisplay.MainDisplayInfo;
                _delta_panY = mainDisplayInfo.Height / mainDisplayInfo.Density * 0.5 - GFX.Height * 0.5 ;
                _offsetY = _delta_panY - (_pivotY * (_currentScale - 1));
                UI.ImageInfo.WW = _framec.WW;
                UI.ImageInfo.WL = _framec.WL;
                //EntryWindowLevel.Text = UI.ViewInfo.WL.ToString();
                //EntryWindowWidth.Text = UI.ViewInfo.WW.ToString();
            }
        }

        private async Task LoadLocation(string loc)
        {
            if (string.IsNullOrEmpty(loc))
                return;

            ResetVars();
            if (!IsFolder)
            {
                await _framec.FromFile(loc);
            }
            else
            {
                List<string> files = new List<string>();
                try
                {
                    string[] get_files = Directory.GetFiles(loc, "*.*", SearchOption.AllDirectories);
                    foreach (string f in get_files)
                    {
                        string fileName = System.IO.Path.GetFileName(f);
                        if (fileName.EndsWith(".dcm", StringComparison.InvariantCultureIgnoreCase) || !fileName.Contains("."))
                            files.Add(f);
                    }
                }
                catch (UnauthorizedAccessException) { }

                await _framec.FromFiles(files);
            }

            await _loadUI();
        }

        private async Task _loadUI()
        {
            // Dispatchを使ってメインスレッドで処理を行う
            await Dispatcher.DispatchAsync(async () =>
            {
                //BtnFocusPicker.IsVisible = UI.ImageRoots.Count > 1;

                if (_framec.Frames.Count == 0)
                {
                    CanDraw = false;
                    GridProgBar.IsVisible = false;
                    GridHeader.IsVisible = false;
                    GridBottom.IsVisible = false;
                    _ = DisplayAlert("Failed", "No DICOM or image files found.", "OK");
                    return;
                }

                GridProgBar.IsVisible = false;

                _loadFromRoot();

                GridHeader.IsVisible = true;
                CanDraw = true;
                _drawFrame(0);
                //GFX.Scale = 1.0;
            });
        }

        private void ResetVars()
        {
            CanDraw = false;
            _currentFrame = 0;

            _framec.Frames.Clear();
            _framec = new FrameCollection();

            var mainDisplayInfo = DeviceDisplay.MainDisplayInfo;
            var uiWidth = mainDisplayInfo.Width / mainDisplayInfo.Density;
            var uiHeight = mainDisplayInfo.Height / mainDisplayInfo.Density;
            DesiredWidth = (float)uiWidth;
            DesiredHeight = (float)uiWidth;
            //Console.WriteLine(GFX.Width);
            //GFX.HeightRequest = GFX.Width;
            GFX.WidthRequest = uiWidth;
            GFX.HeightRequest = uiHeight;
            // Dispatcher.DispatchAsyncを使用してメインスレッドでUI更新
            Dispatcher.DispatchAsync(() =>
            {
                GFX.HeightRequest = (int)GFX.Width;

                SliderFrame.Minimum = 1.0d;
                SliderFrame.Value = 1.0d;
                SliderFrame.Maximum = 1.0d;

                GridHeader.IsVisible = false;
                GridBottom.IsVisible = false;
                GridProgBar.IsVisible = true;
                //GFX.Scale = 1.0;
                //BtnFocusPicker.IsVisible = false;
            });
        }

        private void _drawFrame(int val)
        {
            if (!CanDraw)
                return;

            if (val < _totalFrames && val >= 0)
            {
                _currentFrame = val;
                LblFrameNo.Text = $"[{_currentFrame + 1}/{_totalFrames}]";
                //LblCurrentZoom.Text = $"[{_currentFrame + 1}/{_totalFrames}]";
                _setInfoModel();
                //DrawImage();
                var mainDisplayInfo = DeviceDisplay.MainDisplayInfo;
                GFX.HeightRequest = mainDisplayInfo.Height / mainDisplayInfo.Density;
                GFX.Invalidate();

            }
        }

        public ImageSource _imgs;
        public static double _currentScale = 1;
        private bool isPinchInProgress = false;
        private double _startX;
        private double _startY;
        private double _pivotX;
        private double _pivotY;
        private double _delta_panX;
        private double _delta_panY;

        private void OnPickerSelectedIndexChanged(object sender, EventArgs e)
        {
            var picker = (Microsoft.Maui.Controls.Picker)sender;
            int selectedIndex = picker.SelectedIndex;
            switch(selectedIndex)
            {
                case 0:
                    UI.ImageInfo.WW = 400;
                    UI.ImageInfo.WL = 40;
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
            //this.textChangedLabel.Text = "User completed entering text";
            UI.ImageInfo._current_img = null;
            UI.ImageInfo.WL =  double.Parse( EntryWindowLevel.Text );
            UI.ImageInfo.WW =  double.Parse( EntryWindowWidth.Text );
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
                    double bottomAreaHeight = GridBottom.Height + 50;
                    double topAreaHeight = GridHeader.Height + 50;
                    // 下部タッチでスライダーを表示
                    // 上部タッチエリアの場合
                    double touchY = touchPosition.Value.Y;
                    if (touchY <= topAreaHeight)
                    {
                        if (!GridHeader.IsVisible || !GridBottom.IsVisible)
                        {
                            GridBottom.IsVisible = true;
                            GridHeader.IsVisible = true;
                        }
                    }
                    // 下部タッチエリアの場合
                    else if (touchY >= GFX.Height - bottomAreaHeight)
                    {
                        if (!GridBottom.IsVisible || !GridHeader.IsVisible)
                        {
                            GridBottom.IsVisible = true;
                            GridHeader.IsVisible = true;
                        }
                    }
                    // その他の領域の場合は非表示
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
                    var mainDisplayInfo = DeviceDisplay.MainDisplayInfo;
                    _pivotX = mainDisplayInfo.Width / mainDisplayInfo.Density * 0.5;
                    _pivotY = _pivotX;
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
            var mainDisplayInfo = DeviceDisplay.MainDisplayInfo;
            switch (e.Status)
            {
                case GestureStatus.Started:
                    isPinchInProgress = true;
                    _pivotX = mainDisplayInfo.Width / mainDisplayInfo.Density * 0.5;
                    _pivotY = _pivotX;
                    break;
                case GestureStatus.Running:
                    _currentScale += (e.Scale - 1);
                    _currentScale = Math.Max(1, _currentScale);
                    DesiredWidth = (float)(mainDisplayInfo.Width / mainDisplayInfo.Density * _currentScale);
                    DesiredHeight = DesiredWidth;

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
                return; // ピンチ中はスライダー操作を無視
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
                // Log the exception, e.g., using Debug or logging library
                Console.WriteLine($"Error picking file: {ex.Message}");
                // Optionally, handle or rethrow specific exceptions if needed
            }

            return null;
        }

        private async void BtnImport_Clicked(object sender, EventArgs e)
        {
            GridHeader.IsVisible = false;
            await Shell.Current.GoToAsync("//BrowserPage"); //Define uri in AppShell.xaml
            GridHeader.IsVisible = true;
        }

        private void PickerRoot_SelectedIndexChanged(object sender, EventArgs e)
        {
            //_rootIdx = PickerRoot.SelectedIndex;
            //_loadFromRoot();
            //_setInfoModel();
            //GFX.Invalidate();
        }

        private void BtnFocusPicker_Clicked(object sender, EventArgs e)
        {
            //if (PickerRoot.Items.Count > 1)
            //    PickerRoot.Focus();
        }
        private void _resetInfoModel()
        {
            UI.InfoView.Clear();
        }

        private void _setInfoModel()
        {
            if (_totalFrames <= 0 || _currentFrame < -1 || _currentFrame >= _totalFrames || _rootIdx < 0)
            {
                _resetInfoModel();
                return;
            }

            ImageRoot root = UI.ImageRoots[_rootIdx];
            int actualFrameIdx = _framec.Frames.FindIndex(f => f.Number == _currentFrame && f.OriginalDirectory == root.FullFolderPath);

            if (actualFrameIdx >= _framec.Frames.Count)
            {
                _resetInfoModel();
                return;
            }
            if (actualFrameIdx == -1)
            {
                Console.WriteLine("Frame not found: Current Frame = " + _currentFrame + ", Root Path = " + root.FullFolderPath);
                return;
            }
            UI.InfoView.PatientInfo = _framec.Frames[actualFrameIdx].Info.PatientDetails;
            UI.InfoView.StudyInfo = _framec.Frames[actualFrameIdx].Info.StudyDetails;

        }
        public static byte[]? GetCurrentFrameBuffer()
        {
            if (_totalFrames <= 0 || _currentFrame < -1 || _currentFrame >= _totalFrames || _rootIdx < 0)
                return null;

            ImageRoot root = UI.ImageRoots[_rootIdx];
            int actualFrameIdx = _framec.Frames.FindIndex(f => f.Number == _currentFrame && f.OriginalDirectory == root.FullFolderPath);

            if (actualFrameIdx >= _framec.Frames.Count)
                return null;

            if (actualFrameIdx == -1)
            {
                Console.WriteLine("Frame not found: Current Frame = " + _currentFrame + ", Root Path = " + root.FullFolderPath);
                return null;
            }
            UI.ImageInfo.current_img_height = _framec.Frames[actualFrameIdx].Height;
            UI.ImageInfo.current_img_width = _framec.Frames[actualFrameIdx].Width;

            return _framec.Frames[actualFrameIdx].Buffer;
        }
        public static ushort[]? GetCurrentFramePixeldata()
        {
            if (_totalFrames <= 0 || _currentFrame < -1 || _currentFrame >= _totalFrames || _rootIdx < 0)
                return null;

            ImageRoot root = UI.ImageRoots[_rootIdx];
            int actualFrameIdx = _framec.Frames.FindIndex(f => f.Number == _currentFrame && f.OriginalDirectory == root.FullFolderPath);

            if (actualFrameIdx >= _framec.Frames.Count)
                return null;

            if (actualFrameIdx == -1)
            {
                Console.WriteLine("Frame not found: Current Frame = " + _currentFrame + ", Root Path = " + root.FullFolderPath);
                return null;
            }
            UI.ImageInfo.current_img_height = _framec.Frames[actualFrameIdx].Height;
            UI.ImageInfo.current_img_width = _framec.Frames[actualFrameIdx].Width;

            return _framec.Frames[actualFrameIdx].Pixeldata;
        }
    }
}


