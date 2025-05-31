using System.Text.Json;
using System.Text.Json.Serialization;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Diagnostics;
using System.Drawing; // Added for image conversion
using System.Drawing.Imaging; // Added for image conversion

namespace NasaApodWallpaper
{
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(ApodData))]
    [JsonSerializable(typeof(Config))]
    internal partial class AppJsonContext : JsonSerializerContext
    {
    }

    class Program
    {
        // Windows API for setting wallpaper
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
        private const int SPI_SETDESKWALLPAPER = 20;
        private const int SPIF_UPDATEINIFILE = 0x01;
        private const int SPIF_SENDCHANGE = 0x02;

        // Configuration
        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NasaApodWallpaper");
        private static readonly string ConfigPath = Path.Combine(AppDataPath, "config.json");
        private static readonly string ImagesPath = Path.Combine(AppDataPath, "Images");
        private static readonly string LogPath = Path.Combine(AppDataPath, "apod.log");

        // Your NASA API key is read from environment variables
        private static readonly string? ApiKey = Environment.GetEnvironmentVariable("NASA_API_KEY");

        // Reuse HTTP client for efficiency
        private static readonly HttpClient httpClient = new HttpClient();

        static async Task Main(string[] args)
        {
            try
            {
                // Early validation
                if (string.IsNullOrWhiteSpace(ApiKey))
                {
                    LogMessage("NASA API key not found. Set NASA_API_KEY environment variable.");
                    return;
                }

                // Handle command line args early
                if (args.Length > 0)
                {
                    switch (args[0].ToLowerInvariant())
                    {
                        case "--schedule":
                            ScheduleTask();
                            return;
                        case "--help":
                            ShowHelp();
                            return;
                        case "--force":
                            // Skip date check and force update
                            break;
                        default:
                            ShowHelp();
                            return;
                    }
                }

                // Ensure directories exist
                Directory.CreateDirectory(AppDataPath);
                Directory.CreateDirectory(ImagesPath);

                LogMessage("NASA APOD Wallpaper Changer started");

                // Load configuration
                var config = await LoadConfigAsync();
                string today = DateTime.Now.ToString("yyyy-MM-dd");

                // Skip if already updated today (unless forced)
                if (config.LastUpdate == today && !args.Contains("--force"))
                {
                    LogMessage($"Already updated today ({today}). Use --force to override.");
                    return;
                }

                // Fetch APOD data
                var apodData = await GetApodDataAsync();
                if (apodData == null)
                {
                    LogMessage("Failed to get APOD data. Exiting.");
                    return;
                }

                // Skip if it's not an image
                if (apodData.MediaType != "image")
                {
                    LogMessage($"Today's APOD is not an image (it's {apodData.MediaType}). Skipping.");
                    return;
                }

                // Ensure apodData.HdUrl and apodData.Url are not null before using them
                string imageUrl = !string.IsNullOrEmpty(apodData?.HdUrl) ? apodData.HdUrl! : apodData?.Url ?? string.Empty;
                if (string.IsNullOrEmpty(imageUrl))
                {
                    LogMessage("No image URL found in APOD data. Exiting.");
                    return;
                }

                // Prepare file path for saving the image
                string fileExtension = Path.GetExtension(imageUrl);
                if (string.IsNullOrEmpty(fileExtension))
                    fileExtension = ".jpg";

                string imagePath = Path.Combine(ImagesPath, $"NASA_APOD_{today}{fileExtension}");

                // Download image
                if (await DownloadImageAsync(imageUrl, imagePath))
                {
                    LogMessage($"Downloaded image: {imagePath}");
                    
                    // Try setting wallpaper directly first (works with modern Windows)
                    LogMessage($"Attempting to set wallpaper using: {imagePath}");
                    bool wallpaperResult = SetWallpaper(imagePath);
                    
                    if (!wallpaperResult)
                    {
                        // Fallback to BMP conversion if direct setting fails
                        LogMessage("Direct wallpaper setting failed, converting to BMP...");
                        string bmpPath = Path.Combine(ImagesPath, $"NASA_APOD_{today}.bmp");
                        if (ConvertToBmp(imagePath, bmpPath))
                        {
                            wallpaperResult = SetWallpaper(bmpPath);
                            if (wallpaperResult)
                            {
                                imagePath = bmpPath; // Update path for config
                            }
                        }
                    }
                    
                    if (wallpaperResult)
                    {
                        // Update config
                        config.LastUpdate = today;
                        config.LastImage = imagePath;
                        await SaveConfigAsync(config);

                        // Log info
                        LogMessage($"Today's APOD: {apodData?.Title ?? "Unknown Title"}");
                        LogMessage($"Wallpaper successfully set: {imagePath}");

                        // Cleanup old images
                        CleanupOldImages();
                    }
                    else
                    {
                        LogMessage("Failed to set wallpaper.");
                    }
                }
                else
                {
                    LogMessage("Failed to download image.");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error: {ex.Message}");
            }
            finally
            {
                httpClient.Dispose();
            }
        }

        private static bool ConvertToBmp(string sourcePath, string bmpPath)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
#pragma warning disable CA1416 // Validate platform compatibility
                    using (var img = Image.FromFile(sourcePath))
                    {
                        img.Save(bmpPath, ImageFormat.Bmp);
                    }
#pragma warning restore CA1416 // Validate platform compatibility
                    LogMessage($"Image converted to BMP: {bmpPath}");
                    return true;
                }
                else
                {
                    LogMessage("Image conversion to BMP is only supported on Windows.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to convert image to BMP: {ex.Message}");
                return false;
            }
        }

        private static void CleanupOldImages()
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-7); // Keep only last 7 days
                var files = Directory.GetFiles(ImagesPath, "NASA_APOD_*.*");
                
                foreach (var file in files)
                {
                    if (File.GetCreationTime(file) < cutoffDate)
                    {
                        File.Delete(file);
                        LogMessage($"Deleted old image: {Path.GetFileName(file)}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error during cleanup: {ex.Message}");
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine("NASA APOD Wallpaper Changer");
            Console.WriteLine("===========================");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  NasaApodWallpaper.exe              - Update wallpaper now");
            Console.WriteLine("  NasaApodWallpaper.exe --schedule   - Schedule daily updates");
            Console.WriteLine("  NasaApodWallpaper.exe --force      - Force update (ignore today check)");
            Console.WriteLine("  NasaApodWallpaper.exe --help       - Show this help message");
            Console.WriteLine();
            Console.WriteLine("This program sets your desktop wallpaper to NASA's");
            Console.WriteLine("Astronomy Picture of the Day (APOD).");
        }

        private static void ScheduleTask()
        {
            try
            {
                Console.WriteLine("Setting up scheduled task to run daily at 9:00 AM...");

                string exePath = Environment.ProcessPath ?? throw new InvalidOperationException("Process path is not available.");
                string taskName = "NasaApodWallpaper";

                // Create the scheduled task using schtasks.exe
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/create /tn {taskName} /tr \"\\\"{exePath}\\\"\" /sc daily /st 09:00 /ru SYSTEM /f",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process process = new Process { StartInfo = psi })
                {
                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        Console.WriteLine("Task scheduled successfully!");
                        Console.WriteLine("The wallpaper will update daily at 9:00 AM.");
                        Console.WriteLine("Running the application now to update your wallpaper...");
                        
                        // Start a new instance of the app without --schedule to update wallpaper now
                        ProcessStartInfo runNow = new ProcessStartInfo
                        {
                            FileName = exePath,
                            UseShellExecute = true
                        };
                        Process.Start(runNow);
                    }
                    else
                    {
                        Console.WriteLine("Failed to schedule task. Try running this application as Administrator.");
                        Console.WriteLine(error);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scheduling task: {ex.Message}");
            }
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        private static async Task<ApodData?> GetApodDataAsync()
        {
            try
            {
                string url = $"https://api.nasa.gov/planetary/apod?api_key={ApiKey}";
                var response = await httpClient.GetStringAsync(url);
                LogMessage($"Raw APOD API response: {response}");
                return JsonSerializer.Deserialize(response, AppJsonContext.Default.ApodData);
            }
            catch (Exception ex)
            {
                LogMessage($"Error fetching APOD data: {ex.Message}");
                return null;
            }
        }

        private static async Task<bool> DownloadImageAsync(string url, string filePath)
        {
            try
            {
                // Check if file already exists and is recent
                if (File.Exists(filePath) && File.GetCreationTime(filePath).Date == DateTime.Today)
                {
                    LogMessage($"Image already exists for today: {filePath}");
                    return true;
                }

                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fileStream);
                
                LogMessage($"Image downloaded to {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"Error downloading image: {ex.Message}");
                return false;
            }
        }

        private static bool SetWallpaper(string imagePath)
        {
            try
            {
                // Ensure the code runs only on Windows
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Set the wallpaper style to fit (in registry)
                    using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true))
                    {
                        if (key != null)
                        {
                            key.SetValue("WallpaperStyle", "10"); // 10 = Fill
                            key.SetValue("TileWallpaper", "0");
                        }
                        else
                        {
                            LogMessage("Failed to open registry key for wallpaper settings.");
                            return false;
                        }
                    }

                    // Set the wallpaper
                    int result = SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, imagePath, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                    return result != 0;
                }
                else
                {
                    LogMessage("Setting wallpaper is only supported on Windows.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error setting wallpaper: {ex.Message}");
                return false;
            }
        }

        private static async Task<Config> LoadConfigAsync()
        {
            if (!File.Exists(ConfigPath))
                return new Config();

            try
            {
                await using var stream = new FileStream(ConfigPath, FileMode.Open, FileAccess.Read);
                return await JsonSerializer.DeserializeAsync(stream, AppJsonContext.Default.Config) ?? new Config();
            }
            catch
            {
                return new Config();
            }
        }

        private static async Task SaveConfigAsync(Config config)
        {
            await using var stream = new FileStream(ConfigPath, FileMode.Create, FileAccess.Write);
            await JsonSerializer.SerializeAsync(stream, config, AppJsonContext.Default.Config);
        }

        private static void LogMessage(string message)
        {
            string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
            Console.WriteLine(logEntry);

            try
            {
                File.AppendAllText(LogPath, logEntry + Environment.NewLine);
            }
            catch
            {
                // If logging fails, continue anyway
            }
        }
    }

    // Data classes
    class ApodData
    {
        [JsonPropertyName("date")]
        public string? Date { get; set; }
        [JsonPropertyName("explanation")]
        public string? Explanation { get; set; }
        [JsonPropertyName("hdurl")]
        public string? HdUrl { get; set; }
        [JsonPropertyName("media_type")]
        public string? MediaType { get; set; }
        [JsonPropertyName("service_version")]
        public string? ServiceVersion { get; set; }
        [JsonPropertyName("title")]
        public string? Title { get; set; }
        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }

    class Config
    {
        public string LastUpdate { get; set; } = "";
        public string LastImage { get; set; } = "";
    }
}