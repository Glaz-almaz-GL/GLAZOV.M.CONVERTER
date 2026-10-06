namespace GLAZOV.M.CONVERTER.Office.Engines;

/// <summary>
/// The user profile a LibreOffice run starts with: macros off, links of the document to the outside blocked, the network shut.
/// A document is untrusted input, so the profile is written before the program starts; nothing is taken from the machine's own profile.
/// </summary>
internal static class LibreOfficeProfile
{
    // MacroSecurityLevel 3 is "very high" (only signed macros from trusted places), DisableMacrosExecution turns macros off outright.
    // BlockUntrustedRefererLinks stops a document from pulling pictures and other linked content from the network or the disk.
    // It does not cover style sheets and CSS backgrounds of HTML, so the network itself is shut: every kind of request goes to
    // a proxy on a closed port (discard, 9) and the list of hosts that skip the proxy is empty.
    internal const string Settings = """
        <?xml version="1.0" encoding="UTF-8"?>
        <oor:items xmlns:oor="http://openoffice.org/2001/registry" xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
        <item oor:path="/org.openoffice.Office.Common/Security/Scripting"><prop oor:name="MacroSecurityLevel" oor:op="fuse"><value>3</value></prop></item>
        <item oor:path="/org.openoffice.Office.Common/Security/Scripting"><prop oor:name="DisableMacrosExecution" oor:op="fuse"><value>true</value></prop></item>
        <item oor:path="/org.openoffice.Office.Common/Security/Scripting"><prop oor:name="BlockUntrustedRefererLinks" oor:op="fuse"><value>true</value></prop></item>
        <item oor:path="/org.openoffice.Inet/Settings"><prop oor:name="ooInetProxyType" oor:op="fuse"><value>2</value></prop></item>
        <item oor:path="/org.openoffice.Inet/Settings"><prop oor:name="ooInetNoProxy" oor:op="fuse"><value></value></prop></item>
        <item oor:path="/org.openoffice.Inet/Settings"><prop oor:name="ooInetHTTPProxyName" oor:op="fuse"><value>127.0.0.1</value></prop></item>
        <item oor:path="/org.openoffice.Inet/Settings"><prop oor:name="ooInetHTTPProxyPort" oor:op="fuse"><value>9</value></prop></item>
        <item oor:path="/org.openoffice.Inet/Settings"><prop oor:name="ooInetHTTPSProxyName" oor:op="fuse"><value>127.0.0.1</value></prop></item>
        <item oor:path="/org.openoffice.Inet/Settings"><prop oor:name="ooInetHTTPSProxyPort" oor:op="fuse"><value>9</value></prop></item>
        <item oor:path="/org.openoffice.Inet/Settings"><prop oor:name="ooInetFTPProxyName" oor:op="fuse"><value>127.0.0.1</value></prop></item>
        <item oor:path="/org.openoffice.Inet/Settings"><prop oor:name="ooInetFTPProxyPort" oor:op="fuse"><value>9</value></prop></item>
        </oor:items>
        """;

    /// <summary>
    /// Creates the profile folder with its settings file.
    /// </summary>
    /// <param name="profileFolder">Folder of the profile of this run.</param>
    internal static void Write(string profileFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileFolder);

        string user = Path.Combine(profileFolder, "user");
        Directory.CreateDirectory(user);
        File.WriteAllText(Path.Combine(user, "registrymodifications.xcu"), Settings, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
