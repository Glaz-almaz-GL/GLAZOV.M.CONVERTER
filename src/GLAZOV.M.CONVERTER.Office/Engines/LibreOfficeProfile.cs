namespace GLAZOV.M.CONVERTER.Office.Engines;

/// <summary>
/// The user profile a LibreOffice run starts with: macros off always; by default also links of the document to the outside blocked and the network shut.
/// A document is untrusted input, so the profile is written before the program starts; nothing is taken from the machine's own profile.
/// </summary>
internal static class LibreOfficeProfile
{
    private const string Open = """
        <?xml version="1.0" encoding="UTF-8"?>
        <oor:items xmlns:oor="http://openoffice.org/2001/registry" xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
        """;

    // MacroSecurityLevel 3 is "very high" (only signed macros from trusted places), DisableMacrosExecution turns macros off outright.
    // Macros have no second legitimate value, so these two lines are in every profile.
    private const string Macros = """

        <item oor:path="/org.openoffice.Office.Common/Security/Scripting"><prop oor:name="MacroSecurityLevel" oor:op="fuse"><value>3</value></prop></item>
        <item oor:path="/org.openoffice.Office.Common/Security/Scripting"><prop oor:name="DisableMacrosExecution" oor:op="fuse"><value>true</value></prop></item>
        """;

    // BlockUntrustedRefererLinks stops a document from pulling pictures and other linked content from the network or the disk.
    // It does not cover style sheets and CSS backgrounds of HTML, so the network itself is shut: every kind of request goes to
    // a proxy on a closed port (discard, 9) and the list of hosts that skip the proxy is empty.
    private const string Isolation = """

        <item oor:path="/org.openoffice.Office.Common/Security/Scripting"><prop oor:name="BlockUntrustedRefererLinks" oor:op="fuse"><value>true</value></prop></item>
        <item oor:path="/org.openoffice.Inet/Settings"><prop oor:name="ooInetProxyType" oor:op="fuse"><value>2</value></prop></item>
        <item oor:path="/org.openoffice.Inet/Settings"><prop oor:name="ooInetNoProxy" oor:op="fuse"><value></value></prop></item>
        <item oor:path="/org.openoffice.Inet/Settings"><prop oor:name="ooInetHTTPProxyName" oor:op="fuse"><value>127.0.0.1</value></prop></item>
        <item oor:path="/org.openoffice.Inet/Settings"><prop oor:name="ooInetHTTPProxyPort" oor:op="fuse"><value>9</value></prop></item>
        <item oor:path="/org.openoffice.Inet/Settings"><prop oor:name="ooInetHTTPSProxyName" oor:op="fuse"><value>127.0.0.1</value></prop></item>
        <item oor:path="/org.openoffice.Inet/Settings"><prop oor:name="ooInetHTTPSProxyPort" oor:op="fuse"><value>9</value></prop></item>
        <item oor:path="/org.openoffice.Inet/Settings"><prop oor:name="ooInetFTPProxyName" oor:op="fuse"><value>127.0.0.1</value></prop></item>
        <item oor:path="/org.openoffice.Inet/Settings"><prop oor:name="ooInetFTPProxyPort" oor:op="fuse"><value>9</value></prop></item>
        """;

    private const string Close = """

        </oor:items>
        """;

    /// <summary>
    /// The default profile: macros off, outside links blocked, the network shut.
    /// </summary>
    internal const string Settings = Open + Macros + Isolation + Close;

    /// <summary>
    /// The profile when outside resources are allowed: macros stay off, nothing else is restricted.
    /// </summary>
    internal const string SettingsWithExternalResources = Open + Macros + Close;

    /// <summary>
    /// Creates the profile folder with its settings file.
    /// </summary>
    /// <param name="profileFolder">Folder of the profile of this run.</param>
    /// <param name="allowExternalResources">Whether the document may load pictures and styles from outside; macros stay off either way.</param>
    internal static void Write(string profileFolder, bool allowExternalResources = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileFolder);

        string user = Path.Combine(profileFolder, "user");
        Directory.CreateDirectory(user);
        string settings = allowExternalResources ? SettingsWithExternalResources : Settings;
        File.WriteAllText(Path.Combine(user, "registrymodifications.xcu"), settings, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
