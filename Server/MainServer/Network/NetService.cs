﻿using System.Net;
using Common;
using Proto;
using Serilog;

namespace NetServer;

/// <summary>
/// 网络服务
/// </summary>
public class NetService
{
    //负责监听TCP连接
    TcpServer tcpServer;

    /// <summary>
    /// 记录conn最后一次心跳包的时间
    /// key: 连接
    ///  value: 最后一次心跳包的时间
    /// </summary>
    private Dictionary<Connection, DateTime> heartBeatPairs = new Dictionary<Connection, DateTime>();

    //心跳超时时间
    private static int HEARTBEATTIMEOUT = 5;

    //服务器查询心跳字典的间隔时间
    private static int HEARTBEATQUERYTIME = 5;

    private NET_ENUM NET_ENUM;


    public NetService(NET_ENUM NET_ENUM)
    {
        this.NET_ENUM = NET_ENUM;
        switch (this.NET_ENUM)
        {
            case NET_ENUM.TCP:
                tcpServer = new TcpServer(Configs.Server.ip, Configs.Server.port);
                tcpServer.Connected += OnConnected;
                tcpServer.Disconnected += OnDisconnected;
                tcpServer.Error += OnError;
                break;
            case NET_ENUM.UDP:
                Log.Information("[当前版本不支持UDP，等后续完善]");
                break;
        }
    }

    /// <summary>
    /// 开启当前服务
    /// </summary>
    public async Task Start()
    {
        //启动网络监听
        await tcpServer.Start();

        //启动消息分发器
        MessageRouter.Instance.Start(Configs.Server.WorkerCount);

        //订阅心跳事件
        MessageRouter.Instance.Subscribe<HeartBeatRequest>(_HeartBeatRequest);

        //定时检查心跳包的情况
        Timer timer = new Timer(TimerCallback, null, TimeSpan.Zero, TimeSpan.FromSeconds(HEARTBEATQUERYTIME));
    }


    /// <summary>
    /// 客户端断开连接回调
    /// </summary>
    /// <param name="conn"></param>
    private void OnDisconnected(Connection conn)
    {
        //从心跳字典中删除连接
        if (heartBeatPairs.ContainsKey(conn))
        {
            heartBeatPairs.Remove(conn);
        }

        //session
        var session = conn.GetTValue<Session>(); //从连接中获取session
        if (session != null)
        {
            session.Conn = null;
            Log.Information("[连接断开]");
        }
        else
        {
            Log.Information("[连接断开]未知用户");
        }
    }

    /// <summary>
    /// 客户端链接成功的回调
    /// </summary>
    private void OnConnected(Connection conn)
    {
        try
        {
            if (conn.Socket is { Connected: true })
            {
                var endPoint = conn.Socket.RemoteEndPoint;
                Log.Information("[客户端连接成功]接入客户端Ip{0}，端口{1}", IPAddress.Parse(((IPEndPoint)endPoint).Address.ToString()),
                    ((IPEndPoint)endPoint).Port.ToString());

                // 给conn添加心跳时间
                heartBeatPairs[conn] = DateTime.Now; //记录当前时间
            }
            else
            {
                Log.Warning("[NetService]尝试访问已关闭的 Socket 对象");
            }
        }
        catch (ObjectDisposedException e)
        {
            Log.Error("[NetService]Socket 已被释放: " + e.Message);
        }
    }


    private void OnError(Connection conn, string error)
    {
        Log.Error("[服务器链接错误]" + conn.Socket.RemoteEndPoint + " : " + error);
    }

    /// <summary>
    /// 接收到心跳包的处理
    /// </summary>
    /// <param name="conn"></param>
    /// <param name="message"></param>
    public void _HeartBeatRequest(Connection conn, HeartBeatRequest message)
    {
        //更新心跳时间
        heartBeatPairs[conn] = DateTime.Now;
        var session = conn.GetTValue<Session>();
        if (session != null)
        {
            session.LastHeartTime = Common.Timer.time;
        }

        //响应
        HeartBeatResponse resp = new HeartBeatResponse();
        conn.Send(resp);
    }
    
    /// <summary>
    /// 检查心跳包的回调,这里是自己启动了一个timer。可以考虑交给中心计时器
    /// </summary>
    /// <param name="state"></param>
    private void TimerCallback(object state)
    {
        DateTime nowTime = DateTime.Now;
        //这里规定心跳包超过30秒没用更新就将连接清理
        foreach (var kv in heartBeatPairs)
        {
            TimeSpan gap = nowTime - kv.Value;
            if (gap.TotalSeconds > HEARTBEATTIMEOUT)
            {
                //关闭超时的客户端连接
                Connection conn = kv.Key;
                Log.Information("[心跳检查]心跳超时==>");//移除相关的资源
                ActiveClose(conn);
            }
        }
    }

    /// <summary>
    /// 主动关闭某个连接
    /// </summary>
    public void ActiveClose(Connection conn)
    {
        if (conn == null) return;

        //从心跳字典中删除连接
        if (heartBeatPairs.ContainsKey(conn))
        {
            heartBeatPairs.Remove(conn);
        }

        //session
        var session = conn.GetTValue<Session>();
        if (session != null)
        {
            session.Conn = null;
        }

        //转交给下一层的connection去进行关闭
        conn.ActiveClose();
    }
}