using Microsoft.Maui.Controls;
using System;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Collections.Generic;

namespace MauiMe
{
    public partial class MainPage : ContentPage
    {
        private List<(string Name, string File, string Color)> modelInfo = new List<(string, string, string)>
        {
            ("Jacket Top", "jackettop.glb", "#FFD700"),
            ("Jacket Bottom", "jacketbottom.glb", "#FFD700"),
            ("Button", "button.glb", "#5C5CFF"),
            ("Bottom", "bottom.glb", "#FFD700"),
            ("USB", "usb.glb", "#B5C0C9"),
            ("Mouthpiece", "mouthpiece.glb", "#B5C0C9"),
            ("Top", "top.glb", "#B5C0C9")
        };
        private Dictionary<string, string> modelPaths = new Dictionary<string, string>();

        private List<string> colorOptions = new List<string>
        {
            "#FF0000", // Red
            "#00FF00", // Green
            "#0000FF", // Blue
            "#FFFF00", // Yellow
            "#FF00FF", // Magenta
            "#00FFFF", // Cyan
            "#FFA500",
            "#800080",
            "#A52A2A",
        };

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
            webView = new WebView { HeightRequest = 300 };
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
                    webView,
                    new Label { Text = "Pitch" },
                    pitchSlider,
                    new Label { Text = "Yaw" },
                    yawSlider,
                    new Label { Text = "Roll" },
                    rollSlider
                }
            };

            // Add color dropdowns for each model
            for (int i = 0; i < modelInfo.Count; i++)
            {
                int index = i;  // Capture the index for use in lambda
                var (name, _, _) = modelInfo[i];

                var colorPicker = new Picker { Title = $"Color for {name}" };
                colorPicker.ItemsSource = colorOptions;
                colorPicker.SelectedIndexChanged += (s, e) =>
                {
                    if (colorPicker.SelectedIndex != -1)
                    {
                        UpdateModelColor(index, colorOptions[colorPicker.SelectedIndex]);
                    }
                };
                layout.Children.Add(new Label { Text = $"Color for {name}" });
                layout.Children.Add(colorPicker);
            }

            Content = new ScrollView { Content = layout };
        }

        private async void LoadWebViewContent()
        {
            try
            {
                var assembly = typeof(MainPage).GetTypeInfo().Assembly;

                // Load HTML content
                Stream htmlStream = assembly.GetManifestResourceStream("MauiMe.Resources.Raw.updated.html");
                if (htmlStream == null) throw new InvalidOperationException("Could not find index.html resource");

                string htmlContent;
                using (var reader = new StreamReader(htmlStream))
                {
                    htmlContent = await reader.ReadToEndAsync();
                }

                // Load Three.js and GLTFLoader.js content
                string jsContent = await LoadResourceContent(assembly, "MauiMe.Resources.Raw.three.min.js");
                string gltfContent = await LoadResourceContent(assembly, "MauiMe.Resources.Raw.GLTFLoader.js");

                // Inject the scripts directly into the HTML
                htmlContent = htmlContent.Replace("<!-- THREE_JS_PLACEHOLDER -->", $"<script>{jsContent}</script>");
                htmlContent = htmlContent.Replace("<!-- GLTF_LOADER_PLACEHOLDER -->", $"<script>{gltfContent}</script>");

                // Replace model file paths
                for (int i = 0; i < modelInfo.Count; i++)
                {
                    var (_, file, _) = modelInfo[i];
                    string localFilePath = await CopyModelToLocalStorage(file);
                    htmlContent = htmlContent.Replace($"\"{file}\"", $"\"{localFilePath}\"");
                }


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

        private async Task<string> LoadResourceContent(Assembly assembly, string resourceName)
        {
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null) throw new InvalidOperationException($"Could not find {resourceName} resource");
                using (var reader = new StreamReader(stream))
                {
                    return await reader.ReadToEndAsync();
                }
            }
        }

        private async Task<string> CopyModelToLocalStorage(string modelFile)
        {
            if (!modelPaths.ContainsKey(modelFile))
            {
                var assembly = typeof(MainPage).GetTypeInfo().Assembly;
                Stream modelStream = assembly.GetManifestResourceStream($"MauiMe.Resources.Raw.{modelFile}");
                if (modelStream == null) throw new InvalidOperationException($"Could not find {modelFile} resource");

                string localFilePath = Path.Combine(FileSystem.CacheDirectory, modelFile);
                Directory.CreateDirectory(Path.GetDirectoryName(localFilePath));
                using (var fileStream = File.Create(localFilePath))
                {
                    await modelStream.CopyToAsync(fileStream);
                }
                modelPaths[modelFile] = $"file://{localFilePath}";
            }
            return modelPaths[modelFile];
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

        private async void UpdateModelColor(int modelIndex, string colorHex)
        {
            try
            {
                string script = $"updateModelColor({modelIndex}, 0x{colorHex.Substring(1)});";
                string result = await webView.EvaluateJavaScriptAsync(script);
                Debug.WriteLine($"UpdateModelColor result: {result}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in UpdateModelColor: {ex.Message}");
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