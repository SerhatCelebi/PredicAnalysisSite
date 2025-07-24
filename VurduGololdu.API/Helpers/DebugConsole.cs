namespace VurduGololdu.API.Helpers;

public static class DebugConsole
{
#if DEBUG
    public static void Log(string message) => Console.WriteLine(message);
#else
    public static void Log(string message) { /* production: no-op */ }
#endif
}