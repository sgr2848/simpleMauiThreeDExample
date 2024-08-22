using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Maui.Controls;
using UIKit;

namespace MauiMe;

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
    private Label warningLabel;
    private Dictionary<string, string> modelPaths = new Dictionary<string, string>();

    private List<string> colorOptions = new List<string>
    {
        "#FF0000", // Red
        "#00FF00", // Green
        "#0000FF", // Blue
        "#FFFF00", // Yellow
        "#FF00FF", // Magenta
        "#00FFFF", // Cyan
        "#FFA500", // Orange
        "#800080", // Purple
        "#A52A2A", // Brown
    };

    public MainPage()
    {
        InitializeComponent();
        InitializeAsync();
    }

    private async void InitializeAsync()
    {
        try
        {
            CreateUI();
            ConfigureWebView();
            await LoadWebViewContent();
            VerifyFilesInCache();
        }
        catch (Exception ex)
        {
            LogError("Error in initialization", ex);
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

        warningLabel = new Label
        {
            Text = string.Empty,
            TextColor = Colors.Red,
            IsVisible = false
        };
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

        for (int i = 0; i < modelInfo.Count; i++)
        {
            int index = i;
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

    private void ConfigureWebView()
    {
        Microsoft.Maui.Handlers.WebViewHandler.Mapper.AppendToMapping("MyCustomization", (handler, view) =>
        {
#if ANDROID
            handler.PlatformView.Settings.JavaScriptEnabled = true;
            handler.PlatformView.Settings.AllowFileAccess = true;
            handler.PlatformView.Settings.AllowFileAccessFromFileURLs = true;
            handler.PlatformView.Settings.AllowUniversalAccessFromFileURLs = true;
#elif IOS
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var wkWebView = handler.PlatformView as WebKit.WKWebView;
                if (wkWebView != null)
                {
                    var config = wkWebView.Configuration;
                    if (config != null)
                    {
                        if (UIDevice.CurrentDevice.CheckSystemVersion(10, 0))
                        {
                            config.Preferences.SetValueForKey(Foundation.NSObject.FromObject(true), new Foundation.NSString("allowFileAccessFromFileURLs"));
                        }
                        config.AllowsInlineMediaPlayback = true;
                        config.MediaTypesRequiringUserActionForPlayback = WebKit.WKAudiovisualMediaTypes.None;
                    }
                }
            });
#endif
        });
    }
    private void VerifyFilesInCache()
    {
        Debug.WriteLine("Verifying files in cache:");
        foreach (var (_, file, _) in modelInfo)
        {
            string filePath = Path.Combine(FileSystem.CacheDirectory, file);
            if (File.Exists(filePath))
            {
                Debug.WriteLine($"File exists: {filePath}");
                try
                {
                    using (var stream = File.OpenRead(filePath))
                    {
                        Debug.WriteLine($"File is readable: {filePath}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error reading file {filePath}: {ex.Message}");
                }
            }
            else
            {
                Debug.WriteLine($"File does not exist: {filePath}");
            }
        }
    }
    private async Task LoadWebViewContent()
    {
        try
        {
            Debug.WriteLine("LoadWebViewContent: Starting");

            Debug.WriteLine("LoadWebViewContent: Loading HTML content");
            string htmlContent = await ResourceLoader.LoadResourceContentAsync("updated.html");
            Debug.WriteLine($"LoadWebViewContent: HTML content loaded, length: {htmlContent?.Length ?? 0}");

            Debug.WriteLine("LoadWebViewContent: Loading JS content");
            string jsContent = await ResourceLoader.LoadResourceContentAsync("three.min.js");
            Debug.WriteLine($"LoadWebViewContent: JS content loaded, length: {jsContent?.Length ?? 0}");

            Debug.WriteLine("LoadWebViewContent: Loading GLTF content");
            string gltfContent = await ResourceLoader.LoadResourceContentAsync("GLTFLoader.js");
            Debug.WriteLine($"LoadWebViewContent: GLTF content loaded, length: {gltfContent?.Length ?? 0}");

            Debug.WriteLine("LoadWebViewContent: Replacing placeholders in HTML");
            htmlContent = htmlContent.Replace("<!-- THREE_JS_PLACEHOLDER -->", $"<script>{jsContent}</script>");
            htmlContent = htmlContent.Replace("<!-- GLTF_LOADER_PLACEHOLDER -->", $"<script>{gltfContent}</script>");

            //Debug.WriteLine("LoadWebViewContent: Processing model files");
            //foreach (var (name, file, _) in modelInfo)
            //{
            //    string localFilePath = await CopyModelToLocalStorage(file);
            //    string customUrl = $"modelfile://{file}";
            //    htmlContent = htmlContent.Replace($"\"{file}\"", $"\"{customUrl}\"");
            //    Debug.WriteLine($"LoadWebViewContent: Replaced {file} with {customUrl} in HTML content");

            //}
            var modelDataScript = new StringBuilder();
            modelDataScript.AppendLine("<script>");
            modelDataScript.AppendLine("const modelData = {");

            foreach (var (name, file, _) in modelInfo)
            {
                string localFilePath = await CopyModelToLocalStorage(file);
                string filePath = Path.Combine(FileSystem.CacheDirectory, file);
                if (File.Exists(filePath))
                {
                    byte[] fileBytes = File.ReadAllBytes(filePath);
                    string base64 = Convert.ToBase64String(fileBytes);
                    modelDataScript.AppendLine($"  '{name}': 'data:application/octet-stream;base64,{base64}',");
                }
                else
                {
                    Debug.WriteLine($"File not found: {filePath}");
                }
            }

            modelDataScript.AppendLine("};");
            modelDataScript.AppendLine("</script>");

            htmlContent = htmlContent.Replace("<!-- MODEL_DATA_PLACEHOLDER -->", modelDataScript.ToString());

            Debug.WriteLine("LoadWebViewContent: Setting WebView source");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Debug.WriteLine($"LoadWebViewContent: CacheDirectory: {FileSystem.CacheDirectory}");
                webView.Source = new HtmlWebViewSource { Html = htmlContent, BaseUrl = FileSystem.CacheDirectory };
                Debug.WriteLine("LoadWebViewContent: WebView source set");
            });

            Debug.WriteLine("LoadWebViewContent: Completed successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoadWebViewContent: Error occurred - {ex.GetType().Name}");
            LogError("Error in LoadWebViewContent", ex);
        }
    }

    private async Task<string> CopyModelToLocalStorage(string modelFile)
    {
        if (modelPaths.TryGetValue(modelFile, out string cachedPath))
        {
            Debug.WriteLine($"CopyModelToLocalStorage: Returning cached path for {modelFile}: {cachedPath}");
            return cachedPath;
        }

        try
        {
            var assembly = typeof(MainPage).GetTypeInfo().Assembly;
            using (Stream modelStream = assembly.GetManifestResourceStream($"MauiMe.Resources.Raw.{modelFile}"))
            {
                if (modelStream == null)
                {
                    throw new InvalidOperationException($"Could not find {modelFile} resource");
                }

                string localFilePath = Path.Combine(FileSystem.CacheDirectory, modelFile);
                Directory.CreateDirectory(Path.GetDirectoryName(localFilePath));

                using (var fileStream = File.Create(localFilePath))
                {
                    await modelStream.CopyToAsync(fileStream);
                }
                Debug.WriteLine($"File copied to {localFilePath}");

                if (File.Exists(localFilePath))
                {
                    var fileInfo = new FileInfo(localFilePath);
                    Debug.WriteLine($"File exists at {localFilePath}. Size: {fileInfo.Length} bytes");
                }
                else
                {
                    Debug.WriteLine($"WARNING: File does not exist at {localFilePath} after copying");
                }

                modelPaths[modelFile] = localFilePath;
                return localFilePath;
            }
        }
        catch (Exception ex)
        {
            LogError($"Error copying model {modelFile}", ex);
            throw;
        }
    }

    private async void UpdateRotation(double pitch, double yaw, double roll)
    {
        try
        {
            string script = $"updateRotation({pitch}, {yaw}, {roll});";
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                string result = await webView.EvaluateJavaScriptAsync(script);
                Debug.WriteLine($"UpdateRotation result: {result}");
                CheckModelOrientation(pitch, yaw, roll);
            });
        }
        catch (Exception ex)
        {
            LogError("Error in UpdateRotation", ex);
        }
    }
    private void CheckModelOrientation(double pitch, double yaw, double roll)
    {
        const double threshold = 1.57; // Approximately 90 degrees in radians

        bool isSideways = Math.Abs(pitch) > threshold || Math.Abs(roll) > threshold;
        bool isUpsideDown = yaw > threshold || yaw < -threshold;

        if (isSideways || isUpsideDown)
        {
            warningLabel.Text = "Warning: The model is sideways or upside down!";
            warningLabel.IsVisible = true;
        }
        else
        {
            warningLabel.IsVisible = false;
        }
    }
    private async void UpdateModelColor(int modelIndex, string colorHex)
    {
        try
        {
            string script = $"updateModelColor({modelIndex}, 0x{colorHex.Substring(1)});";
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                string result = await webView.EvaluateJavaScriptAsync(script);
                Debug.WriteLine($"UpdateModelColor result: {result}");
            });
        }
        catch (Exception ex)
        {
            LogError("Error in UpdateModelColor", ex);
        }
    }

    private void LogError(string message, Exception ex)
    {
        Debug.WriteLine($"ERROR: {message}");
        Debug.WriteLine($"Exception: {ex.GetType().Name}");
        Debug.WriteLine($"Message: {ex.Message}");
        Debug.WriteLine($"StackTrace: {ex.StackTrace}");

        if (ex.InnerException != null)
        {
            Debug.WriteLine("Inner Exception:");
            LogError("Inner Exception", ex.InnerException);
        }

        // Consider implementing a more robust logging mechanism or crash reporting service here
    }
}

public class ResourceLoader
{
    private static Dictionary<string, string> cachedResources = new Dictionary<string, string>();

    public static async Task<string> LoadResourceContentAsync(string resourceName)
    {
        if (cachedResources.TryGetValue(resourceName, out string cachedContent))
        {
            return cachedContent;
        }

        var assembly = typeof(MainPage).GetTypeInfo().Assembly;
        var fullResourceName = $"{assembly.GetName().Name}.Resources.Raw.{resourceName}";

        using (var stream = assembly.GetManifestResourceStream(fullResourceName))
        {
            if (stream == null)
            {
                throw new InvalidOperationException($"Could not find resource: {fullResourceName}");
            }

            using (var reader = new StreamReader(stream))
            {
                var content = await reader.ReadToEndAsync();
                cachedResources[resourceName] = content;
                return content;
            }
        }
    }
}