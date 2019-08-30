using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.OS;
using Android.Support.V4.App;
using Android.Support.V4.Content;
using Android.Support.V7.App;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.Lang;
using Java.Util;
using Recognizer.Core.Resources.Strings;
using RecognizerTestApp.Helpers;
using RecognizerTestApp.Services;
using RecognizerTestApp.Settings;
using Camera = Android.Hardware.Camera;
using Math = System.Math;
using Toolbar = Android.Support.V7.Widget.Toolbar;

namespace RecognizerTestApp
{
    [Activity(Label = "@string/app_name", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, TextureView.ISurfaceTextureListener
    {
        private const int REQUEST_CAMERA_ID = 1001;
        private const int REQUEST_WRITE_ID = 1002;
        private const int REQUEST_INTERNET_ID = 1003;


        private static readonly string TAG = "Recognizer";
        // Max preview width that is guaranteed by Camera2 API
        private static readonly int MAX_PREVIEW_WIDTH = 1920;

        // Max preview height that is guaranteed by Camera2 API
        private static readonly int MAX_PREVIEW_HEIGHT = 1080;

        private Camera _camera;

        private OverlayView _overlayView;
        private Camera.Size _previewSize;
        private bool _rightCheckInProgress;

        private RecognizerService _recognizerService;

        private TextureView _textureView;
        private TextView _textView;
        private Toolbar _toolbar;
        private ImageView _imageView;
        private Button _rerunButton;
        private SurfaceTexture _surface;
        private Size _realSurfaceSize = new Size(0,0);

        public void OnSurfaceTextureAvailable(SurfaceTexture surface, int width, int height)
        {
            if (Camera.NumberOfCameras == 0)
            {
                Toast.MakeText(this, CommonResources.no_camera, ToastLength.Long).Show();
                return;
            }

            _surface = surface;

            CheckRights();
        }

        private static Size ChooseOptimalSize(Size[] choices, int textureViewWidth,
            int textureViewHeight, int maxWidth, int maxHeight, Size aspectRatio)
        {
            // Collect the supported resolutions that are at least as big as the preview Surface
            var bigEnough = new List<Size>();
            // Collect the supported resolutions that are smaller than the preview Surface
            var notBigEnough = new List<Size>();
            int w = aspectRatio.Width;
            int h = aspectRatio.Height;

            for (var i = 0; i < choices.Length; i++)
            {
                Size option = choices[i];
                if ((option.Width <= maxWidth) && (option.Height <= maxHeight) &&
                    option.Height == option.Width * h / w)
                {
                    if (option.Width >= textureViewWidth &&
                        option.Height >= textureViewHeight)
                    {
                        bigEnough.Add(option);
                    }
                    else
                    {
                        notBigEnough.Add(option);
                    }
                }
            }

            // Pick the smallest of those big enough. If there is no one big enough, pick the
            // largest of those not big enough.
            if (bigEnough.Count > 0)
            {
                return (Size)Collections.Min(bigEnough, new CompareSizesByArea());
            }
            else if (notBigEnough.Count > 0)
            {
                return (Size)Collections.Max(notBigEnough, new CompareSizesByArea());
            }
            else
            {
                Log.Error("Rec", "Couldn't find any suitable preview size");
                return choices[0];
            }
        }

        private void SetUpCameraOutputs(int width, int height)
        {
            var activity = this;
            var manager = (CameraManager)activity.GetSystemService(Context.CameraService);
            try
            {
                for (var i = 0; i < manager.GetCameraIdList().Length; i++)
                {
                    var cameraId = manager.GetCameraIdList()[i];
                    CameraCharacteristics characteristics = manager.GetCameraCharacteristics(cameraId);

                    // We don't use a front facing camera in this sample.
                    var facing = (Integer)characteristics.Get(CameraCharacteristics.LensFacing);
                    if (facing != null && facing == (Integer.ValueOf((int)LensFacing.Front)))
                    {
                        continue;
                    }

                    var map = (StreamConfigurationMap)characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap);
                    if (map == null)
                    {
                        continue;
                    }

                    // For still image captures, we use the largest available size.
                    //Size largest = (Size)Collections.Max(Arrays.AsList(map.GetOutputSizes((int)ImageFormatType.Jpeg)),
                    //    new CompareSizesByArea());

                    // Find out if we need to swap dimension to get the preview size relative to sensor
                    // coordinate.
                    var displayRotation = activity.WindowManager.DefaultDisplay.Rotation;
                    //noinspection ConstantConditions
                    var sensorOrientation = (int)characteristics.Get(CameraCharacteristics.SensorOrientation);
                    bool swappedDimensions = false;
                    switch (displayRotation)
                    {
                        case SurfaceOrientation.Rotation0:
                        case SurfaceOrientation.Rotation180:
                            if (sensorOrientation == 90 || sensorOrientation == 270)
                            {
                                swappedDimensions = true;
                            }
                            break;
                        case SurfaceOrientation.Rotation90:
                        case SurfaceOrientation.Rotation270:
                            if (sensorOrientation == 0 || sensorOrientation == 180)
                            {
                                swappedDimensions = true;
                            }
                            break;
                        default:
                            break;
                    }

                    Point displaySize = new Point();
                    activity.WindowManager.DefaultDisplay.GetSize(displaySize);
                    var rotatedPreviewWidth = width;
                    var rotatedPreviewHeight = height;
                    var maxPreviewWidth = displaySize.X;
                    var maxPreviewHeight = displaySize.Y;

                    if (swappedDimensions)
                    {
                        rotatedPreviewWidth = height;
                        rotatedPreviewHeight = width;
                        maxPreviewWidth = displaySize.Y;
                        maxPreviewHeight = displaySize.X;
                    }

                    if (maxPreviewWidth > MAX_PREVIEW_WIDTH)
                    {
                        maxPreviewWidth = MAX_PREVIEW_WIDTH;
                    }

                    if (maxPreviewHeight > MAX_PREVIEW_HEIGHT)
                    {
                        maxPreviewHeight = MAX_PREVIEW_HEIGHT;
                    }

                    // Danger, W.R.! Attempting to use too large a preview size could  exceed the camera
                    // bus' bandwidth limitation, resulting in gorgeous previews but the storage of
                    // garbage capture data.
                    //_previewSize = ChooseOptimalSize(map.GetOutputSizes(Class.FromType(typeof(SurfaceTexture))),
                    //    rotatedPreviewWidth, rotatedPreviewHeight, maxPreviewWidth,
                    //    maxPreviewHeight, largest);

                    Size largest = new Size(maxPreviewWidth, maxPreviewHeight);
                    var aspectRatio = ChooseOptimalSize(map.GetOutputSizes(Class.FromType(typeof(SurfaceTexture))),
                        rotatedPreviewWidth, rotatedPreviewHeight, maxPreviewWidth,
                        maxPreviewHeight, largest);

                    //_textureView.SetAspectRatio(aspectRatio.Height, aspectRatio.Width);
                    //_textureView.SetAspectRatio(aspectRatio.Width, aspectRatio.Height);

                    return;
                }
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
            catch (NullPointerException e)
            {
                // Currently an NPE is thrown when the Camera2API is used but not supported on the
                // device this code runs.
                Console.WriteLine(e);
            }
        }


        public void ConfigureTransform(int viewWidth, int viewHeight)
        {
            Activity activity = this;
            if (null == _textureView || null == _previewSize || null == activity)
            {
                return;
            }
            var rotation = (int)activity.WindowManager.DefaultDisplay.Rotation;
            Matrix matrix = new Matrix();
            RectF viewRect = new RectF(0, 0, viewWidth, viewHeight);
            RectF bufferRect = new RectF(0, 0, _previewSize.Height, _previewSize.Width);
            float centerX = viewRect.CenterX();
            float centerY = viewRect.CenterY();
            if ((int)SurfaceOrientation.Rotation90 == rotation || (int)SurfaceOrientation.Rotation270 == rotation)
            {
                bufferRect.Offset(centerX - bufferRect.CenterX(), centerY - bufferRect.CenterY());
                matrix.SetRectToRect(viewRect, bufferRect, Matrix.ScaleToFit.Fill);
                float scale = Math.Max((float)viewHeight / _previewSize.Height, (float)viewWidth / _previewSize.Width);
                matrix.PostScale(scale, scale, centerX, centerY);
                matrix.PostRotate(90 * (rotation - 2), centerX, centerY);
            }
            else if ((int)SurfaceOrientation.Rotation180 == rotation)
            {
                matrix.PostRotate(180, centerX, centerY);
            }
            _textureView.SetTransform(matrix);
        }

        private void SetTextureViewSize()
        {
            var previewWidth = _previewSize.Height;
            var previewHeight = _previewSize.Width;

            double wh = (double)previewWidth / previewHeight;
            double hw = (double)previewHeight / previewWidth;

            if (previewHeight < _textureView.Height && previewWidth < _textureView.Width)
            {
                if ((previewHeight / _textureView.Height) >= (previewWidth / _textureView.Width))
                {
                    _realSurfaceSize = new Size((int)(_textureView.Height / hw), _textureView.Height);
                }
                else
                {
                    _realSurfaceSize = new Size(_textureView.Width, (int)(_textureView.Width / wh));
                }
            }
            else
            {
                if (( _textureView.Height / previewHeight) >= (_textureView.Width / previewWidth))
                {
                    _realSurfaceSize = new Size((int)(_textureView.Height / hw), _textureView.Height);
                }
                else
                {
                    _realSurfaceSize = new Size(_textureView.Width, (int)(_textureView.Width / wh));
                }
            }

        }

        private async Task MainInit(SurfaceTexture surface)
        {
            _camera = Camera.Open();

            if (_camera == null)
                _camera = Camera.Open(0);

            var cameraParams = _camera.GetParameters();
            cameraParams.FocusMode = Camera.Parameters.FocusModeContinuousPicture;
            _camera.SetParameters(cameraParams);

            _previewSize = _camera.GetParameters().PreviewSize;

            //SetUpCameraOutputs(_textureView.Width, _textureView.Height);
            //ConfigureTransform(_textureView.Width, _textureView.Height);

            SetTextureViewSize();

            _camera.SetPreviewTexture(surface);
            _camera.StartPreview();

            _textureView.Rotation = 90.0f;
            _overlayView.Rotation = 90.0f;

            RunOnUiThread(() =>
            {
                _textureView.LayoutParameters =
                    new FrameLayout.LayoutParams(_realSurfaceSize.Height, _realSurfaceSize.Width,
                        GravityFlags.CenterVertical | GravityFlags.CenterHorizontal);

                _overlayView.LayoutParameters =
                    new FrameLayout.LayoutParams(_realSurfaceSize.Height, _realSurfaceSize.Width,
                        GravityFlags.CenterVertical | GravityFlags.CenterHorizontal);
            });


            await _recognizerService.Init(ApplicationContext, new Size(_realSurfaceSize.Height , _realSurfaceSize.Width));
            _recognizerService.OverlayRectUpdated += RecognizerServiceOverlayRectUpdated;
            _recognizerService.CroppedImageUpdated += RecognizerServiceCroppedImageUpdated;
            _recognizerService.RecordWasFound += RecognizerServiceRecordWasFound;
        }

        private void RecognizerServiceRecordWasFound(object sender, string result)
        {
            RunOnUiThread(() =>
            {
                _rerunButton.Visibility = ViewStates.Visible;
                _overlayView.Rect = new Rect();
                _overlayView.ForceLayout();
                _textView.Text = result;
            });
            
        }

        private void RecognizerServiceCroppedImageUpdated(object sender, Bitmap bitmap)
        {
            RunOnUiThread(() => _imageView.SetImageBitmap(bitmap));
        }

        public bool OnSurfaceTextureDestroyed(SurfaceTexture surface)
        {
            _camera.StopPreview();
            _camera.Release();

            return true;
        }

        public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height)
        {
            // camera takes care of this
        }

        public async void OnSurfaceTextureUpdated(SurfaceTexture surface)
        {
            if (_recognizerService.IsInitialized && !_recognizerService.SearchComplete && !_recognizerService.RecognizingTextInProgress)
            {
                _recognizerService.RecognizingTextInProgress = true;
                await Task.Factory.StartNew(RecognizeText);
            }
        }

        private async Task RecognizeText()
        {
            var updatedBitmap = _textureView.GetBitmap(_textureView.Bitmap.Width, _textureView.Bitmap.Height);

            var result = await _recognizerService.RecognizeText(updatedBitmap);

            _textView.Text = $"{CommonResources.common_quality}: {result.Quality}%";
            _overlayView.ForceLayout();
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.toolbar_menu, menu);
            return base.OnCreateOptionsMenu(menu);
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            switch (item.ItemId)
            {
                case Resource.Id.menu_client:
                    SupportActionBar.Title = CommonResources.on_client;
                    Toast.MakeText(this, CommonResources.on_client, ToastLength.Short).Show();
                    _recognizerService.SetRecognizingActor(RecognizingActor.Client);

                    break;
                case Resource.Id.menu_server:
                    SupportActionBar.Title = CommonResources.on_server;
                    Toast.MakeText(this, CommonResources.on_server, ToastLength.Short).Show();
                    _recognizerService.SetRecognizingActor(RecognizingActor.Server);
                    break;
            }

            return base.OnOptionsItemSelected(item);
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            RequestedOrientation = ScreenOrientation.Portrait;

            _recognizerService = new RecognizerService();

            // Set our view from the "main" layout resource

            SetContentView(Resource.Layout.activity_main);

            if (GeneralSettings.ShowCroppedImage)
            {
                _imageView = FindViewById<ImageView>(Resource.Id.image_view);
                _imageView.Visibility = ViewStates.Visible;
            }

            _textView = FindViewById<TextView>(Resource.Id.text_view);
            _rerunButton = FindViewById<Button>(Resource.Id.rerun_button);
            _rerunButton.Text = CommonResources.rerun;
            _rerunButton.Click += (obj, e) =>
            {
                _rerunButton.Visibility = ViewStates.Invisible;
                _recognizerService.SearchComplete = false;
                _textView.Text = string.Empty;
            };

            _toolbar = FindViewById<Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(_toolbar);
            SupportActionBar.Title = CommonResources.on_client;

            _textureView = FindViewById<TextureView>(Resource.Id.surface_view);
            _overlayView = FindViewById<OverlayView>(Resource.Id.overlay_view);
            _overlayView.BringToFront();

            _textureView.SurfaceTextureListener = this;

        }

        private void CheckRights()
        {
            if (ContextCompat.CheckSelfPermission(ApplicationContext, Manifest.Permission.Camera) != Permission.Granted)
            {
                _rightCheckInProgress = true;
                ActivityCompat.RequestPermissions(this, new[] { Manifest.Permission.Camera },
                    REQUEST_CAMERA_ID);
                return;
            }

            if (ContextCompat.CheckSelfPermission(ApplicationContext, Manifest.Permission.WriteExternalStorage) !=
                Permission.Granted)
            {
                _rightCheckInProgress = true;
                ActivityCompat.RequestPermissions(this, new[] { Manifest.Permission.WriteExternalStorage },
                    REQUEST_WRITE_ID);
                return;
            }

            if (ContextCompat.CheckSelfPermission(ApplicationContext, Manifest.Permission.Internet) !=
                Permission.Granted)
            {
                _rightCheckInProgress = true;
                ActivityCompat.RequestPermissions(this, new[] { Manifest.Permission.Internet },
                    REQUEST_INTERNET_ID);
                return;
            }

            MainInit(_surface);
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            switch (requestCode)
            {
                case REQUEST_CAMERA_ID:
                    if (permissions.Length == 0 || grantResults.Any(t => t != Permission.Granted))
                    {
                        ErrorDialog.NewInstance(CommonResources.camera_request_permission)
                            .Show(this.FragmentManager, "dialog");
                    }
                    break;

                case REQUEST_WRITE_ID:
                    if (permissions.Length == 0 || grantResults.Any(t => t != Permission.Granted))
                    {
                        ErrorDialog.NewInstance(CommonResources.write_request_permission)
                            .Show(this.FragmentManager, "dialog");
                    }
                    break;

                case REQUEST_INTERNET_ID:
                    if (permissions.Length == 0 || grantResults.Any(t => t != Permission.Granted))
                    {
                        ErrorDialog.NewInstance(CommonResources.internet_request_permission)
                            .Show(this.FragmentManager, "dialog");
                    }
                    break;
            }

            CheckRights();

        }

        private void RecognizerServiceOverlayRectUpdated(object sender, Rect rect)
        {
            _overlayView.Rect = rect;
        }
    }
}