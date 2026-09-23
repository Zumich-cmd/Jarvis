namespace Jarvis.Api;

public sealed record ApiConnectionInfo(
    string Name,
    string Purpose,
    string File,
    string EntryPoint,
    string Status);
