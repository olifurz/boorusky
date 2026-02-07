using System.Timers;
using static System.Environment;
using Microsoft.Extensions.Logging;
using TinyLogger;
using Timer = System.Timers.Timer;


namespace Boorusky;

internal static class Boorusky
{
    private static readonly Timer Timer = new();
    private static Grabber? _grabber;
    public static readonly HttpClient Client = new();
    public static ILogger? Logger;

    private static async Task Main(string[] args)
    {
        if (!File.Exists(".env"))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(".env file is missing. Please read the documentation.");
            Console.ResetColor();
            return;
        }

        DrawAscii();
        DotNetEnv.Env.Load(".env");

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddTinyLogger(options =>
            {
                options.Template = MessageTemplates.DefaultTimestamped;

                options.Template = "{timestamp_utc} {logLevel_short}: "
                                   + "{message}{newLine}"
                                   + "{exception_newLine}{exception}{exception_newLine}";

                options.AddConsole();
                options.AddFile("latest.log");
            });
        });
        Logger = loggerFactory.CreateLogger("General");
        _grabber = new Grabber();
        await _grabber.Initialise();

        if (!string.IsNullOrEmpty(args.FirstOrDefault(s => s == "-dry")))
        {
            Logger.LogWarning("Running dry run! (-dry argument used)");
            Logger.LogInformation("Check the target page after running to make sure it has been successful.");
            await _grabber.Post(null, null, true)!;
        }
        else
        {
            Timer.Elapsed += TimerTick;
            Timer.Enabled = true;
            Timer.Interval = MilliSecondsLeftTilTheHour();

            Logger.LogInformation("Starting schedule, if everything looks good then you can leave this be.\nHit CTRL + C or your shell's suitable key to stop this program if you notice any issues.");
            while (true)
            {
                Thread.Sleep(1000);
                var input = Console.ReadLine();
                if (input != null && input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }
            Logger.LogInformation("Goodbye!");
        }
    }


    private static int MilliSecondsLeftTilTheHour()
    {
        var minutesRemaining = 59 - DateTime.Now.Minute;
        var secondsRemaining = 59 - DateTime.Now.Second;
        var interval = ((minutesRemaining * 60) + secondsRemaining) * 1000;

        // If we happen to be exactly on the hour
        if (interval == 0)
        {
            interval = 60 * 60 * 1000;
        }
        return interval;
    }

    private static void TimerTick(object? sender, ElapsedEventArgs? elapsedEventArgs)
    {
        Task.Run(async () => {
            try
            {
                Timer.Interval = MilliSecondsLeftTilTheHour();
                await _grabber?.Post(null, null, false)!;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Scheduled post failed, retrying after 10 seconds");
                Thread.Sleep(10000);
                TimerTick(null, null); // Retry
            }
        });
}
    private static void DrawAscii()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(" _                                _          ");
        Thread.Sleep(250);
        Console.WriteLine("| |__   ___   ___  _ __ _   _ ___| | ___   _ ");
        Thread.Sleep(250);
        Console.WriteLine("| '_ \\ / _ \\ / _ \\| '__| | | / __| |/ / | | |");
        Thread.Sleep(250);
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine("| |_) | (_) | (_) | |  | |_| \\__ \\   <| |_| |");
        Thread.Sleep(250);
        Console.WriteLine("|_.__/ \\___/ \\___/|_|   \\__,_|___/_|\\_\\\\__, |");
        Thread.Sleep(250);
        Console.WriteLine("                                       |___/ ");
        Console.ResetColor();
    }

    public static void AddHttpHeaders(HttpRequestMessage request)
    {
        var userAgent = GetEnvironmentVariable("USER_AGENT") ?? "Boorusky/1.0";
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
        request.Headers.Add("Connection", "keep-alive");
        request.Headers.Add("DNT", "1");
        request.Headers.Add("Upgrade-Insecure-Requests", "1");
        request.Headers.Add("User-Agent", userAgent);
    }
}
