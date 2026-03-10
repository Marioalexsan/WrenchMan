namespace WrenchMan;

internal class Program
{
    public static WrenchManBot Bot { get; private set; } = null!;

    static async Task Main(string[] args)
    {
        Bot = new();

        while (true)
            await Task.Delay(100);
    }
    
    public static void Trace(string source, string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkBlue;
        Console.WriteLine($"[TRACE] [{source}] {message}");
        Console.ResetColor();
    }
    
    public static void Debug(string source, string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"[DEBUG] [{source}] {message}");
        Console.ResetColor();
    }

    public static void Info(string source, string message)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[INFO] [{source}] {message}");
        Console.ResetColor();
    }
    
    public static void Warn(string source, string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[WARN] [{source}] {message}");
        Console.ResetColor();
    }
    
    public static void Error(string source, string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.BackgroundColor = ConsoleColor.Red;
        Console.Write($"[ERROR]");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[{source}] {message}");
        Console.ResetColor();
    }
    
    public static void Fatal(string source, string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.BackgroundColor = ConsoleColor.Red;
        Console.Write($"[FATAL] [{source}] {message}");
        Console.ResetColor();
    }
}