using Microsoft.WindowsAzure.Storage.Auth;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics.Models;
using WindowsAzureTable = Microsoft.WindowsAzure.Storage.Table;
using WindowsAzureStorage = Microsoft.WindowsAzure.Storage;
using CosmosDbStorage = Microsoft.Azure.Storage;
using CosmosDbTable = Microsoft.Azure.CosmosDB.Table;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents;
using System.Drawing.Drawing2D;
using Microsoft.Azure.CognitiveServices.ContentModerator;
using Microsoft.Azure.CognitiveServices.Search.EntitySearch;
using System.Net;

namespace CognitiveDocumentEnricher
{
    static class Util
    {
        // Blob Storage Account
        public static WindowsAzureStorage.CloudStorageAccount BlobStorageAccount = new WindowsAzureStorage.CloudStorageAccount(
            new StorageCredentials(Config.STORAGE_ACCOUNT_NAME, Config.STORAGE_ACCOUNT_KEY),
            true);
        // CosmosDB Storage Account
        // public static Microsoft.Azure.Storage.CloudStorageAccount CosmosDbStorageAccount = CosmosDbStorage.CloudStorageAccount.Parse(Config.COSMOSDB_CONNECTIONSTRING);
        // CosmosDB Storage Account - Scoring Table
        // public static Microsoft.Azure.Storage.CloudStorageAccount CosmosDbStorageAccountScoringTable = CosmosDbStorage.CloudStorageAccount.Parse(Config.COSMOSDB_CONNECTIONSTRING_DOCUMENTSCORING);

        public static double ConvertBytesToMegabytes(long bytes)
        {
            double megaBytes = (bytes / 1024f) / 1024f;
            return megaBytes;
        }

