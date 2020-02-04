using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CognitiveDocumentEnricher
{
    public class SentimentV3Response
    {
        public IList<DocumentSentiment> Documents { get; set; }

        public IList<ErrorRecord> Errors { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public RequestStatistics Statistics { get; set; }
    }

    public class TextAnalyticsBatchInput
    {
        public IList<TextAnalyticsInput> Documents { get; set; }
    }

    public class TextAnalyticsBatchInputV3
    {
        public IList<TextAnalyticsInputV3> Documents { get; set; }
    }

    public class TextAnalyticsInput
    {
        /// <summary>
        /// A unique, non-empty document identifier.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The input text to process.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// The language code. Default is english ("en").
        /// </summary>
        public string LanguageCode { get; set; } = "en";
    }

    public class TextAnalyticsInputV3
    {
        /// <summary>
        /// A unique, non-empty document identifier.
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// The input text to process.
        /// </summary>
        public string text { get; set; }

        /// <summary>
        /// The language code. Default is english ("en").
        /// </summary>
        public string language { get; set; } = "en";
    }

    public class DocumentSentiment
    {
        public DocumentSentiment(
            string id,
            DocumentSentimentLabel sentiment,
            SentimentConfidenceScoreLabel documentSentimentScores,
            IEnumerable<SentenceSentiment> sentencesSentiment)
        {
            Id = id;

            Sentiment = sentiment;

            DocumentScores = documentSentimentScores;

            Sentences = sentencesSentiment;
        }

        /// <summary>
        /// A unique, non-empty document identifier.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Predicted sentiment for document (Negative, Neutral, Positive, or Mixed).
        /// </summary>
        public DocumentSentimentLabel Sentiment { get; set; }

        /// <summary>
        /// Document level sentiment confidence scores for each sentiment class.
        /// </summary>
        public SentimentConfidenceScoreLabel DocumentScores { get; set; }

        /// <summary>
        /// Sentence level sentiment analysis.
        /// </summary>
        public IEnumerable<SentenceSentiment> Sentences { get; set; }
    }

    public enum DocumentSentimentLabel
    {
        Positive,

        Neutral,

        Negative,

        Mixed
    }

    public enum SentenceSentimentLabel
    {
        Positive,

        Neutral,

        Negative
    }

    public class SentimentConfidenceScoreLabel
    {
        public double Positive { get; set; }

        public double Neutral { get; set; }

        public double Negative { get; set; }
    }

    public class ErrorRecord
    {
        /// <summary>
        /// The input document unique identifier that this error refers to.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The actual error message.
        /// </summary>
        public string Message { get; set; }
    }

    public class SentenceSentiment
    {
        /// <summary>
        /// The predicted Sentiment for the sentence.
        /// </summary>
        public SentenceSentimentLabel Sentiment { get; set; }

        /// <summary>
        /// The sentiment confidence score for the sentence for all classes.
        /// </summary>
        public SentimentConfidenceScoreLabel SentenceScores { get; set; }

        /// <summary>
        /// The sentence offset from the start of the document.
        /// </summary>
        public int Offset { get; set; }

        /// <summary>
        /// The sentence length as given by StringInfo's LengthInTextElements property.
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// The warnings generated for the sentence.
        /// </summary>
        public string[] Warnings { get; set; }
    }

    public class RequestStatistics
    {
        /// <summary>
        /// Number of documents submitted in the request.
        /// </summary>
        public int DocumentsCount { get; set; }

        /// <summary>
        /// Number of valid documents. This excludes empty, over-size limit or non-supported languages documents.
        /// </summary>
        public int ValidDocumentsCount { get; set; }

        /// <summary>
        /// Number of invalid documents. This includes empty, over-size limit or non-supported languages documents.
        /// </summary>
        public int ErroneousDocumentsCount { get; set; }

        /// <summary>
        /// Number of transactions for the request.
        /// </summary>
        public long TransactionsCount { get; set; }
    }
}
