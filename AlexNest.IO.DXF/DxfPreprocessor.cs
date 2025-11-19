using System.Text;

namespace AlexNest.IO.DXF;

public static class DxfPreprocessor
{
    public static string EnsureSupportedVersion(string originalPath)
    {
        // Read the header block and look for $ACADVER
        // We'll read whole file for simplicity; DXFs aren't usually massive in nesting context
        var text = File.ReadAllText(originalPath, Encoding.ASCII);

        // Very dumb but robust-enough approach: find "$ACADVER" and replace the next "AC10xx"
        const string marker = "$ACADVER";
        int idx = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return originalPath; // no version marker? let netDxf try

        // Find the next "AC10" token after $ACADVER
        int acIdx = text.IndexOf("AC10", idx, StringComparison.OrdinalIgnoreCase);
        if (acIdx < 0)
            return originalPath;

        string current = text.Substring(acIdx, 6); // e.g. AC1003

        // If it's already R12 or newer, just return
        if (string.Compare(current, "AC1015", StringComparison.OrdinalIgnoreCase) >= 0)
            return originalPath;

        // Otherwise, bump to AC1009 (R12)
        string patched = text.Remove(acIdx, 6).Insert(acIdx, "AC1015");

        // Write to temp file so we don't overwrite the original
        string tempPath = Path.Combine(Path.GetTempPath(),
            "AlexNest_" + Path.GetFileNameWithoutExtension(originalPath) + "_R12.dxf");

        File.WriteAllText(tempPath, patched, Encoding.ASCII);

        return tempPath;
    }
}