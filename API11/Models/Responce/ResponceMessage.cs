using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace API11.Models.Responce
{
    public class ResponseMessage
    {

        public List<Choice> choices { get; set; }
        public int created { get; set; }
        public string model { get; set; }
        public string @object { get; set; }
        public Usage usage { get; set; }

        public class ResponseMessageContent
        {
            public string role { get; set; }
            public string content { get; set; }
            public string functions_state_id { get; set; }
        }
        public class Usage
        {
            public int completion_tokens { get; set; }
            public int promt_tokens { get; set; }
            public int system_tokens { get; set; }
            public int total_tokens { get; set; }
        }

        public class Choice
        {
            public string finish_reason { get; set; }
            public int index { get; set; }
            public ResponseMessageContent message { get; set; }
        }
    }
}
