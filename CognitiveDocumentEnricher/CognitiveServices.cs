using Microsoft.Azure.CognitiveServices.ContentModerator;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics.Models;
using Microsoft.Azure.CognitiveServices.Search.EntitySearch;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace CognitiveDocumentEnricher
{
    public static class CognitiveServices
    {
        /// <summary>
        /// Bing Entity Search - Entities
        /// </summary>
        /// <param name="entities"></param>
        /// <returns></returns>
        public static List<BingEntityData> BingEntities(List<string> entities)
        {
            // set up main search client
            var entityClient = new EntitySearchClient(new ApiKeyServiceClientCredentialsEntitySearch());

            var entityDataSet = new List<BingEntityData>();

            Parallel.ForEach(entities,
                new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 2.0)) },
                (entity) =>
                //foreach (var entity in entities)
                {
                    var entityData = entityClient.Entities.Search(query: entity);
                    var description = "NOTFOUND";
                    var taxonomy = "NOTFOUND";

                    // 1) Lookup main entities
                    if (entityData?.Entities?.Value?.Count > 0)
                    {
                        // Find the entity that represents the dominant one
                        var mainEntity = entityData.Entities.Value.Where(thing => thing.EntityPresentationInfo.EntityScenario == Microsoft.Azure.CognitiveServices.Search.EntitySearch.Models.EntityScenario.DominantEntity).FirstOrDefault();

                        // If null, use disambiguaation item
                        if (mainEntity == null)
                        {
                            mainEntity = entityData.Entities.Value.Where(thing => thing.EntityPresentationInfo.EntityScenario == Microsoft.Azure.CognitiveServices.Search.EntitySearch.Models.EntityScenario.DisambiguationItem).FirstOrDefault();
                        }
                        Console.WriteLine("\t\t" + entity);

                        // Extract key properties
                        description = mainEntity == null ? "NOTFOUND" : mainEntity.Description;
                        taxonomy = mainEntity == null ? "NOTFOUND" : (mainEntity.EntityPresentationInfo.EntityTypeHints != null) ?
                            mainEntity.EntityPresentationInfo.EntityTypeHints.FirstOrDefault() :
                                (mainEntity.EntityPresentationInfo == null) ? "NOTFOUND" : mainEntity.EntityPresentationInfo.EntityTypeDisplayHint;

                        // Workaround for some entities being labeled as movies
                        if (taxonomy == "Movie" || taxonomy == "MusicRecording")
                        {
                            taxonomy = "Generic";
                            description = string.Empty;
                        }
                    }
                    // 2) Lookup places
                    else if (entityData?.Places?.Value?.Count > 0)
                    {
                        // Find the entity that represents the dominant one
                        var mainEntity = entityData.Places.Value.Where(place => place.EntityPresentationInfo.EntityScenario == Microsoft.Azure.CognitiveServices.Search.EntitySearch.Models.EntityScenario.ListItem).FirstOrDefault();

                        // If null, use disambiguaation item
                        if (mainEntity == null)
                        {
                            mainEntity = entityData.Places.Value.Where(place => place.EntityPresentationInfo.EntityScenario == Microsoft.Azure.CognitiveServices.Search.EntitySearch.Models.EntityScenario.DisambiguationItem).FirstOrDefault();
                        }

                        // Extract key properties
                        description = mainEntity == null ? "NOTFOUND" : mainEntity.Description;
                        taxonomy = mainEntity == null ? "NOTFOUND" : (mainEntity.EntityPresentationInfo.EntityTypeHints != null) ?
                            mainEntity.EntityPresentationInfo.EntityTypeHints.FirstOrDefault() :
                                (mainEntity.EntityPresentationInfo == null) ? "NOTFOUND" : mainEntity.EntityPresentationInfo.EntityTypeDisplayHint;
                    }


                    entityDataSet.Add(
                        new BingEntityData()
                        {
                            EntityName = entity,
                            Description = description,
                            Taxony = taxonomy
                        }
                    );
                } // EOF foreach
            );


            return entityDataSet;
        }

        /// <summary>
        /// Text Analytics - V2 - Key Phrases & Entities
        /// </summary>
        /// <param name="keyPhrasesSamples"></param>
        /// <returns></returns>
        public static Tuple<KeyPhraseBatchResult, EntitiesBatchResult> TextAnalyticsKeyPhrasesAndEntities(List<KeyValuePair<string, string>> keyPhrasesSamples)
        {
            var creds = new ApiKeyServiceClientCredentials();

            // Build client API call
            ITextAnalyticsClient client = new TextAnalyticsClient(creds)
            {
                Endpoint = Config.COGNITIVE_SERVICES_REGION_TEXT_ANALYTICS
            };

            // Getting key-phrases
            var lengthofText = keyPhrasesSamples.Select((v, i) => v.Value.ToString().Length).Sum();
            //Console.WriteLine(string.Format("\tDocs: {0}", keyPhrasesSamples.Count));
            Console.WriteLine(string.Format("\tCharacters: {0}", lengthofText));

            var multiLanguageInputs = (keyPhrasesSamples.Select((v, i) => new MultiLanguageInput(v.Key, i.ToString(), v.Value)).ToList());
            var multiLanguageInputsString = String.Join(string.Empty, multiLanguageInputs.Select(a => a.Text).ToList());
            //Console.WriteLine("OCR Text Sent for key phrases: " + Math.Round(mb, 3));

            // Send batches of 100 inputs
            int batches = multiLanguageInputs.Count / 100 + 1;

            var test = new Tuple<KeyPhraseBatchResult, EntitiesBatchResult>(null, null);
            ;
            var keyPhraseBatchResults = new List<KeyPhraseBatchResult>();
            var entityBatchResults = new List<EntitiesBatchResult>();

            for (int i = 0; i != batches; i++)
            {
                // set up the batches
                var multiLanguageInputsToProcess = multiLanguageInputs.Skip(i * 100).Take(100).ToList();

                if (multiLanguageInputsToProcess.Count > 0)
                {
                    var multiLanguageBatch = new MultiLanguageBatchInput(multiLanguageInputsToProcess);

                    Console.WriteLine(string.Format("\tProcessing Batch {0} of {1}", (i + 1), batches));

                    // key phrases result
                    var keyPhraseMiniBatchResult = client.KeyPhrasesAsync(true,
                        new MultiLanguageBatchInput(multiLanguageInputsToProcess)).Result;
                    keyPhraseBatchResults.Add(keyPhraseMiniBatchResult);

                    var entitiesMiniBatchResult = client.EntitiesAsync(true,
                        new MultiLanguageBatchInput(multiLanguageInputsToProcess)).Result;
                    entityBatchResults.Add(entitiesMiniBatchResult);
                }
            }

            var keyPhraseDocuments = keyPhraseBatchResults.SelectMany(i => i.Documents).ToList();
            var keyPhraseErrors = keyPhraseBatchResults.SelectMany(i => i.Errors).ToList();
            var keyPhraseBatchResult = new KeyPhraseBatchResult(keyPhraseDocuments, keyPhraseErrors);

            var entitiesDcouments = entityBatchResults.SelectMany(i => i.Documents).ToList();
            var entitiesErrors = entityBatchResults.SelectMany(i => i.Errors).ToList();
            var entitiesBatchResult = new EntitiesBatchResult(entitiesDcouments, entitiesErrors);

            //var tuple = (KeyPhraseBatchResult: keyPhraseBatchResult, EntitiesBatchResult: entitiesBatchResult);

            return new Tuple<KeyPhraseBatchResult, EntitiesBatchResult>(keyPhraseBatchResult, entitiesBatchResult);
        }

        /// <summary>
        /// Text Analytics - V2 - PII Result
        /// </summary>
        /// <param name="ocrPhrasesSamples"></param>
        /// <returns></returns>
        public static PIIResult TextAnalyticsPIIResultV2(List<KeyValuePair<string, string>> ocrPhrasesSamples)
        {
            var contentModeratorClient = new ContentModeratorClient(new ApiKeyServiceClientCredentialsContentModerator())
            {
                Endpoint = Config.COGNITIVE_SERVICES_REGION_TEXT_ANALYTICS
            };

            var textInputs = (ocrPhrasesSamples.Select((v, i) => v.ToString()).ToList());

            var emails = new List<string>();
            var phoneNumbers = new List<string>();
            var addresses = new List<string>();
            var ssnNumbers = new List<string>();

            foreach (var textInput in textInputs)
            {
                // Content Moderator needs strings max at 1024 chararacters
                var textsToModerate = Helpers.SplitAndPadFourtyChars(textInput, 980);

                foreach (var truncatedTextForModerator in textsToModerate)
                {
                    //truncatedTextForModerator = truncatedTextForModerator.Trim();

                    if (truncatedTextForModerator.Trim() != string.Empty)
                    {
                        var result = contentModeratorClient.TextModeration.ScreenText("text/plain",
                            new MemoryStream(Encoding.UTF8.GetBytes(truncatedTextForModerator)),
                                string.Empty, true, true, null, true);

                        // Check if PII is not NULL
                        if (!(result.PII is null))
                        {
                            var emailsToAdd = result.PII.Email.Select(a => a.Text).ToList();
                            emails.AddRange(emailsToAdd);
                            var phoneNumbersToAdd = result.PII.Phone.Select(a => a.Text).ToList();
                            phoneNumbers.AddRange(phoneNumbersToAdd);
                            var addressesToAdd = result.PII.Address.Select(a => a.Text).ToList();
                            addresses.AddRange(addressesToAdd);
                            var ssnNumbersToAdd = result.PII.SSN.Select(a => a.Text).ToList();
                            ssnNumbers.AddRange(ssnNumbersToAdd);
                        }
                    }
                }
            }

            var piiResult = new PIIResult
            {
                Emails = emails,
                Addresses = addresses,
                PhoneNumbers = phoneNumbers,
                SSNs = ssnNumbers
            };

            return piiResult;
        }

        /// <summary>
        /// Text Analytics - V3 - Sentiment Analysis - Preview
        /// </summary>
        /// <param name="documents"></param>
        /// <returns></returns>
        public static async Task<SentimentV3Response> TextAnalyticsSentimentAnalysisV3PreviewAsync(List<TextAnalyticsInput> documents)
        {
            var inputDocuments = new TextAnalyticsBatchInput()
            {
                Documents = documents
            };

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", Config.COGNITIVE_SERVICES_KEY);

                var httpContent = new StringContent(JsonConvert.SerializeObject(inputDocuments), Encoding.UTF8, "application/json");

                var httpResponse = await httpClient.PostAsync(new Uri(Config.COGNITIVE_SERVICES_REGION_TEXT_ANALYTICS_SENTIMENTv3), httpContent);
                var responseContent = await httpResponse.Content.ReadAsStringAsync();

                if (!httpResponse.StatusCode.Equals(HttpStatusCode.OK) || httpResponse.Content == null)
                {
                    throw new Exception(responseContent);
                }

                return JsonConvert.DeserializeObject<SentimentV3Response>(responseContent, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });
            }
        }

        /// <summary>
        /// Vision - OCR - BatchRead from URI
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="apiVersion"></param>
        /// <returns></returns>
        public static async Task<Tuple<string, OCRObjectResult>> VisionOCRResultBatchReadAsync(string uri, string apiVersion)
        {
            // Computer Vision URL
            var urlString = "https://" + Config.COGNITIVE_SERVICES_REGION + ".api.cognitive.microsoft.com/vision/" + apiVersion + "/read/core/asyncBatchAnalyze";
            // return variables
            var responseContent = string.Empty;
            OCRObjectResult ocrObject = null;
            string ocrResultString = string.Empty;

            using (var httpClient = new HttpClient())
            {
                // Setup HttpClient
                httpClient.BaseAddress = new Uri(urlString);

                // Request headers
                httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", Config.COGNITIVE_SERVICES_KEY);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Request body
                var uriBody = "{\"url\":\"" + uri + "\"}";

                using (var content = new StringContent(uriBody))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                    // Make request with resiliency policy (can retry http requests)
                    var resilienyStrategy = CognitiveServicesRetryPolicy.DefineAndRetrieveResiliencyStrategy();
                    var response = await resilienyStrategy.ExecuteAsync(() => httpClient.PostAsync(urlString, content));

                    // read response and write to view
                    // batch call, you need to wait for the "job" to finish
                    if (response.IsSuccessStatusCode)
                    {
                        var urlLocation = response.Headers.GetValues("Operation-Location").FirstOrDefault();
                        var resilienyStrategyBatchJob = CognitiveServicesRetryPolicy.DefineAndRetrieveResiliencyStrategyForBatchJob();

                        var ocrResultResponse = await resilienyStrategyBatchJob.ExecuteAsync(() => httpClient.GetAsync(urlLocation));

                        if (ocrResultResponse.IsSuccessStatusCode)
                        {
                            ocrResultString = await ocrResultResponse.Content.ReadAsStringAsync();
                            if (string.IsNullOrEmpty(ocrResultString))
                            {
                                var test = ocrResultString;
                            }
                        }

                        ocrObject = JsonConvert.DeserializeObject<OCRObjectResult>(ocrResultString);
                    }
                    else
                    {
                        throw new Exception(response.StatusCode + " : " + response.ReasonPhrase);
                    }
                }
            }

            return new Tuple<string, OCRObjectResult>(ocrResultString, ocrObject);
        }

        /// <summary>
        /// Vision - OCR - BatchRead from File Path -> Byte Array
        /// </summary>
        /// <param name="imageFilePath"></param>
        /// <param name="apiVersion"></param>
        /// <returns></returns>
        public static async Task<Tuple<string, OCRObjectResult>> VisionOCRResultBatchReadFromImageAsync(string imageFilePath, string apiVersion)
        {
            // Computer Vision URL
            var urlString = "https://" + Config.COGNITIVE_SERVICES_REGION + ".api.cognitive.microsoft.com/vision/" + apiVersion + "/read/core/asyncBatchAnalyze";
            // return variables
            var responseContent = string.Empty;
            OCRObjectResult ocrObject = null;
            string ocrResultString = string.Empty;

            using (var httpClient = new HttpClient())
            {
                // Setup HttpClient
                httpClient.BaseAddress = new Uri(urlString);

                // Request headers
                httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", Config.COGNITIVE_SERVICES_KEY);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Reads the contents of the specified local image into a byte array.
                byte[] byteData = Util.GetImageAsByteArray(imageFilePath);

                // Adds the byte array as an octet stream to the request body.
                using (ByteArrayContent content = new ByteArrayContent(byteData))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                    // Make request with resiliency policy (can retry http requests)
                    var resilienyStrategy = CognitiveServicesRetryPolicy.DefineAndRetrieveResiliencyStrategy();
                    var response = await resilienyStrategy.ExecuteAsync(() => httpClient.PostAsync(urlString, content));

                    // read response and write to view
                    // batch call, you need to wait for the "job" to finish
                    if (response.IsSuccessStatusCode)
                    {
                        var urlLocation = response.Headers.GetValues("Operation-Location").FirstOrDefault();
                        var resilienyStrategyBatchJob = CognitiveServicesRetryPolicy.DefineAndRetrieveResiliencyStrategyForBatchJob();

                        var ocrResultResponse = await resilienyStrategyBatchJob.ExecuteAsync(() => httpClient.GetAsync(urlLocation));

                        if (ocrResultResponse.IsSuccessStatusCode)
                        {
                            ocrResultString = await ocrResultResponse.Content.ReadAsStringAsync();
                            if (string.IsNullOrEmpty(ocrResultString))
                            {
                                var test = ocrResultString;
                            }
                        }

                        ocrObject = JsonConvert.DeserializeObject<OCRObjectResult>(ocrResultString);
                    }
                    else
                    {
                        throw new Exception(response.StatusCode + " : " + response.ReasonPhrase);
                    }
                }
            }

            return new Tuple<string, OCRObjectResult>(ocrResultString, ocrObject);
        }
    }
}
