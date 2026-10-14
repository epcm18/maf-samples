using System.Text;

namespace Shared;

public static class Out
{
    public static void Init(string title)
    {
        System.Console.OutputEncoding = Encoding.UTF8;
        Header(title);
    }

    public static void Header(string text) => Write(text, ConsoleColor.Cyan);

    public static void Step(string text) => Write(text, ConsoleColor.Green);

    public static void Note(string text) => Write(text, ConsoleColor.DarkGray);

    public static void Warn(string text) => Write(text, ConsoleColor.Yellow);

    public static void Separator()
    {
        System.Console.WriteLine();
        // WindowWidth throws when output is redirected (piped to a file, CI, an IDE pane).
        int width = System.Console.IsOutputRedirected ? 100 : Math.Min(System.Console.WindowWidth, 100);
        Write(new string('-', width), ConsoleColor.DarkGray);
        System.Console.WriteLine();
    }

    private static void Write(string text, ConsoleColor color)
    {
        ConsoleColor original = System.Console.ForegroundColor;
        try
        {
            System.Console.ForegroundColor = color;
            System.Console.WriteLine(text);
        }
        finally
        {
            System.Console.ForegroundColor = original;
        }
    }
}