        public static List<string> DirectoryTraverseForFiles(string sDir)
        {
            var files = new List<string>();

            try
            {
                foreach (string d in Directory.GetDirectories(sDir))
                {
                    foreach (string f in Directory.GetFiles(d))
                    {
                        files.Add(f);
                    }

                    DirectoryTraverseForFiles(d);
                }
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return files;
        }

        //public static AzureRegions GetRegion(string region)
        //{
        //    AzureRegions regionEnum;
        //    if (Enum.TryParse(region, true/*ignore case*/, out regionEnum) && Enum.IsDefined(typeof(AzureRegions), regionEnum))
        //    {
        //        return regionEnum;
        //    }
        //    else
        //    {
        //        string msg = $"'{region}' is not supported by the SDK yet. Please move the service to the following locations" + Environment.NewLine;
        //        msg += string.Join(", ", Enum.GetNames(typeof(AzureRegions)));
        //        throw new Exception(msg);
        //    }
        //}

        public static async Task<string> OCRResult(MemoryStream fileStream)
        {
            var urlString = "https://southcentralus.api.cognitive.microsoft.com/customvision/v1.1/Prediction/604fc598-0aee-42e0-9da1-f9935a239bad/image?iterationId=b86127d7-07c3-43ea-b12c-dc6c7ca21fde";
            var responseContent = string.Empty;

            HttpContent content = new StreamContent(fileStream);
            content.Headers.ContentType = new MediaTypeWithQualityHeaderValue("application/octet-stream");

            using (var httpClient = new HttpClient())
            {
                // Setup HttpClient
                httpClient.BaseAddress = new Uri(urlString);
                httpClient.DefaultRequestHeaders.Add("Prediction-Key", "b95022323dfc470fa05ac6ecd67b0663");
                // httpClient.DefaultRequestHeaders.Add("Content-Type", "application/octet-stream");
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

                // Make request
                // var response = await httpClient.PostAsync(urlString, content);

                // Make request with resiliency policy (can retry http requests)
                var resilienyStrategy = CognitiveServicesRetryPolicy.DefineAndRetrieveResiliencyStrategy();
                var response = await resilienyStrategy.ExecuteAsync(() => httpClient.PostAsync(urlString, content));
                //read response and write to view
                responseContent = await response.Content.ReadAsStringAsync();
            }

            return responseContent;
        }

        public static List<BingEntityData> GetEntitiesSearchResponse(List<string> entities)
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

        public static PIIResult GetPIIResponse(List<KeyValuePair<string, string>> ocrPhrasesSamples)
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

                foreach(var truncatedTextForModerator in textsToModerate)
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

        public static Tuple<KeyPhraseBatchResult, EntitiesBatchResult> GetKeyPhrasesAndEntities(List<KeyValuePair<string, string>> keyPhrasesSamples)
        {
            var creds = new ApiKeyServiceClientCredentials();

            // Build client API call
            ITextAnalyticsClient client = new TextAnalyticsClient(creds)
            {
                Endpoint = Config.COGNITIVE_SERVICES_REGION_TEXT_ANALYTICS
            };

            // Getting key-phrases
            var lengthofText = keyPhrasesSamples.Select((v, i) => v.Value.ToString().Length).Sum();
            Console.WriteLine(string.Format("\tDocs: {0}", keyPhrasesSamples.Count));
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

        public static async Task<Tuple<string, OCRObjectResult>> OCRResult(MemoryStream memoryStream, string apiVersion)
        {
            // Two Computer Vision API versions for OCR. v1.0 and v2.0 (in preview)

            // Computer Vision URL
            var urlString = "https://" + Config.COGNITIVE_SERVICES_REGION + ".api.cognitive.microsoft.com/vision/" + apiVersion + "/ocr?language=en&detectOrientation=true";
            // return variables
            var responseContent = string.Empty;
            OCRObjectResult ocrObject = null;

            HttpContent content = new StreamContent(memoryStream);
            content.Headers.ContentType = new MediaTypeWithQualityHeaderValue("application/octet-stream");

            using (var httpClient = new HttpClient())
            {
                // Setup HttpClient
                httpClient.BaseAddress = new Uri(urlString);

                // Request headers
                httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", Config.COGNITIVE_SERVICES_KEY);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

                // Make request
                var response = await httpClient.PostAsync(urlString, content);

                //read response and write to view
                if (response.IsSuccessStatusCode)
                {
                    responseContent = await response.Content.ReadAsStringAsync();
                }
                else
                {
                    throw new Exception(response.StatusCode + " : " + response.ReasonPhrase);
                }

                ocrObject = JsonConvert.DeserializeObject<OCRObjectResult>(responseContent);
            }

            return new Tuple<string, OCRObjectResult>(responseContent, ocrObject);
        }

        public static async Task<Tuple<string, OCRObjectResult>> OCRResult(string uri, string apiVersion)
        {
            // Two Computer Vision API versions for OCR. v1.0 and v2.0 (in preview)
            // v2.0 is based on newer AI technology

            // Computer Vision URL
            var urlString = "https://" + Config.COGNITIVE_SERVICES_REGION + ".api.cognitive.microsoft.com/vision/" + apiVersion + "/ocr?language=en&detectOrientation=true";
            // return variables
            var responseContent = string.Empty;
            OCRObjectResult ocrObject = null;

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
                    //var response = await httpClient.PostAsync(urlString, content);

                    //read response and write to view
                    if (response.IsSuccessStatusCode)
                    {
                        responseContent = await response.Content.ReadAsStringAsync();
                    }
                    else
                    {
                        throw new Exception(response.StatusCode + " : " + response.ReasonPhrase);
                    }
                }

                ocrObject = JsonConvert.DeserializeObject<OCRObjectResult>(responseContent);
            }

            return new Tuple<string, OCRObjectResult>(responseContent, ocrObject);
        }

        public static async Task<SentimentV3Response> SentimentV3PreviewPredictAsync(List<TextAnalyticsInput> documents)
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

        public static MemoryStream ReduceImageQuality(MemoryStream imageStream)
        {
            MemoryStream reducedQualityImage = new MemoryStream();
            MemoryStream resizedImageStream = new MemoryStream();

            using (Bitmap bmp1 = (Bitmap)Image.FromStream(imageStream))
            {
                // resize image
                int width = Convert.ToInt32(bmp1.Width * 0.7);
                int height = Convert.ToInt32(bmp1.Height * 0.7);

                var resizedImage = bmp1.GetThumbnailImage(width, height, null, IntPtr.Zero);
                resizedImage.Save(resizedImageStream, ImageFormat.Png);
            }

            return resizedImageStream;
        }

