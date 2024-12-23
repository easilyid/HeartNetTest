﻿using System.Net.Sockets;
using Serilog;

namespace Common;

/// <summary>
/// Socket异步接收器
/// 可以对接收到的数据的粘包拆包处理
/// 这里我们的数据格式是：4字节长度字段+数据部分
/// 而数据部分的前两个字节是我们的proto协议的编号
/// 异步模型：begin/end   我们改成Async
/// </summary>
public class LengthFieldDecoder
{
    private bool isStart = false; //解码器是否已经启动
    private Socket mSocket; //连接客户端的socket
    private int lengthFieldOffset = 0; //第几个字节是长度字段
    private int lengthFieldLength = 4; //长度字段本身占几个字节
    private int lengthAdjustment = 0; //长度字段和内容之间距离几个字节（也就是长度字段记录了整一个数据包的长度，负数代表向前偏移，body实际长度要减去这个绝对值）
    private int initialBytesToStrip = 4; //表示获取完一个完整的数据包之后，舍弃前面的多少个字节
    private byte[] mBuffer; //接收数据的缓存空间
    private int mOffect = 0; //缓冲区目前的长度
    private int mSize = 64 * 1024; //一次性接收数据的最大字节，默认64kMb

    public delegate void ReceivedHandler(byte[] data);

    public delegate void DisconnectedHandler();

    public delegate void ErrorHandler(string error);

    public event ReceivedHandler Received;
    public event DisconnectedHandler Disconnected;
    public event ErrorHandler Error;

    public LengthFieldDecoder(Socket mSocket, int maxBufferLength, int lengthFieldOffset, int lengthFieldLength,
        int lengthAdjustment, int initialBytesToStrip)
    {
        this.mSocket = mSocket;
        this.mSize = maxBufferLength;
        this.lengthFieldOffset = lengthFieldOffset;
        this.lengthFieldLength = lengthFieldLength;
        this.lengthAdjustment = lengthAdjustment;
        this.initialBytesToStrip = initialBytesToStrip;
        this.mBuffer = new byte[mSize];
    }


    /// <summary>
    /// 获取大端模式int值
    /// </summary>
    private int GetInt32BE(byte[] data, int index)
    {
        //大端模式 返回的值是 传入的data数组从index开始的4个字节的int值
        return (data[index] << 0x18) | (data[index + 1] << 0x10) | (data[index + 2] << 8) | (data[index + 3]);
    }

    /// <summary>
    /// 被动关闭连接
    /// </summary>
    private void PassiveDisconnection()
    {
        try
        {
            mSocket?.Shutdown(SocketShutdown.Both); //停止数据发送和接收，确保正常关闭连接。
            mSocket?.Close(); //关闭 Socket 并释放其资源
            //mSocket?.Dispose();                     //释放 Socket 占用的所有资源，特别是非托管资源。（Close已经隐式调用了）
        }
        catch (System.Exception e)
        {
            Error?.Invoke(e.Message);
        }
        finally
        {
            mSocket = null;
        }

        mSocket = null;

        //并且向上传递消息断开信息
        if (isStart)
        {
            Disconnected?.Invoke();
        }
    }

    /// <summary>
    /// 主动关闭连接
    /// </summary>
    public void ActiveClose()
    {
        isStart = false;
        PassiveDisconnection();
    }

    public void Start()
    {
        if (mSocket != null && !isStart)
        {
            mSocket.BeginReceive(mBuffer, mOffect, mSize - mOffect, SocketFlags.None, new AsyncCallback(OnReceive),
                null);
            isStart = true;
        }
    }

    private void OnReceive(IAsyncResult ar)
    {
        try
        {
            int len = 0;
            if (mSocket != null && mSocket.Connected)
            {
                len = mSocket.EndReceive(ar);
            }

            // 消息长度为0，代表连接已经断开
            if (len == 0)
            {
                PassiveDisconnection();
                return;
            }

            //处理信息
            ReadMessage(len);

            // 继续接收数据
            if (mSocket != null && mSocket.Connected)
            {
                mSocket.BeginReceive(mBuffer, mOffect, mSize - mOffect, SocketFlags.None, new AsyncCallback(OnReceive),
                    null);
            }
            else
            {
                Log.Information("[LengthFieldDecoder]Socket 已断开连接，无法继续接收数据。");
                PassiveDisconnection();
            }
        }
        catch (ObjectDisposedException e)
        {
            // Socket 已经被释放
            Log.Information("[LengthFieldDecoder:ObjectDisposedException]");
            Log.Information(e.ToString());
            PassiveDisconnection();
        }
        catch (SocketException e)
        {
            //打印一下异常，并且断开与客户端的连接
            Log.Information("[[LengthFieldDecoder:SocketException]");
            Log.Information(e.ToString());
            PassiveDisconnection();
        }
        catch (Exception e)
        {
            //打印一下异常，并且断开与客户端的连接
            Log.Information("[LengthFieldDecoder:Exception]");
            Log.Information(e.ToString());
            PassiveDisconnection();
        }
    }
    
    /// <summary>
    /// 处理数据，并且进行转发处理
    /// </summary>
    /// <param name="len"></param>
    private void ReadMessage(int len)
    {
        //headLen+bodyLen=totalLen

        int headLen = lengthFieldOffset + lengthFieldLength;//魔法值+长度字段的长度
        int adj = lengthAdjustment; //body偏移量

        //循环开始之前mOffect代表上次剩余长度
        //所以moffect需要加上本次送过来的len
        mOffect += len;

        //循环解析
        while (true)
        {
            //此时缓冲区内有moffect长度的字节需要去处理

            //如果未处理的数据超出缓冲区大小限制
            if (mOffect > mSize)
            {
                throw new IndexOutOfRangeException("数据超出限制");
            }
            if (mOffect < headLen)
            {
                //接收的数据不够一个完整的包，继续接收
                return;
            }

            //获取body长度，通过大端模式
            //int bodyLen = BitConverter.ToInt32(mBuffer, lengthFieldOffset);
            int bodyLen = GetInt32BE(mBuffer, lengthFieldOffset);

            //判断body够不够长
            if (mOffect < headLen + adj + bodyLen)
            {
                //接收的数据不够一个完整的包，继续接收
                return;
            }

            //整个包的长度为
            int total = headLen + bodyLen;

            //获取数据
            byte[] data = new byte[bodyLen];
            Array.Copy(mBuffer, headLen, data, 0, bodyLen);

            //数据解析完毕就需要更新buffer缓冲区
            Array.Copy(mBuffer, bodyLen+ headLen, mBuffer, 0, mOffect- total);
            mOffect = mOffect - total;

            //完成一个数据包
            Received?.Invoke(data);
        }

    }
}