using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace API11.Models
{
    public class Request
    {
        public string model { get; set; }
        public List<Message> messages { get; set; }
        public string function_call { get; set; }
        public double temperature { get; set; }
        public int max_tokens { get; set; }

        public class Message
        {
            public string role { get; set; }
            public string content { get; set; }
        }
    }
}