        // Writes to Azure Table Storage
        // Note: Azure Table Storage will not write records that are longer than 64Kb
        public static void WriteToBlobStorageTable(WindowsAzureTable.CloudTable cloudTable, string category, string documentName, string ocrResult, 
            string keyPhraseResult, string distinctKeyPhraseString,
            string entities, string distinctEntitiesString,
            int pages, string uri, string documentType, long documentSizeInBytes,
            PIIResult piiResult, List<BingEntityData> bingEntityDataResult,
            SentimentV3Response sentimentV3Prediction)
        {
            ocrResult = ocrResult.Trim();
            keyPhraseResult = keyPhraseResult.Trim();

            var entityTaxonomies = string.Join(" ;;;; ", bingEntityDataResult.Select(a => a.Taxony).ToArray());

            var size = ocrResult.Length * sizeof(char);
            var keyPhraseResultSize = keyPhraseResult.Length * sizeof(char);
            var distinctKeyPhraseResultSize = distinctKeyPhraseString.Length * sizeof(char);
            var entitiesSize = entities.Length * sizeof(char);
            var entityTaxonomiesSize = entityTaxonomies.Length * sizeof(char);

            // Only for Table Storage API (CosmosDB can handle large values)
            if (size > 63999)
            {
                var lengthToTake = Convert.ToInt32(Math.Round((double) (32000 * 1.0 / size) * ocrResult.Length, 0));
                ocrResult = ocrResult.Substring(0, Math.Min(ocrResult.Length, lengthToTake));
            }

            if (keyPhraseResultSize > 31999)
            {
                var lengthToTake = Convert.ToInt32(Math.Round((double)(32000 * 1.0 / size) * keyPhraseResult.Length, 0));
                keyPhraseResult = keyPhraseResult.Substring(0, Math.Min(keyPhraseResult.Length, lengthToTake));
            }

            if (distinctKeyPhraseResultSize > 31999)
            {
                var lengthToTake = Convert.ToInt32(Math.Round((double)(32000 * 1.0 / size) * distinctKeyPhraseString.Length, 0));
                distinctKeyPhraseString = keyPhraseResult.Substring(0, Math.Min(distinctKeyPhraseString.Length, lengthToTake));
            }

            if (entitiesSize > 31999)
            {
                var lengthToTake = Convert.ToInt32(Math.Round((double)(32000 * 1.0 / size) * entities.Length, 0));
                entities = keyPhraseResult.Substring(0, Math.Min(entities.Length, lengthToTake));
            }

            if (entityTaxonomiesSize > 31999)
            {
                var lengthToTake = Convert.ToInt32(Math.Round((double)(32000 * 1.0 / size) * entityTaxonomies.Length, 0));
                entityTaxonomies = keyPhraseResult.Substring(0, Math.Min(entityTaxonomies.Length, lengthToTake));
            }

            // Create a new customer entity.
            var document = new DocumentEntity(category, documentName);
            document.OcrResult = ocrResult.Trim();
            document.TextAnalyticsKeyPhraseResult = keyPhraseResult;
            document.TextAnalyticsDistinctKeyPhraseResult = distinctKeyPhraseString;
            document.TextAnalyticsEntitiesResult = entities;
            document.TextAnalyticsDistinctEntititesResult = new string(distinctEntitiesString.Take(31999).ToArray());
            document.TextAnalyticsEntitiesTaxonomiesResult = entityTaxonomies;
            document.Uri = string.Empty;
            document.TextSize = size;
            document.Pages = pages;
            document.Uri = uri;
            document.DocumentType = documentType;
            document.DocumentSizeInBytes = documentSizeInBytes;
            document.PIIEmailsCount = piiResult.Emails.Count;
            document.PIIAddressesCount = piiResult.Addresses.Count;
            document.PIIPhoneNumbersCount = piiResult.PhoneNumbers.Count;
            document.PIISSNSCount = piiResult.SSNs.Count;
            document.SentimentAnalysis =
                "Positive: " + sentimentV3Prediction.Documents[0].DocumentScores.Positive +
                ", Neutral: " + sentimentV3Prediction.Documents[0].DocumentScores.Neutral +
                ", Negative: " + sentimentV3Prediction.Documents[0].DocumentScores.Negative;

            // Create the TableOperation object that inserts the customer entity.
            var insertOperation = WindowsAzureTable.TableOperation.InsertOrReplace(document);

            // Execute the insert operation.
            cloudTable.Execute(insertOperation);
        }

