using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CognitiveDocumentEnricher
{
    public class DocumentEntity : TableEntity
    {
        public DocumentEntity(string category, string documentName)
        {
            this.PartitionKey = category;
            this.RowKey = documentName;
        }

        public DocumentEntity() { }

        public string DocumentType { get; set; }

        public int TextSize { get; set; }

        public int Pages { get; set; }

        public int CognitiveServicesApiCallsApiCallCount { get; set; }

        public int CognitiveServicesApiCallsApiCallV2Count { get; set; }

        public int CognitiveServicesApiCallsApiCallV3Count { get; set; }

        public int CognitiveServicesApiCallsTotalCount { get; set; }

        public string Uri { get; set; }

        public string OcrResult { get; set; }

        public string TextAnalyticsKeyPhraseResult { get; set; }

        public string TextAnalyticsDistinctKeyPhraseResult { get; set; }

        public string TextAnalyticsEntitiesResult { get; set; }

        public string TextAnalyticsEntitiesTaxonomiesResult { get; set; }

        public string TextAnalyticsDistinctEntititesResult { get; set; }

        public long DocumentSizeInBytes { get; set; }

        public int PIIEmailsCount { get; set; }

        public int PIIAddressesCount { get; set; }

        public int PIIPhoneNumbersCount { get; set; }

        public int PIISSNSCount { get; set; }

        public string SentimentAnalysis { get; set; }
    }
}
