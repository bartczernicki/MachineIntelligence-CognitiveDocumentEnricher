using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CognitiveDocumentEnricher.CognitiveServiceClasses.Entities
{
    public class TextAnalyticsV3Entities
    {
        public List<Document> documents { get; set; }
        public List<object> errors { get; set; }
        public string modelVersion { get; set; }
    }

    public class Entity
    {
        public string text { get; set; }
        public string type { get; set; }
        public int offset { get; set; }
        public int length { get; set; }
        public double score { get; set; }
        public string subtype { get; set; }
    }

    public class Document
    {
        public string id { get; set; }
        public List<Entities.Entity> entities { get; set; }
    }
}