        public static void WriteToCosmosDbStorageTable(CosmosDbTable.CloudTable cloudTable, string category, string documentName, 
            string ocrResult, string keyPhraseResult, string distinctKeyPhraseString, int pages, string uri, string documentType, long documentSizeInBytes,
            PIIResult piiResult)
        {
            ocrResult = ocrResult.Trim();
            var size = ocrResult.Length * sizeof(char);

            // Create a new customer entity.
            var document = new CosmosDbDocumentEntity(category, documentName);
            document.TextOcrResult = ocrResult.Trim();
            document.KeyPhraseResult = keyPhraseResult;
            document.DistinctKeyPhraseResult = distinctKeyPhraseString;
            document.Uri = string.Empty;
            document.TextSize = size;
            document.Pages = pages;
            document.Uri = uri;
            document.DocumentType = documentType;
            document.DocumentSizeInBytes = documentSizeInBytes;

            // Create the TableOperation object that inserts the customer entity.
            var insertOperation = CosmosDbTable.TableOperation.InsertOrReplace(document);

            // Execute the insert operation.
            cloudTable.Execute(insertOperation);
        }

        public static void WriteToCosmosDbStorageSQLApi(DocumentClient documentDbClient, string category, string documentName,
            string textOcrResult, 
            string keyPhraseResult, string distinctKeyPhraseString,
            string entitiesResult, string distinctEntitiesResult,
            int pages, string uri, string documentType, long documentSizeInBytes, 
            Dictionary<string, List<string>>  topThreeClassificationNamesDictionary, 
            Dictionary<string, List<double>> topThreeClassificationProbabilitiesDictionary,
            PIIResult piiResult, List<BingEntityData> bingEntityDataResult,
            SentimentV3Response sentimentV3Prediction)
        {

            var docID = category + documentName;
            Console.WriteLine("\tProcessing DocumentDB: " + docID);
            var ocrPagesList = Util.GenerateDocumentPagesList("png", pages, category, documentName);
            var financialTablePageList = Util.DocumentFinancialTablePagesList(ocrPagesList, category);
            var topThreeClassificationNames = new List<string>();
            var topThreeClassificationProbabilities = new List<double>();

            dynamic documentToProcess = new
            {
                id = docID,
                PartitionKey = category,
                RowKey = documentName,
                DocumentType = documentType,
                DocumentSizeInBytes = documentSizeInBytes,
                TextAnalyticsDistinctKeyPhraseResult = distinctKeyPhraseString,
                TextAnalyticsKeyPhraseResult = keyPhraseResult,
                TextAnalyticsEntitiesResult = entitiesResult,
                TextAnalyticsDistinctEntitiesResult = distinctEntitiesResult,
                TextAnalyticsEntities = entitiesResult.Split(new string[] { " ;;;; " }, StringSplitOptions.None).ToList(),
                TextAnalyticsEntitiesTaxonomies = bingEntityDataResult.Select(a => a.Taxony).ToList(),
                Pages = pages,
                TextSize = textOcrResult.Length,
                TextOcrResult = textOcrResult,
                OcrPagesList = ocrPagesList,
                SentimentAnalysis = sentimentV3Prediction,
                JsonPagesList = Util.GenerateDocumentPagesList("json", pages, category, documentName),
                //DocumentClassificationNames = topThreeClassificationNamesDictionary.TryGetValue(documentName, out topThreeClassificationNames) ? topThreeClassificationNames : null,
                //DocumentClassificationProbabilities = topThreeClassificationProbabilitiesDictionary.TryGetValue(documentName, out topThreeClassificationProbabilities) ? topThreeClassificationProbabilities : null,
                //DocumentFinancialTablePages = financialTablePageList.Select(a => a.Item1).ToList(),
                //DocumentFinancialTablePagePredictions = financialTablePageList.Select(a => a.Item2).ToList(),
                PIIEmails = piiResult.Emails,
                PIIEmailsCount = piiResult.Emails.Count,
                PIIAddresses = piiResult.Addresses,
                PIIAddressesCount = piiResult.Addresses.Count,
                PIIPhoneNumbers = piiResult.PhoneNumbers,
                PIIPhoneNumbersCount = piiResult.PhoneNumbers.Count,
                PIISSNs = piiResult.SSNs,
                PIISSNSCount = piiResult.SSNs.Count,
                BingEntitityDataFull = bingEntityDataResult
                //HasFinancialTables = (financialTablePageList.Count > 0) ? true : false
            };

            // Write JSON to Blob Storage
            var jsonString = JsonConvert.SerializeObject(documentToProcess);
            var fullEnrichedDocumentPath = category.ToLower() + @"\" + documentName.ToLower() + @"\fullEnrichedDocument.json";
            var cloudStorageBloblClient = Util.BlobStorageAccount.CreateCloudBlobClient();
            var enrichmentContainer = cloudStorageBloblClient.GetContainerReference(Config.STORAGE_TABLE_AND_CONTAINER_NAMES.ToLower());
            var enrichedDocumentLocation = enrichmentContainer.GetBlockBlobReference(fullEnrichedDocumentPath);

            byte[] byteArray = Encoding.UTF8.GetBytes(jsonString);
            using (MemoryStream ms = new MemoryStream(byteArray))
            {
                enrichedDocumentLocation.UploadFromStream(ms);
            }

            CreateNewDoc(documentDbClient, Config.COSMOSDB_DOCUMENTS_SELFLINK, documentToProcess);
        }

