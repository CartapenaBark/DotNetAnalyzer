// SEC004: 不安全反序列化测试样本
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Web.Script.Serialization;

namespace SecurityTestAssets.UnsafeDeserialization
{
    public class BadExamples
    {
        public object DeserializeData(Stream stream)
        {
            // SEC004: BinaryFormatter 不安全
            var formatter = new BinaryFormatter();
            return formatter.Deserialize(stream);
        }

        public object DeserializeSoap(Stream stream)
        {
            // SEC004: SoapFormatter 不安全
            var formatter = new SoapFormatter();
            return formatter.Deserialize(stream);
        }

        public T DeserializeNetDataContract<T>(Stream stream)
        {
            // SEC004: NetDataContractSerializer 不安全
            var serializer = new NetDataContractSerializer();
            return (T)serializer.ReadObject(stream);
        }
    }

    public class GoodExamples
    {
        public T DeserializeData<T>(Stream stream)
        {
            // 安全: 使用 System.Text.Json
            return System.Text.Json.JsonSerializer.Deserialize<T>(stream)!;
        }
    }
}
