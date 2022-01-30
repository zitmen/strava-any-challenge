using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using Google.Android.Material.Snackbar;
using System;

namespace AnyChallenge
{
    internal class WebViewClientWithProgress : WebViewClient
    {
        private readonly MainActivity _activity;
        private readonly ProgressBar _progressBar;

        public WebViewClientWithProgress(MainActivity activity)
        {
            _activity = activity;
            _progressBar = activity.FindViewById<ProgressBar>(Resource.Id.progress);
        }

        public override void OnPageStarted(WebView view, string url, Bitmap favicon)
        {
            base.OnPageStarted(view, url, favicon);
            _progressBar.Visibility = ViewStates.Visible;
        }

        public override bool ShouldOverrideUrlLoading(WebView view, IWebResourceRequest request)
        {
            var url = request.Url.ToString();
            if (url.StartsWith("https://www.strava.com/oauth", StringComparison.OrdinalIgnoreCase))
            {
                var intentUri = $"https://www.strava.com/oauth/mobile/authorize?client_id={Config.StravaApiClientId}&redirect_uri={Config.AnyChallengeUrl}/authenticated&response_type=code&approval_prompt=auto&scope=read,activity:read,activity:read_all";
                var intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse(intentUri));
                _activity.StartActivity(intent);

                return true;
            }

            if (url.StartsWith("https://www.strava.com", StringComparison.OrdinalIgnoreCase))
            {
                var intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse(url));
                _activity.StartActivity(intent);

                return true;
            }

            return false;
        }

        public override void OnReceivedError(WebView view, IWebResourceRequest request, WebResourceError error)
        {
            base.OnReceivedError(view, request, error);
            Snackbar.Make(view, $"Error: {error.Description}", Snackbar.LengthLong).Show();
        }

        public override void OnPageFinished(WebView view, string url)
        {
            _progressBar.Visibility = ViewStates.Gone;
            base.OnPageFinished(view, url);
        }
    }
}