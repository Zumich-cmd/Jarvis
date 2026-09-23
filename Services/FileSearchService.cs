using System.IO;

namespace Jarvis.Services;

public sealed class FileSearchService
{
    private readonly string[] _roots =
    {
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
    };

    public IReadOnlyList<string> Search(string query, int count = 8)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<string>();

        string pattern = query.StartsWith('.') ? $"*{query}" : $"*{query}*";
        var results = new List<string>();
        foreach (string root in _roots.Where(Directory.Exists).Distinct())
        {
            try
            {
                results.AddRange(Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories).Take(count - results.Count));
                if (results.Count >= count)
                    break;
            }
            catch
            {
            }
        }

        return results;
    }

    public bool TryOpen(string path, bool containingFolder)
    {
        try
        {
            string target = containingFolder ? Path.GetDirectoryName(path) ?? path : path;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
