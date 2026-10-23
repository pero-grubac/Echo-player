using EchoPlayer.Core;

namespace EchoPlayer.Desktop.ViewModels;

/// <summary>A named starting point for the echo settings.</summary>
public sealed record EchoPreset(string Name, string Description, EchoSettings Settings)
{
    public static IReadOnlyList<EchoPreset> All { get; } =
    [
        new("Slapback", "One quick, short repeat", new EchoSettings(90, 0.1, 0.55)),
        new("Room", "A small, close space", new EchoSettings(160, 0.35, 0.4)),
        new("Classic", "The default echo", EchoSettings.Default),
        new("Hall", "Long, rolling repeats", new EchoSettings(420, 0.55, 0.5)),
        new("Canyon", "Far away and slow to fade", new EchoSettings(750, 0.65, 0.6)),
    ];
}
