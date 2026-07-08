

namespace VBaseFramework.Core.Entitys
{
    public class SeekPageResponse<T> : Response<List<T>>
    {

    }

    public class Response<T>
    {
        public T Data { get; set; }
    }


    public class ErrerResponse<T> : ErrerResponse
    {
        public T ErrerData { get; set; }
    }

    public class ErrerResponse
    {
        public int ErrerCode { get; set; }
        public string ErrerMessage { get; set; }

    }
}