        public static void WriteCsvFile(Dictionary<string, Tuple<string, string, string>> trainingFiles)
        {
            var path = Config.LOCAL_LOCATION_FILES_SOURCE_DOCUMENTS + "trainingFile.csv";

            using (var w = new StreamWriter(path))
            {
                foreach (var key in trainingFiles.Keys)
                {
                    var category = key;
                    var name = trainingFiles[key].Item1;
                    var ocrResult = trainingFiles[key].Item2;
                    var line = string.Format("{0}||||{1}||||{2}", category, name, ocrResult);
                    w.WriteLine(line);
                    w.Flush();
                }
            }
        }

        public static List<string> GenerateDocumentPagesList(string extension, int numberOfPages, string partition, string rowKey)
        {
            var pageSequence = new List<string>(numberOfPages);

            for (int i = 0; i != numberOfPages; i++)
            {
                pageSequence.Add(Config.STORAGE_ENRICHED_LOCATION + partition.ToLower() + @"/" + rowKey.ToLower() + @"/" + rowKey.ToLower() + (i + 1) + "." + extension);
            }

            return pageSequence;
        }

        public static List<Tuple<string, Prediction>> DocumentFinancialTablePagesList(List<string> documentPages, string partitionKey)
        {
            var documentFinancialTablePages = new List<Tuple<string, Prediction>>();
            for (int i = 0; i != documentPages.Count; i++)
            {
                var lastDocChars = documentPages[i].Substring(documentPages[i].Length - 8);

                if //(lastDocChars == "doc5.png" || 
                    (partitionKey != "10_0_Financials")
                {
                    break;
                }

                var url = documentPages[i] + Config.STORAGE_ACCOUNT_TEMP_SAS_KEY;
                var financialTableIdentificationPredictions = Util.CustomVisionObjectResult(url).Result;

                var financialTableHigherConfidencePredictions = financialTableIdentificationPredictions.Item2.predictions.Where(a => a.probability > 0.70).OrderByDescending(b => b.probability).ToList();

                if (financialTableHigherConfidencePredictions.Count > 0)
                {
                    var highestPredictedTableOnPage = financialTableHigherConfidencePredictions[0];
                    var tuplePagePrediction = new Tuple<string, Prediction>(documentPages[i], highestPredictedTableOnPage);
                    documentFinancialTablePages.Add(tuplePagePrediction);
                }
            }

            return documentFinancialTablePages;
        }

