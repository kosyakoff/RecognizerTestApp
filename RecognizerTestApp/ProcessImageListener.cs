using System;
using Android.Gms.Tasks;
using Firebase.ML.Vision.Text;
using Exception = Java.Lang.Exception;
using Object = Java.Lang.Object;

namespace RecognizerTestApp
{
    public class ProcessImageListener : Object, IOnSuccessListener,
        IOnCompleteListener, IOnFailureListener
    {
        public EventHandler<string> TextRecognized;

        public void OnComplete(Task task)
        {
        }

        public void OnFailure(Exception e)
        {
            TextRecognized?.Invoke(this, string.Empty);
        }

        public void OnSuccess(Object result)
        {
            var text = (FirebaseVisionText) result;
            TextRecognized?.Invoke(this, text.Text);
        }
    }
}