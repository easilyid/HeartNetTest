using Serilog;
using YamlDotNet.Serialization;

namespace NetServer;

public static class Configs
{
    private static AppConfig _config;

    public static void Init(string filePath = "config.yaml")
    {
        // 读取配置文件,当前项目根目录
        var yaml = File.ReadAllText(filePath);
        //Log.Information("LoadYamlText:\r\n {Yaml}", yaml);

        // 反序列化配置文件
        var deserializer = new DeserializerBuilder().Build();
        _config = deserializer.Deserialize<AppConfig>(yaml);

        Log.Information("服务器配置加载完毕: IP:{0},端口为:{1},工作线程为:{2},AOI视野范围为:{3},逻辑处理频率为:{4}", Server.ip, Server.port,
            Server.WorkerCount, Server.AoiViewArea, Server.UpdateHz);
    }


    public static ServerConfig Server => _config?.Server;
}

public class ServerConfig
{
    [YamlMember(Alias = "IP")] public string ip { get; set; }

    [YamlMember(Alias = "PORT")] public int port { get; set; }

    [YamlMember(Alias = "WORKER_COUNT")] public int WorkerCount { get; set; }

    [YamlMember(Alias = "AOI_VIEW_AREA")] public float AoiViewArea { get; set; }

    [YamlMember(Alias = "UPDATE_HZ")] public int UpdateHz { get; set; }
}

public class AppConfig
{
    [YamlMember(Alias = "Server")] public ServerConfig Server { get; set; }
}