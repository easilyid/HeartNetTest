using Serilog;

namespace NetServer;

public class Program
{
    public static void Main(string[] args)
    {
        //初始化日志环境
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Console(
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File("logs\\server-log.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        Log.Debug("[日志服务启动,日志等级:Verbose，日志路径为{0}]", "logs\\server-log.txt");

        Log.Debug("[加载server配置信息]");
        Configs.Init();

        Log.Debug("[开始初始化模块]");
        GameServer server = new GameServer();
        server.Run();
    }
}