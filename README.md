# NASA APOD Wallpaper Changer

This Windows application automatically sets your desktop wallpaper to NASA's Astronomy Picture of the Day (APOD).

## Features

- Downloads the daily APOD image from NASA (uses HD version when available)
- Sets images directly as wallpaper (JPEG/PNG) on modern Windows
- Falls back to BMP conversion only if needed for compatibility
- Automatically cleans up old images (keeps last 7 days)
- Can be scheduled to run daily
- Prevents duplicate downloads on the same day
- Comprehensive logging to track operations

## Usage

- **Update wallpaper now**: `NasaApodWallpaper.exe`
- **Schedule daily updates**: `NasaApodWallpaper.exe --schedule` (sets up task at 9:00 AM)
- **Force update**: `NasaApodWallpaper.exe --force` (ignores "already updated today" check)
- **Show help**: `NasaApodWallpaper.exe --help`

## Requirements

- Windows 10/11
- .NET 9.0 (self-contained build provided)
- NASA API key set in environment variable `NASA_API_KEY`

### Getting a NASA API Key

1. Visit [NASA API Portal](https://api.nasa.gov/)
2. Click "Generate API Key"
3. Fill out the form to get your free API key
4. Set the environment variable:

   ```cmd
   setx NASA_API_KEY "your_api_key_here"
   ```

## Installation

1. Download the latest release
2. Set your NASA API key as environment variable
3. Run `NasaApodWallpaper.exe --schedule` to set up daily updates
4. Or run manually whenever you want to update your wallpaper

## Configuration

The application stores its configuration and images in:
`%LOCALAPPDATA%\NasaApodWallpaper\`

- `config.json` - Tracks last update date and image path
- `Images\` - Downloaded APOD images (auto-cleaned after 7 days)
- `apod.log` - Application logs

## Build

```pwsh
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true
```

## Troubleshooting

- **"NASA API key not found"**: Set the `NASA_API_KEY` environment variable
- **"Already updated today"**: Use `--force` flag to override
- **Wallpaper not setting**: The app will automatically try BMP conversion as fallback
- **Scheduled task fails**: Run `--schedule` as Administrator

## License

MIT
