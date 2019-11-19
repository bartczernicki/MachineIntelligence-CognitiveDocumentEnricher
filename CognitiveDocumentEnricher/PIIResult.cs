using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CognitiveDocumentEnricher
{
    public class PIIResult
    {
        public PIIResult()
        { }

        public List<string> Emails { get; set; }

        public List<string> Addresses { get; set; }

        public List<string> PhoneNumbers { get; set; }

        public List<string> SSNs { get; set; }
    }
}
