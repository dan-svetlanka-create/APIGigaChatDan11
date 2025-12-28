using System;
using System.Collections.Generic;
using System.Text;

namespace API5
{
    public class Request
    {
        public string model { get; set; }
        public List<Message> messages { get; set; }
        public string function_call { get; set; }
        public double temperature { get; set; }
        public int max_tokens { get; set; }
    }

    public class Message
    {
        public string role { get; set; }
        public string content { get; set; }
    }
}
