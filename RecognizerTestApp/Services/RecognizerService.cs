using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Android.Content;
using Android.Graphics;
using Android.Util;
using Firebase;
using Firebase.ML.Vision;
using Firebase.ML.Vision.Common;
using Firebase.ML.Vision.Text;
using Recognizer.Core;
using Tesseract.Droid;
using Console = System.Console;
using Exception = System.Exception;
using File = Java.IO.File;

namespace RecognizerTestApp.Services
{
    public class RecognizerService
    {

        private string[] _referenceStrings = new[]
        {
            "рного тел ется, что и помощи  й можно п т в четыр ли овладе".Replace(" ", "").ToLower(),
            "печатник создал форм шрифтов распечатки обр успешно переж веков, но и пере".Replace(" ", "").ToLower()
        };

        private IList<Com.Arview.Aurecognizerlibrary.HueRange> _referenceHues = new List<Com.Arview.Aurecognizerlibrary.HueRange>{
            new Com.Arview.Aurecognizerlibrary.HueRange(){HueLow  = 180, HueHigh = 274}, //blue
            new Com.Arview.Aurecognizerlibrary.HueRange(){HueLow  = 185, HueHigh = 195}//violet
        };

     
        private Context _appContext;
        private string _currentReferenceString;
        public const int RECOGNITION_VALUE = 80;
        private readonly RecognitionResult _recognitionResult = new RecognitionResult();

        private FirebaseApp _defaultFirebaseApp;
        private ProcessImageListener _firebaseProcessImageListener;
        private FirebaseVisionTextRecognizer _firebaseVisionTextDetector;
        private RecognizingActor _recognizingActor;
        private Android.Util.Size _recognizeAreaSize = new Android.Util.Size(0,0);
        private AssetsDeployment _assetsDeployment;

        private readonly Com.Arview.Aurecognizerlibrary.AuCodeLibrary _auCodeLibrary = new Com.Arview.Aurecognizerlibrary.AuCodeLibrary();

        public volatile bool IsInitialized;
        public volatile bool SearchComplete;

        public volatile bool RecognizingTextInProgress;

        public void StartRecognizingText()
        {
            RecognizingTextInProgress = true;
        }

        public void StopRecognizingText()
        {
            RecognizingTextInProgress = false;
        }

        private void InitFirebase()
        {
            try
            {
                _defaultFirebaseApp = FirebaseApp.InitializeApp(_appContext);

                _firebaseProcessImageListener = new ProcessImageListener();

                var options = new FirebaseVisionCloudTextRecognizerOptions.Builder()
                    .SetLanguageHints(new List<string> {"ru"})
                    .SetModelType(FirebaseVisionCloudTextRecognizerOptions.DenseModel)
                    .Build();

                var firebaseVision = FirebaseVision.GetInstance(_defaultFirebaseApp);

                _firebaseVisionTextDetector =
                    firebaseVision.GetCloudTextRecognizer(options);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private async Task<string> CopyAssets()
        {
            try
            {
                var assetManager = _appContext.Assets;
                var files = assetManager.List("tessdata");
                var file = _appContext.GetExternalFilesDir(null);
                var tessdata = new File(_appContext.GetExternalFilesDir(null), "tessdata");
                if (!tessdata.Exists())
                {
                    tessdata.Mkdir();
                }
                else if (_assetsDeployment == AssetsDeployment.OncePerVersion)
                {
                    var packageInfo = _appContext.PackageManager.GetPackageInfo(_appContext.PackageName, 0);
                    var version = packageInfo.VersionName;
                    var versionFile = new File(tessdata, "version");
                    if (versionFile.Exists())
                    {
                        var fileVersion = System.IO.File.ReadAllText(versionFile.AbsolutePath);
                        if (version == fileVersion)
                        {
                            Log.Debug("TesseractApi", "Application version didn't change, skipping copying assets");
                            return file.AbsolutePath;
                        }
                        versionFile.Delete();
                    }
                    System.IO.File.WriteAllText(versionFile.AbsolutePath, version);
                }

                Log.Debug("TesseractApi", "Copy assets to " + file.AbsolutePath);

                foreach (var filename in files)
                {
                    using (var inStream = assetManager.Open("tessdata/" + filename))
                    {
                        var outFile = new File(tessdata, filename);
                        if (outFile.Exists())
                        {
                            outFile.Delete();
                        }
                        using (var outStream = new FileStream(outFile.AbsolutePath, FileMode.Create))
                        {
                            await inStream.CopyToAsync(outStream);
                            await outStream.FlushAsync();
                        }
                    }
                }
                return file.AbsolutePath;
            }
            catch (Exception ex)
            {
                Log.Error("TesseractApi", ex.Message);
            }
            return null;
        }

        public async Task Init(Context appContext)
        {
            _appContext = appContext ?? throw new ArgumentNullException(nameof(appContext));
            var path = await CopyAssets();

            File directory = new File(path);
            File[] files = directory.ListFiles();

            await Task.Run(() =>
            {
                //InitFirebase();

                try
                {
                    IsInitialized = _auCodeLibrary.Init(path, "rus");
                    IsInitialized = true;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
              

                
            });

        }

        private void SetRecognitionResultFromText(string originalText)
        {
            try
            {
                double quality = 0;

                var text = originalText.Replace("\n", "").Replace(" ", "").ToLower();

                if (text.Length > 0)
                {
                    foreach (string str in _referenceStrings)
                    {
                        double diff = StringDistance.GetDamerauLevenshteinDistance(str, text);
                        var currQuality = (str.Length - diff) / str.Length * 100;

                        if (currQuality > quality)
                        {
                            _currentReferenceString = str;
                            quality = currQuality;
                        }
                    }
                }

                _recognitionResult.OriginalText = originalText;
                _recognitionResult.ResultText = text;
                _recognitionResult.Quality = quality;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public async Task<RecognitionResult> RecognizeText(Bitmap bitmap, Android.Util.Size size)
        {
            try
            {
                _recognitionResult.Invalidate();

                string text = string.Empty;

                await Task.Run(() => { text = _auCodeLibrary.RecognizeText(bitmap, _referenceHues); });

                SetRecognitionResultFromText(text);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                RecognizingTextInProgress = false;
            }

            return _recognitionResult;
        }
    

        private async Task FirebaseTextRecognizing(Bitmap croppedBitmap)
        {
            var tcs = new TaskCompletionSource<bool>();

            void OnTextRecognized(object sender, string recognizedText)
            {
                _firebaseProcessImageListener.TextRecognized -= OnTextRecognized;

                SetRecognitionResultFromText(recognizedText);

                tcs.SetResult(true);
            }

            _firebaseProcessImageListener.TextRecognized += OnTextRecognized;

            var image = FirebaseVisionImage.FromBitmap(croppedBitmap);

            var task = _firebaseVisionTextDetector.ProcessImage(image);
            task.AddOnSuccessListener(_firebaseProcessImageListener);
            task.AddOnCompleteListener(_firebaseProcessImageListener);
            task.AddOnFailureListener(_firebaseProcessImageListener);

            await tcs.Task;
        }

        

        public void SetRecognizingActor(RecognizingActor actor)
        {
            _recognizingActor = actor;
        }

        public RecognizingActor GetRecognizingActor()
        {
            return _recognizingActor;
        }

        public bool CanBeRecognizedFromServer()
        {
            return _firebaseVisionTextDetector != null;
        }
    }
}