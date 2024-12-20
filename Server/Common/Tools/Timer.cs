namespace Common;

/// <summary>
/// 时间单位
/// </summary>
public enum TimeUnit
{
    /// <summary>
    /// 毫秒
    /// </summary>
    Milliseconds,

    /// <summary>
    /// 秒
    /// </summary>
    Seconds,

    /// <summary>
    /// 分钟
    /// </summary>
    Minutes,

    /// <summary>
    /// 小时
    /// </summary>
    Hours,

    /// <summary>
    /// 天
    /// </summary>
    Days
}

public class Timer
{
    //游戏开始的时间戳
    private static long startTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

    //游戏的运行时间（秒），帧开始的时间
    /// <summary>
    /// 帧开始的时间 运行时间
    /// </summary>
    public static float time { get; private set; }

    /// <summary>
    /// 上一帧运行所用的时间
    /// </summary>
    public static float deltaTime { get; private set; }

    // 记录最后一次tick的时间
    private static long lastTick = 0;

    /// <summary>
    /// 由Schedule调用，请不要自行调用，除非知道自己在做什么！！！
    /// 这个是一个时间的计算器，用来计算游戏的运行时间
    /// 通过这个时间计算器，我们可以计算出游戏的运行时间，以及每一帧所用的时间
    /// </summary>
    public static void Tick()
    {
        long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        time = (now - startTime) * 0.001f;
        if (lastTick == 0) lastTick = now;
        deltaTime = (now - lastTick) * 0.001f; //deltaTime是以秒作为单位的
        lastTick = now;
    }
}