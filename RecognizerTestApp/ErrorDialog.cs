
using Android.App;
using Android.Content;
using Android.OS;


namespace RecognizerTestApp
{
    public class ErrorDialog : DialogFragment
    {
        private static readonly string ARG_MESSAGE = "message";
        private static Activity _mActivity;

        private class PositiveListener : Java.Lang.Object, IDialogInterfaceOnClickListener
        {
            public void OnClick(IDialogInterface dialog, int which)
            {
                _mActivity.Finish();
            }
        }

        public static ErrorDialog NewInstance(string message)
        {
            var args = new Bundle();
            args.PutString(ARG_MESSAGE, message);
            return new ErrorDialog { Arguments = args };
        }

        public override Dialog OnCreateDialog(Bundle savedInstanceState)
        {
            _mActivity = Activity;
            return new AlertDialog.Builder(_mActivity)
                .SetMessage(Arguments.GetString(ARG_MESSAGE))
                .SetPositiveButton(Android.Resource.String.Ok, new PositiveListener())
                .Create();
        }
    }
}