using Microsoft.Maui.Controls;
using System;
using System.IO;
using System.Diagnostics;
using System.Reflection;

namespace MauiMe
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
            try
            {
                CreateUI();
                Microsoft.Maui.Handlers.WebViewHandler.Mapper.AppendToMapping("MyCustomization", (handler, view) =>
                {
#if ANDROID
                                handler.PlatformView.Settings.JavaScriptEnabled = true;
                                handler.PlatformView.Settings.AllowFileAccess = true;
                                handler.PlatformView.Settings.AllowFileAccessFromFileURLs = true;
                                handler.PlatformView.Settings.AllowUniversalAccessFromFileURLs = true;

#elif IOS
#endif
                });
                LoadWebViewContent();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in MainPage constructor: {ex.Message}");
            }
        }

        private void CreateUI()
        {
            var pitchSlider = new Slider { Minimum = -3.14, Maximum = 3.14, Value = 0 };
            var yawSlider = new Slider { Minimum = -3.14, Maximum = 3.14, Value = 0 };
            var rollSlider = new Slider { Minimum = -3.14, Maximum = 3.14, Value = 0 };

            pitchSlider.ValueChanged += (s, e) => UpdateRotation(e.NewValue, yawSlider.Value, rollSlider.Value);
            yawSlider.ValueChanged += (s, e) => UpdateRotation(pitchSlider.Value, e.NewValue, rollSlider.Value);
            rollSlider.ValueChanged += (s, e) => UpdateRotation(pitchSlider.Value, yawSlider.Value, e.NewValue);

            var layout = new StackLayout
            {
                Children =
                {
                    new Label { Text = "Pitch" },
                    pitchSlider,
                    new Label { Text = "Yaw" },
                    yawSlider,
                    new Label { Text = "Roll" },
                    rollSlider,
                    webView
                }
            };

            Content = layout;
        }

        private async void LoadWebViewContent()
        {
            try
            {
                var assembly = typeof(MainPage).GetTypeInfo().Assembly;

                // Load HTML content
                Stream htmlStream = assembly.GetManifestResourceStream("MauiMe.Resources.Raw.index.html");
                if (htmlStream == null) throw new InvalidOperationException("Could not find index.html resource");

                string htmlContent;
                using (var reader = new StreamReader(htmlStream))
                {
                    htmlContent = reader.ReadToEnd();
                }
                // Load Three.js script content
                Stream jsStream = assembly.GetManifestResourceStream("MauiMe.Resources.Raw.three.min.js");
                if (jsStream == null) throw new InvalidOperationException("Could not find three.min.js resource");

                string jsContent;
                using (var reader = new StreamReader(jsStream))
                {
                    jsContent = reader.ReadToEnd();
                }

                // Load GLTFLoader.js content
                Stream gltfStream = assembly.GetManifestResourceStream("MauiMe.Resources.Raw.GLTFLoader.js");
                if (gltfStream == null) throw new InvalidOperationException("Could not find GLTFLoader.js resource");

                string gltfContent;
                using (var reader = new StreamReader(gltfStream))
                {
                    gltfContent = reader.ReadToEnd();
                }
                // Inject the Three.js script and GLTFLoader script directly into the HTML
                htmlContent = htmlContent.Replace("<!-- THREE_JS_PLACEHOLDER -->", $"<script>{jsContent}</script>");
                htmlContent = htmlContent.Replace("<!-- GLTF_LOADER_PLACEHOLDER -->", $"<script>{gltfContent}</script>");
                Stream modelStream = assembly.GetManifestResourceStream("MauiMe.Resources.Raw.random.glb");
                if (modelStream == null) throw new InvalidOperationException("Could not find random.glb resource");
                string localFilePath = Path.Combine(FileSystem.CacheDirectory, "model.glb");
                using (var fileStream = File.Create(localFilePath))
                {
                    await modelStream.CopyToAsync(fileStream);
                }
                htmlContent = htmlContent.Replace("random.glb", $"file://{localFilePath}");
                // Set the HTML content to the WebView
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    webView.Source = new HtmlWebViewSource { Html = htmlContent };
                });
            }
            catch (Exception ex)
            {
                LogError("Error in LoadWebViewContent", ex);
            }
        }

        private async void UpdateRotation(double pitch, double yaw, double roll)
        {
            try
            {
                string script = $"updateRotation({pitch}, {yaw}, {roll});";
                string result = await webView.EvaluateJavaScriptAsync(script);
                Debug.WriteLine($"UpdateRotation result: {result}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in UpdateRotation: {ex.Message}");
            }
        }
        private void LogError(string message, Exception ex)
        {
            Debug.WriteLine($"{message}: {ex.Message}");
            Debug.WriteLine($"StackTrace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                Debug.WriteLine($"Inner StackTrace: {ex.InnerException.StackTrace}");
            }
        }
    }
}
