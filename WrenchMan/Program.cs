namespace WrenchMan;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var bot = new WrenchManBot();

        await bot.Start();
        await bot.WaitForClose();
    }
    
    public static void Trace(string source, string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkBlue;
        Console.WriteLine($"[{DateTime.UtcNow.ToString("u")}] [TRACE] [{source}] {message}");
        Console.ResetColor();
    }
    
    public static void Debug(string source, string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"[{DateTime.UtcNow.ToString("u")}] [DEBUG] [{source}] {message}");
        Console.ResetColor();
    }

    public static void Info(string source, string message)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[{DateTime.UtcNow.ToString("u")}] [INFO] [{source}] {message}");
        Console.ResetColor();
    }
    
    public static void Warn(string source, string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[{DateTime.UtcNow.ToString("u")}] [WARN] [{source}] {message}");
        Console.ResetColor();
    }
    
    public static void Error(string source, string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.BackgroundColor = ConsoleColor.Red;
        Console.Write($"[{DateTime.UtcNow.ToString("u")}] [ERROR]");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[{source}] {message}");
        Console.ResetColor();
    }
    
    public static void Fatal(string source, string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.BackgroundColor = ConsoleColor.Red;
        Console.Write($"[{DateTime.UtcNow.ToString("u")}] [FATAL] [{source}] {message}");
        Console.ResetColor();
    }
}