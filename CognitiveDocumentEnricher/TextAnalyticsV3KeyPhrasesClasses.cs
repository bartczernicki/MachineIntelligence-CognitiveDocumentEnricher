using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CognitiveDocumentEnricher.CognitiveServiceClasses.KeyPhrases
{
    public class TextAnalyticsV3KeyPhrases
    {
        public List<Document> documents { get; set; }
        public List<object> errors { get; set; }
        public string modelVersion { get; set; }
    }

    public class Document
    {
        public string id { get; set; }
        public List<string> keyPhrases { get; set; }
    }
}
