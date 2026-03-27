
namespace VBaseFramework.Api.Core
{
    [Serializable]
    internal class VBaseException : Exception
    {
        public int Code { get; set; }
        public VBaseException(int code, string message) : base(message)
        {
        }

        public VBaseException(int code, string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}