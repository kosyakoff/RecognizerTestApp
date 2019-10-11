using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Android.Content;
using Android.Graphics;
using Firebase;
using Firebase.ML.Vision;
using Firebase.ML.Vision.Common;
using Firebase.ML.Vision.Text;
using Recognizer.Android.Library;

namespace RecognizerTestApp.Services
{
    public class RecognizerService
    {

        private string[] _referenceStrings = new[]
        {
            "рного тел ется, что и помощи  й можно п т в четыр ли овладе".Replace(" ", "").ToLower(),
            "печатник создал форм шрифтов распечатки обр успешно переж веков, но и пере".Replace(" ", "").ToLower()
        };

        private double[][] _referenceHues = {
            new double[]{180,274}, //blue
            new double[]{185,195} //violet
        };

     
        private Context _appContext;
        private string _currentReferenceString;
        public const int RECOGNITION_VALUE = 80;
        private readonly RecognitionResult _recognitionResult = new RecognitionResult();

        private FirebaseApp _defaultFirebaseApp;
        private ProcessImageListener _firebaseProcessImageListener;
        private FirebaseVisionTextRecognizer _firebaseVisionTextDetector;
        private RecognizingActor _recognizingActor;

        private Rect _textureRect;

        private readonly AuCodeLibrary _auCodeLibrary = new AuCodeLibrary();

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

        public async Task Init(Context appContext)
        {
            _appContext = appContext ?? throw new ArgumentNullException(nameof(appContext));

            InitFirebase();

            await _auCodeLibrary.Init(_appContext);

            IsInitialized = true;
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

        public async Task<RecognitionResult> RecognizeText(Bitmap bitmap)
        {
            try
            {
                _recognitionResult.Invalidate();

                // await Task.Factory.StartNew();
                string text = await _auCodeLibrary.RecognizeText(bitmap, _referenceHues);

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