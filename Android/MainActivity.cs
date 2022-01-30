using System;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using AndroidX.AppCompat.App;
using Android.Webkit;
using Android.Content.PM;

namespace AnyChallenge
{
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = Config.AnyChallengeUrlProtocol,
        DataHost = Config.AnyChallengeUrlHost,
        AutoVerify = true)]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = Config.AnyChallengeUrlProtocol,
        DataHost = Config.AnyChallengeUrlHost,
        DataPathPattern = "/.*",
        AutoVerify = true)]
    [Activity(
        Label = "@string/app_name",
        Theme = "@style/AppTheme.NoActionBar",
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
    public class MainActivity : AppCompatActivity
    {
        private static bool _firstLoad = true;
        private static string _lastIntentUrl = null;
        private WebView _webView = null;
        
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            var url = Config.AnyChallengeUrl;
            if (Intent?.Action == Intent.ActionView && Intent.DataString.StartsWith(url))
            {
                // protection agains using the same code multiple times because after the authentication
                // the intent will stay the same no matter how many times the Activity is revreated due to rotation, etc.
                if (!string.Equals(_lastIntentUrl, Intent.DataString, StringComparison.OrdinalIgnoreCase))
                {
                    url = Intent.DataString;
                    _lastIntentUrl = url;
                }
            }

            _webView = FindViewById<WebView>(Resource.Id.webview);
            if (_webView != null)
            {
                _webView.Settings.JavaScriptEnabled = true;
                _webView.Settings.DomStorageEnabled = true;
                _webView.Settings.CacheMode = CacheModes.Default;
                _webView.Settings.SetSupportMultipleWindows(support: false);
                _webView.Settings.UserAgentString = _webView.Settings.UserAgentString.Replace("; wv", "; wv; AnyChallenge");

                _webView.SetWebViewClient(new WebViewClientWithProgress(this));

                if (_firstLoad)
                {
                    _webView.ClearCache(includeDiskFiles: true);
                    _firstLoad = false;
                }

                if (savedInstanceState == null)
                {
                    _webView.LoadUrl(url);
                }
            }
        }

        public override void OnBackPressed()
        {
            if (_webView?.CanGoBack() == true)
            {
                _webView.GoBack();
            }
            else
            {
                base.OnBackPressed();
                FinishAffinity();
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        protected override void OnSaveInstanceState(Bundle outState)
        {
            base.OnSaveInstanceState(outState);
            _webView.SaveState(outState);
        }

        protected override void OnRestoreInstanceState(Bundle savedInstanceState)
        {
            base.OnRestoreInstanceState(savedInstanceState);
            _webView.RestoreState(savedInstanceState);
        }
    }
}
