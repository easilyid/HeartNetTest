using System.Net.Sockets;
using Google.Protobuf;
using Serilog;

namespace Common;

/// <summary>
/// 通用网络连接，可以继承此类实现功能拓展
/// 职责：发送消息，关闭连接，断开回调，接收消息回调，
/// 
/// </summary>
public class Connection : TypeAttributeStore
{
    public delegate void DataReceivedCallback(Connection sender, IMessage data);

    public delegate void DisconnectedCallback(Connection sender);

    public delegate void ErrorCallback(Connection sender, string error);

    /// <summary>
    /// 接收到数据
    /// </summary>
    public DataReceivedCallback OnDataReceived;

    /// <summary>
    /// 连接断开
    /// </summary>
    public DisconnectedCallback OnDisconnected;

    /// <summary>
    /// 连接错误
    /// </summary>
    public ErrorCallback OnError;

    private Socket _socket;
    public Socket Socket => _socket;

    /// <summary>
    /// 消息接收器
    /// </summary>
    private LengthFieldDecoder lfd;

    public Connection(Socket socket)
    {
        this._socket = socket;

        //给这个客户端连接创建一个解码器
        lfd = new LengthFieldDecoder(socket, 64 * 1024, 0, 4, 0, 4);
        lfd.Received += _OnDataRecived;
        lfd.Disconnected += _OnDisconnected;
        lfd.Error += _OnError;
        lfd.Start(); //启动解码器，开始接收消息
    }

    private void _OnError(string error)
    {
        OnError?.Invoke(this, error);
    }

    /// <summary>
    /// 断开连接回调
    /// </summary>
    private void _OnDisconnected()
    {
        _socket = null;
        //向上转发，让其删除本connection对象
        OnDisconnected?.Invoke(this);
    }

    private void _OnDataRecived(byte[] data)
    {
        ushort code = GetUShort(data, 0);
        var msg = ProtoHelper.ParseFrom((int)code, data, 2, data.Length - 2);

        //交给消息路由，让其帮忙转发
        if (MessageRouter.Instance.Running)
        {
            MessageRouter.Instance.AddMessage(this, msg);
        }
    }

    /// <summary>
    /// 获取data数据，偏移offset。获取两个字节
    /// 前提：data必须是大端字节序
    /// </summary>
    private ushort GetUShort(byte[] data, int offset)
    {
        if (BitConverter.IsLittleEndian)
            return
                (ushort)((data[offset] << 8) |
                         data[offset + 1]); //这里的data[offset] << 8是将data[offset]左移8位，然后和data[offset+1]进行或运算 得到的值是二者的和
        return (ushort)((data[offset + 1] << 8) | data[offset]);
    }

    #region 发送消息BENGING END

    /// <summary>
    /// 发送消息包，编码过程(通用)
    /// </summary>
    /// <param name="message"></param>
    public void Send(Google.Protobuf.IMessage message)
    {
        try
        {
            //获取imessage类型所对应的编号，网络传输我们只传输编号
            using (var ds = DataStream.Allocate())
            {
                int code = ProtoHelper.SeqCode(message.GetType());
                ds.WriteInt(message.CalculateSize() + 2); //长度字段
                ds.WriteUShort((ushort)code); //协议编号字段
                message.WriteTo(ds); //数据
                SocketSend(ds.ToArray());
            }
        }
        catch (Exception e)
        {
            Log.Error("[消息] " + e.ToString());
        }
    }

    /// <summary>
    /// 通过socket发送，原生数据
    /// </summary>
    /// <param name="data"></param>
    private void SocketSend(byte[] data)
    {
        SocketSend(data, 0, data.Length);
    }

    /// <summary>
    /// 开始异步发送消息,原生数据
    /// </summary>
    /// <param name="data"></param>
    /// <param name="start"></param>
    /// <param name="len"></param>
    private void SocketSend(byte[] data, int start, int len)
    {
        lock (this) //多线程问题，防止争夺send
        {
            if (_socket != null && _socket.Connected)
            {
                _socket.BeginSend(data, start, len, SocketFlags.None, new AsyncCallback(SendCallback), _socket);
            }
        }
    }

    /// <summary>
    /// 异步发送消息回调
    /// </summary>
    /// <param name="ar"></param>
    private void SendCallback(IAsyncResult ar)
    {
        if (_socket != null && _socket.Connected)
        {
            // 发送的字节数
            int len = _socket.EndSend(ar);
        }
    }

    #endregion
    
    /// <summary>
    /// 主动关闭连接
    /// </summary>
    public void ActiveClose()
    {
        _socket = null;
        //转交给下一层的解码器关闭连接
        lfd.ActiveClose();
    }
}