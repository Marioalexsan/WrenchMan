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
}
