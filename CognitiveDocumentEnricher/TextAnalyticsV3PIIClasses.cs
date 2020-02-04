using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CognitiveDocumentEnricher.CognitiveServiceClasses.PII
{
    public class Entity
    {
        public string text { get; set; }
        public string type { get; set; }
        public string subtype { get; set; }
        public int offset { get; set; }
        public int length { get; set; }
        public double score { get; set; }
    }

    public class Document
    {
        public string id { get; set; }
        public List<PII.Entity> entities { get; set; }
    }

    public class TextAnalyticsV3PII
    {
        public List<Document> documents { get; set; }
        public List<object> errors { get; set; }
        public string modelVersion { get; set; }
    }
}
