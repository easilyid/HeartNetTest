using NetServer;
using Serilog;

namespace NetServer;

public enum NET_ENUM
{
    TCP = 1,
    UDP = 2,
}

public class GameServer
{
    public async Task Run()
    {
        try
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {//全局异常捕获
                Log.Error("sender:[0],Message{1}", sender.ToString(), e.ExceptionObject.ToString());
            };


            NetService netService = new NetService(NET_ENUM.TCP);
            await netService.Start();
        }
        catch (Exception e)
        {
            Log.Error(e.Message);
        }
    }
}