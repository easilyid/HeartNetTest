using System.Reflection;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Serilog;

namespace Common;

/// <summary>
/// Protobuf序列化与反序列化
/// </summary>
public class ProtoHelper
{
    /// <summary>
    /// 字典用于保存message的所有类型，用于拆包时进行类型转换
    /// key：message的全限定名， value：message的类型
    /// </summary>
    private static Dictionary<string, Type> _registry = new Dictionary<string, Type>();

    /// <summary>
    /// 用于保存协议类型和协议id的映射关系
    /// </summary>
    private static Dictionary<int, Type> mDict1 = new Dictionary<int, Type>();

    private static Dictionary<Type, int> mDict2 = new Dictionary<Type, int>();

    /// <summary>
    /// 根据类型获取协议在中网络传输的id值
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static int SeqCode(Type type)
    {
        return mDict2[type];
    }

    /// <summary>
    /// 根据协议在中网络传输的id值获取协议的类型
    /// </summary>
    /// <param name="code"></param>
    /// <returns></returns>
    public static Type SeqType(int code)
    {
        return mDict1[code];
    }

    /// <summary>
    /// 用静态代码块加载注册
    /// </summary>
    static ProtoHelper()
    {
        List<string> list = new List<string>();
        //  LINQ 查询语法，获取当前正在执行的程序集中的所有类型。
        //var q = from t in Assembly.GetExecutingAssembly().GetTypes() select t;
        var q = Assembly.GetExecutingAssembly().GetTypes();

        q.ToList().ForEach(t =>
        {
            if (typeof(IMessage).IsAssignableFrom(t))
            {
                //t现在是IMessage的子类 也就是我们的协议类  Descriptor是协议的描述信息 FullName是协议的全限定名
                var desc = t.GetProperty("Descriptor").GetValue(t) as MessageDescriptor;
                _registry.Add(desc.FullName, t);
                list.Add(desc.FullName);
            }
        });

        //根据协议名的字符串进行排序
        list.Sort((x, y) =>
        {
            //根据字符串长度排序
            if (x.Length != y.Length)
            {
                //长度不同 按照长度排序 长度小的在前 长度大的在后
                return x.Length - y.Length;
            }

            //如果长度相同
            //则使用x和y基于 Unicode码点值的排序规则进行字符串比较，x<y就返回负数 x>y就返回正数
            return string.Compare(x, y, StringComparison.Ordinal);
        });

        for (int i = 0; i < list.Count; i++)
        {
            var fname = list[i];
            //Log.Debug("Proto类型注册：{0}  {1}", i,fname);
            var t = _registry[fname];
            mDict1.Add(i, t);
            mDict2.Add(t, i);
        }

        Log.Debug("[ProtoHelper]共加载proto协议为:{0}", list.Count);
    }

    /// <summary>
    /// 根据协议在中网络传输的id值解析成一个IMessage
    /// typeCode是协议的id
    /// data是协议的数据
    /// offset是偏移量
    /// len是长度
    /// </summary>
    public static IMessage ParseFrom(int typeCode, byte[] data, int offset, int len)
    {
        Type t = ProtoHelper.SeqType(typeCode);
        var desc = t.GetProperty("Descriptor").GetValue(t) as MessageDescriptor;
        var msg = desc.Parser.ParseFrom(data, 2, data.Length - 2);
        return msg;
    }

    //初始化
    public static void Init()
    {
    }
}