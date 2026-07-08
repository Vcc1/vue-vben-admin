//using Infrastructure.HttpApi.ChatGPT;
using MengTuDbConfig.Client.Model;

namespace MengTuDbConfig.Client
{
    public class Result<T>
    {
        public bool IsSuccess { get; set; } = true;


        public string Msg { get; set; } = Message.正确返回;


        public T Data { get; set; }
    }
}
