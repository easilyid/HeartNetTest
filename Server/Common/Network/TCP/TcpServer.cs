using System.Net;
using System.Net.Sockets;
using Google.Protobuf;
using Serilog;

namespace Common;

/// <summary>
/// 负责监听TCP网络端口，基于Async编程模型，其中SocketAsyncEventArg实现了iocp模型
/// 三个委托
/// ----Connected       有新的连接
/// ----DataReceived    有新的消息
/// ----Disconnected    有连接断开
/// IsRunning           是否正在运行
/// Stop()              关闭服务
/// Start()             启动服务
/// </summary>
public class TcpServer : IDisposable
{
    //网络连接的属性
    private IPEndPoint endPoint; // 这个是网络的 IP 地址和端口号
    private Socket listenerSocket; //服务端监听对象
    private int backlog = 100; //可以排队接收的传入连接数

    /// <summary>
    /// 异步事件参数
    /// </summary>
    public SocketAsyncEventArgs args;

    //委托
    public delegate void ConnectedCallback(Connection conn);

    public delegate void DataReceivedCallback(Connection conn, IMessage data);

    public delegate void DisconnectedCallback(Connection conn);

    public delegate void ErrorCallback(Connection conn, string error);


    /// <summary>
    /// 客户端连入事件
    /// </summary>
    public event EventHandler<Socket> SocketConnected;

    /// <summary>
    /// 接收到连接的事件
    /// </summary>
    public event ConnectedCallback Connected;

    /// <summary>
    /// 接收到消息的事件
    /// </summary>
    public event DataReceivedCallback DataReceived;

    /// <summary>
    /// 接收到连接断开的事件
    /// </summary>
    public event DisconnectedCallback Disconnected; //接收到连接断开的事件

    /// <summary>
    /// 链接过程中错误的事件
    /// </summary>
    public event ErrorCallback Error;

    public TcpServer(string host, int port)
    {
        endPoint = new IPEndPoint(IPAddress.Parse(host), port);
    }

    public TcpServer(string host, int port, int backlog) : this(host, port)
    {
        this.backlog = backlog;
    }


    public bool IsRunning => listenerSocket != null;


    public async Task Start()
    {
        try
        {
            if (!IsRunning)
            {
                listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                listenerSocket.Bind(endPoint); //绑定一个IPEndPoint
                listenerSocket.Listen(backlog); //开始监听，并设置等待队列长度

                args = new SocketAsyncEventArgs(); //可以复用,当前监听连接socket复用
                args.Completed += OnAccept; //当有用户的连接时触发回调函数
                listenerSocket.AcceptAsync(args); //异步接收
            }
            else
            {
                Log.Information("[TcpServer] 服务器正在运行中");
            }
        }
        catch (Exception e)
        {
            Log.Error(e.Message);
        }
        finally
        {
            // 确保在退出前清理资源
            Stop();
        }
    }

    private void OnAccept(object? sender, SocketAsyncEventArgs e)
    {
        try
        {
            Socket? client = e.AcceptSocket;
            e.AcceptSocket = null;
            listenerSocket.AcceptAsync(e); //继续接收下一个连接


            if (e.SocketError == SocketError.OperationAborted)
                return; //服务器已停止

            if (e.SocketError == SocketError.Success && client is { Connected: true })
            {
                OnSocketConnected(client);
            }
            else
            {
                Log.Warning("[TcpServer] 无法接受连接: " + e.SocketError);
            }
        }
        catch (ObjectDisposedException exception)
        {
            Log.Error("[TcpServer]Socket 已被释放: " + exception.Message);
        }
    }

    private void OnSocketConnected(Socket client)
    {
        SocketConnected?.Invoke(this, client);
        Connection conn = new Connection(client);
        conn.OnDataReceived += OnDataReceived;
        conn.OnDisconnected += OnDisconnected;
        conn.OnError += OnError;
    }

    private void OnError(Connection sender, string error)
    {
        Error?.Invoke(sender, error);
    }

    private void OnDisconnected(Connection sender)
    {
        Disconnected?.Invoke(sender);
    }

    private void OnDataReceived(Connection sender, IMessage data)
    {
        DataReceived?.Invoke(sender, data);
    }


    /// <summary>
    /// 主动关闭服务，停止监听连接
    /// </summary>
    public void Stop()
    {
        if (listenerSocket == null)
        {
            return;
        }

        listenerSocket.Close();
        listenerSocket = null;
    }


    private Boolean disposed = false;

    ~TcpServer()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
            {
                Stop();
                if (args != null)
                    args.Dispose();
            }

            disposed = true;
        }
    }
}