        public static async Task<Tuple<string, CustomVisionObjectResult>> CustomVisionObjectResult(string uri)
        {
            // Two Computer Vision API versions for OCR. v1.0 and v2.0 (in preview)
            // v2.0 is based on newer AI technology

            // Computer Vision URL
            var urlString = "https://southcentralus.api.cognitive.microsoft.com/customvision/v2.0/Prediction/460af9b0-5e66-4e70-a84a-d402e413fe2d/url?iterationId=12848efa-e746-445d-8e45-8a5a58c33ba7";
            // return variables
            var responseContent = string.Empty;
            CustomVisionObjectResult customVisionObjectResult = null;

            using (var httpClient = new HttpClient())
            {
                // Setup HttpClient
                httpClient.BaseAddress = new Uri(urlString);

                // Request headers
                // TODO: Change later, not used for now
                httpClient.DefaultRequestHeaders.Add("Prediction-Key", string.Empty);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Request body
                var uriBody = "{\"url\":\"" + uri + "\"}";

                using (var content = new StringContent(uriBody))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                    // Make request with resiliency policy (can retry http requests)
                    var resilienyStrategy = CognitiveServicesRetryPolicy.DefineAndRetrieveResiliencyStrategy();
                    var response = await resilienyStrategy.ExecuteAsync(() => httpClient.PostAsync(urlString, content));
                    //var response = await httpClient.PostAsync(urlString, content);

                    //read response and write to view
                    if (response.IsSuccessStatusCode)
                    {
                        responseContent = await response.Content.ReadAsStringAsync();
                    }
                    else
                    {
                        throw new Exception(response.StatusCode + " : " + response.ReasonPhrase);
                    }
                }

                customVisionObjectResult = JsonConvert.DeserializeObject<CustomVisionObjectResult>(responseContent);
            }

            return new Tuple<string, CustomVisionObjectResult>(responseContent, customVisionObjectResult);
        }

        private static async Task CreateNewDoc(DocumentClient client, string documentsFeed, object document)
        {
            Util.CreateDocument(client, documentsFeed, document).Wait();
        }

        public static async Task CreateDocument(DocumentClient client, string documentsFeed, object document)
        {
            ResourceResponse<Document> response = await client.UpsertDocumentAsync(documentsFeed, document);
            var createdDocument = response.Resource;
        }

        public static Image ResizeImageForCognitiveOCR(Image image)//, int width, int height)
        {
            // If image is less than 3200x3200 return it
            if (image.Height <= 4200 && image.Width <= 4200)
            {
                return image;
            }

            // Rescale the image for max 3150 x 3150 (below 3200 x 3200)
            var originalWidth = image.Size.Width;
            var originalHeight = image.Size.Height;
            var width = 4000;
            var height = 4000;

            // Figure out the ratio
            double ratioX = (double) width / (double)originalWidth;
            double ratioY = (double) height / (double)originalHeight;
            // use whichever multiplier is smaller
            double ratio = ratioX < ratioY ? ratioX : ratioY;
            // now we can get the new height and width
            int newHeight = Convert.ToInt32(originalHeight * ratio);
            int newWidth = Convert.ToInt32(originalWidth * ratio);

            var destRect = new Rectangle(0, 0, newWidth, newHeight);
            var destImage = new Bitmap(newWidth, newHeight);

            // Now calculate the X,Y position of the upper-left corner 
            // (one of these will always be zero)
            int posX = Convert.ToInt32((newWidth - (originalWidth * ratio)) / 2);
            int posY = Convert.ToInt32((newHeight - (originalHeight * ratio)) / 2);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                //graphics.CompositingQuality = CompositingQuality.HighQuality;
                //graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                //graphics.SmoothingMode = SmoothingMode.HighQuality;
                //graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.ClearBrushRemapTable();
                    wrapMode.ClearGamma();
                    wrapMode.ClearColorMatrix();

                    wrapMode.SetWrapMode(WrapMode.Tile);
                    graphics.Clear(Color.White);
                    //graphics.DrawRectangle(new Pen(new SolidBrush(Color.White)), 0, 0, newWidth, newHeight);
                    graphics.DrawImage(image, destRect, posX, posY, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            // Ensure image doesn't have a transparency layer
            Bitmap temp = new Bitmap(destImage.Width, destImage.Height, PixelFormat.Format24bppRgb);
            Graphics g = Graphics.FromImage(temp);
            g.Clear(Color.White);
            g.DrawImage(destImage, Point.Empty);
            return temp;
            // return destImage;
        }
    }
}