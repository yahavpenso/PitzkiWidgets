using System.IO;

namespace PitzkiWidgets.Core;

/// <summary>
/// Putting the app in the Start menu, so it is found by typing its name like anything else. It is
/// one shortcut in this user's own Start menu folder: nothing is installed, nothing is written
/// anywhere else, and taking it off removes that one file. Windows itself is asked to make the
/// shortcut, through the same thing a script would use.
/// </summary>
public static class StartMenu
{
    public const string AppName = "Pitzki Widgets";

    /// <summary>Where the shortcut goes: this user's own Start menu, not everybody's.</summary>
    public static string LinkPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", AppName + ".lnk");

    public static bool There => File.Exists(LinkPath);

    /// <summary>
    /// Puts it in the Start menu, or takes it out. True when the Start menu now says what was wanted.
    /// The tests hand it a shortcut of their own somewhere else, so the real Start menu is left alone.
    /// </summary>
    public static bool Set(bool want, string? exe = null, string? link = null)
    {
        exe ??= Environment.ProcessPath;
        link ??= LinkPath;
        try
        {
            if (!want)
            {
                if (File.Exists(link)) File.Delete(link);
                return !File.Exists(link);
            }
            if (exe == null || !File.Exists(exe)) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(link)!);
            Make(link, exe);
            return File.Exists(link);
        }
        catch { return File.Exists(link); }    // a locked-down machine is no reason to bring the app down
    }

    /// <summary>One shortcut, made by Windows' own shell.</summary>
    static void Make(string link, string exe)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null) return;
        object? shell = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            if (shell == null) return;
            object? cut = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { link });
            if (cut == null) return;
            var kind = cut.GetType();
            void Say(string what, object value) => kind.InvokeMember(what, System.Reflection.BindingFlags.SetProperty, null, cut, new[] { value });
            Say("TargetPath", exe);
            Say("WorkingDirectory", Path.GetDirectoryName(exe) ?? "");
            Say("IconLocation", exe + ",0");
            Say("Description", "Little cards on your desktop");
            kind.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, cut, null);
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(cut);
        }
        finally
        {
            if (shell != null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }
    }
}
