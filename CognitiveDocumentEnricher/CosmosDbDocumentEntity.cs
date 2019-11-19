using Microsoft.Azure.CosmosDB.Table;

namespace CognitiveDocumentEnricher
{
    public class CosmosDbDocumentEntity : TableEntity
    {
        public CosmosDbDocumentEntity(string category, string documentName)
        {
            this.PartitionKey = category;
            this.RowKey = documentName;
        }

        public CosmosDbDocumentEntity() { }

        public string Uri { get; set; }

        public string TextOcrResult { get; set; }

        public string KeyPhraseResult { get; set; }

        public string DistinctKeyPhraseResult { get; set; }

        public int TextSize { get; set; }

        public int Pages { get; set; }

        public string DocumentType { get; set; }

        public long DocumentSizeInBytes { get; set; }
    }
}

