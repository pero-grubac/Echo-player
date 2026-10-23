using System.Globalization;

namespace EchoPlayer.Desktop.ViewModels;

internal static class Format
{
    /// <summary>For example "0:07.4", or "1:02:07.4" for an hour or more.</summary>
    public static string Time(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
            time = TimeSpan.Zero;

        string seconds = (time.Seconds + time.Milliseconds / 1000.0).ToString("00.0", CultureInfo.InvariantCulture);
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{seconds}"
            : $"{time.Minutes}:{seconds}";
    }

    public static string Seconds(TimeSpan time) => time.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";

    public static string Percent(double fraction) => (fraction * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
}
