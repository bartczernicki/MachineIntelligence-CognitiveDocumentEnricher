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

        public static byte[] GetImageAsByteArray(string imageFilePath)
        {
            // Open a read-only file stream for the specified file.
            using (FileStream fileStream =
                new FileStream(imageFilePath, FileMode.Open, FileAccess.Read))
            {
                // Read the file's contents into a byte array.
                BinaryReader binaryReader = new BinaryReader(fileStream);
                return binaryReader.ReadBytes((int)fileStream.Length);
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
            PIIResult piiResultV2, List<CognitiveServiceClasses.PII.Entity> piiResultV3,
            List<BingEntityData> bingEntityDataResult,
            SentimentV3Response sentimentV3Prediction,
            CognitiveServicesApiCalls cognitiveServicesApiCalls)
        {
            ocrResult = ocrResult.Trim();
            keyPhraseResult = keyPhraseResult.Trim();

            var entityTaxonomies = 
                (bingEntityDataResult is null) ? string.Empty :
                string.Join(" ;;;; ", bingEntityDataResult.Select(a => a.Taxony).ToArray());

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
            document.CognitiveServicesApiCallsApiCallCount = cognitiveServicesApiCalls.ApiCallCount;
            document.CognitiveServicesApiCallsApiCallV2Count = cognitiveServicesApiCalls.ApiCallV2Count;
            document.CognitiveServicesApiCallsApiCallV3Count = cognitiveServicesApiCalls.ApiCallV3Count;
            document.CognitiveServicesApiCallsTotalCount = cognitiveServicesApiCalls.TotalCount;
            document.OcrResult = ocrResult.Trim();
            document.TextAnalyticsKeyPhraseResult = keyPhraseResult;
            document.TextAnalyticsDistinctKeyPhraseResult = distinctKeyPhraseString;
            document.TextAnalyticsEntitiesResult = entities;
            document.TextAnalyticsDistinctEntititesResult = new string(distinctEntitiesString.Take(31999).ToArray());
            document.TextAnalyticsEntitiesTaxonomiesResult = entityTaxonomies;
            document.TextSize = size;
            document.Pages = pages;
            document.Uri = uri;
            document.DocumentType = documentType;
            document.DocumentSizeInBytes = documentSizeInBytes;

            if (piiResultV2.Addresses != null)
            {
                document.PIIEmailsCount = piiResultV2.Emails.Count;
                document.PIIAddressesCount = piiResultV2.Addresses.Count;
                document.PIIPhoneNumbersCount = piiResultV2.PhoneNumbers.Count;
                document.PIISSNSCount = piiResultV2.SSNs.Count;
            }

            if (sentimentV3Prediction.Documents != null)
            {
                document.SentimentAnalysis =
                    "Positive: " + sentimentV3Prediction.Documents[0].DocumentScores.Positive +
                    ", Neutral: " + sentimentV3Prediction.Documents[0].DocumentScores.Neutral +
                    ", Negative: " + sentimentV3Prediction.Documents[0].DocumentScores.Negative;
            }

            // Create the TableOperation object that inserts the customer entity.
            var insertOperation = WindowsAzureTable.TableOperation.InsertOrReplace(document);

            // Execute the insert operation.
            cloudTable.Execute(insertOperation);
        }


        public static void WriteToCosmosDbStorageSQLApi(DocumentClient documentDbClient, string category, string documentName,
            string textOcrResult,
            List<string> keyPhrasesV2, List<string> keyPhrasesV3,
            List<string> entitiesV2, List<CognitiveServiceClasses.Entities.Entity> entitiesV3,
            int pages, string uri, string documentType, long documentSizeInBytes,
            PIIResult piiResultV2, List<CognitiveServiceClasses.PII.Entity> piiResultV3,
            List<BingEntityData> bingEntityDataResult,
            SentimentV3Response sentimentV3Prediction,
            CognitiveServicesApiCalls cognitiveServicesApiCalls)
        {
            var documentToProcess = Util.GetDocumentObject(category, documentName, textOcrResult,
                keyPhrasesV2, keyPhrasesV3,
                entitiesV2, entitiesV3,
                pages, uri, documentType,
                documentSizeInBytes, piiResultV2, bingEntityDataResult, sentimentV3Prediction,
                cognitiveServicesApiCalls);

            var jsonString = JsonConvert.SerializeObject(documentToProcess);
            var fullEnrichedDocumentPath = category.ToLower() + @"\" + documentName.ToLower() + @"\fullEnrichedDocument.json";

            // Write JSON to Blob Storage
            if (Config.USE_AZURE_BLOB_STORAGE)
            {
                var cloudStorageBloblClient = Util.BlobStorageAccount.CreateCloudBlobClient();
                var enrichmentContainer = cloudStorageBloblClient.GetContainerReference(Config.STORAGE_TABLE_AND_CONTAINER_NAMES.ToLower());
                var enrichedDocumentLocation = enrichmentContainer.GetBlockBlobReference(fullEnrichedDocumentPath);

                byte[] byteArray = Encoding.UTF8.GetBytes(jsonString);
                using (MemoryStream ms = new MemoryStream(byteArray))
                {
                    enrichedDocumentLocation.UploadFromStream(ms);
                }
            }

            CreateNewDoc(documentDbClient, Config.COSMOSDB_DOCUMENTS_SELFLINK, documentToProcess);
        }


        public static void WriteToLocalStorage(string category, string documentName, string textOcrResult, 
            List<string> keyPhrasesV2, List<string> keyPhrasesV3,
            List<string> entitiesV2, List<CognitiveServiceClasses.Entities.Entity> entitiesV3,
            int pages, string uri, string documentType, long documentSizeInBytes,
            PIIResult piiResultV2, List<CognitiveServiceClasses.PII.Entity> piiResultV3,
            List<BingEntityData> bingEntityDataResult,
            SentimentV3Response sentimentV3Prediction,
            CognitiveServicesApiCalls cognitiveServicesApiCalls)
        {

            var documentToProcess = Util.GetDocumentObject(category, documentName, textOcrResult,
                keyPhrasesV2, keyPhrasesV3,
                entitiesV2, entitiesV3,
                pages, uri, documentType,
                documentSizeInBytes, piiResultV2, bingEntityDataResult, sentimentV3Prediction,
                cognitiveServicesApiCalls);

            var jsonString = JsonConvert.SerializeObject(documentToProcess);
            var fullEnrichedDocumentPath = category.ToLower() + @"\" + documentName.ToLower() + @"\fullEnrichedDocument.json";

            // Write JSON to Local Disk
            System.IO.File.WriteAllText(Config.LOCAL_LOCATION_FILES_PROCESSED_OUTPUTS + @"\" + fullEnrichedDocumentPath, jsonString);
        }


        public static dynamic GetDocumentObject(string category, string documentName, string textOcrResult, 
            List<string> keyPhrasesV2, List<string> keyPhrasesV3,
            List<string> entitiesV2, List<CognitiveServiceClasses.Entities.Entity> entitiesV3,
            int pages, string uri, string documentType, long documentSizeInBytes,
            PIIResult piiResult, List<BingEntityData> bingEntityDataResult,
            SentimentV3Response sentimentV3Prediction,
            CognitiveServicesApiCalls cognitiveServicesApiCalls)
        {
            var docID = category + documentName;

            List<string> azureBlobOcrPagesList = Config.USE_AZURE_BLOB_STORAGE ?
            Util.GenerateDocumentPagesList("png", pages, category, documentName) : new List<string>();

            dynamic documentToProcess = new
            {
                id = docID,
                PartitionKey = category,
                RowKey = documentName,
                DocumentType = documentType,
                DocumentSizeInBytes = documentSizeInBytes,
                Pages = pages,
                TextSize = textOcrResult.Length,
                CognitiveServicesApiCallsApiCallCount = cognitiveServicesApiCalls.ApiCallCount,
                CognitiveServicesApiCallsApiCallV2Count = cognitiveServicesApiCalls.ApiCallV2Count,
                CognitiveServicesApiCallsApiCallV3Count = cognitiveServicesApiCalls.ApiCallV3Count,
                CognitiveServicesApiCallsTotalCount = cognitiveServicesApiCalls.TotalCount,
                TextAnalyticsV2KeyPhrasesCount = keyPhrasesV2.Count(),
                TextAnalyticsV2KeyPhrases = keyPhrasesV2,
                TextAnalyticsV2KeyPhrasesDistinct = keyPhrasesV2.Distinct().ToList(),
                TextAnalyticsV2EntitiesCount = entitiesV2.Count(),
                TextAnalyticsV2Entities = entitiesV2,
                TextAnalyticsV2EntitiesDistinct = entitiesV2.Distinct().ToList(),
                TextAnalyticsV2EntitiesBingTaxonomies = bingEntityDataResult.Select(a => a.Taxony).ToList(),
                TextAnalyticsV3EntitiesCount = entitiesV3.Count(),
                TextAnalyticsV3Entities = entitiesV3,
                TextAnalyticsV3KeyPhrasesCount = keyPhrasesV3.Count(),
                TextAnalyticsV3KeyPhrases = keyPhrasesV3,
                TextAnalyticsV3KeyPhrasesDistinct = keyPhrasesV3.Distinct().ToList(),
                TextAnalyticsV3SentimentAnalysis = sentimentV3Prediction,
                TextAnalyticsV3SentimentAnalysisPositive = sentimentV3Prediction,
                TextOcrResult = textOcrResult,
                AzureBlobJsonPagesList = Config.USE_AZURE_BLOB_STORAGE ?
                    Util.GenerateDocumentPagesList("json", pages, category, documentName) : new List<string>(),
                AzureBlobOcrPagesList = azureBlobOcrPagesList,
                PIIResult = piiResult,
                BingEntitityDataFull = bingEntityDataResult
            };

            return documentToProcess;
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
            await Util.CreateDocument(client, documentsFeed, document);
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