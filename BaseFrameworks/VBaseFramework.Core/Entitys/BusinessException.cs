using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VBaseFramework.Core.Entitys
{
    public class BusinessException<T> : BusinessException
    {
        public BusinessException(ErrerResponse<T> error) : base(error)
        {
        }

        public BusinessException(int code, string message, T data)
        {
            Error = new ErrerResponse<T>() { ErrerCode = code, ErrerMessage = message, ErrerData = data };
        }

    }
    public class BusinessException : Exception
    {
        public ErrerResponse Error { get; set; }
        public BusinessException(ErrerResponse error)
        {
            Error = error;
        }

        public BusinessException(int code, string message)
        {
            Error = new ErrerResponse() { ErrerCode = code, ErrerMessage = message };
        }

        public BusinessException(int code, string message, Exception e) : base($"{code}-{message}", e)
        {
            Error = new ErrerResponse() { ErrerCode = code, ErrerMessage = message };
        }


        protected BusinessException()
        {
        }
    }
}
