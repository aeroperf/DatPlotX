using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace DatPlotX.Services;

/// <summary>
/// Registers DatPlotX as the handler for <c>.dpx</c> project files so a double-click opens
/// the project in DatPlotX.
/// <para>
/// <b>Windows:</b> writes per-user keys under <c>HKCU\Software\Classes</c> — no admin rights and
/// no installer required. Exposed as an opt-in action (the user explicitly asks DatPlotX to become
/// the default handler) rather than registering silently on launch.
/// </para>
/// <para>
/// <b>macOS:</b> the association is declared in the app bundle's <c>Info.plist</c>
/// (<c>CFBundleDocumentTypes</c>) and registered by Launch Services when the bundle is installed,
/// so in-app registration is a no-op there.
/// </para>
/// <para><b>Linux:</b> not handled here (left to the packaging layer / <c>.desktop</c> files).</para>
/// </summary>
public interface IFileAssociationService
{
    /// <summary>True on platforms where in-app registration is meaningful (Windows only).</summary>
    bool IsSupported { get; }

    /// <summary>True when DatPlotX is already the registered <c>.dpx</c> handler for the current user.</summary>
    bool IsRegistered();

    /// <summary>
    /// Register DatPlotX as the current user's <c>.dpx</c> handler. Returns <c>true</c> on success,
    /// <c>false</c> if the platform is unsupported. Throws if the registry write fails.
    /// </summary>
    bool Register();
}

/// <inheritdoc />
public sealed class FileAssociationService : IFileAssociationService
{
    internal const string Extension = ".dpx";
    internal const string ProgId = "DatPlotX.Project";
    private const string ProgIdDescription = "DatPlotX Project";

    public bool IsSupported => OperatingSystem.IsWindows();

    public bool IsRegistered()
    {
        if (!OperatingSystem.IsWindows())
            return false;
        return IsRegisteredWindows();
    }

    public bool Register()
    {
        if (!OperatingSystem.IsWindows())
            return false;
        RegisterWindows();
        return true;
    }

    [SupportedOSPlatform("windows")]
    private static bool IsRegisteredWindows()
    {
        using var ext = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{Extension}");
        return ext?.GetValue(null) as string == ProgId;
    }

    [SupportedOSPlatform("windows")]
    private static void RegisterWindows()
    {
        string exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot resolve the DatPlotX executable path.");

        // .dpx -> ProgId
        using (var ext = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{Extension}"))
            ext.SetValue(null, ProgId);

        // ProgId -> friendly name, icon, and open command
        using (var progId = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}"))
        {
            progId.SetValue(null, ProgIdDescription);
            using (var icon = progId.CreateSubKey("DefaultIcon"))
                icon.SetValue(null, $"\"{exePath}\",0");
            using (var command = progId.CreateSubKey(@"shell\open\command"))
                command.SetValue(null, $"\"{exePath}\" \"%1\"");
        }

        NotifyShellAssociationChanged();
    }

    [SupportedOSPlatform("windows")]
    private static void NotifyShellAssociationChanged()
        // SHCNE_ASSOCCHANGED (0x08000000), SHCNF_IDLIST (0x0000) — tells Explorer to refresh
        // the icon/handler cache so the new association takes effect without a logout.
        => SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);

    [SupportedOSPlatform("windows")]
    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
}
