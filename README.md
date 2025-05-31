# NASA APOD Wallpaper Changer

This Windows application automatically sets your desktop wallpaper to NASA's Astronomy Picture of the Day (APOD).

## Features

- Downloads the daily APOD image from NASA
- Converts it to BMP for Windows compatibility
- Sets it as your desktop wallpaper
- Can be scheduled to run daily

## Usage

- Run the executable to update your wallpaper immediately.
- Use `--schedule` to set up a daily scheduled task at 9:00 AM.
- Use `--help` for usage instructions.

## Requirements

- Windows 10/11
- .NET 9.0 (self-contained build provided)
- Set your NASA API key in the environment variable `NASA_API_KEY`.

## Build

```pwsh
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true
```

## License

MIT
