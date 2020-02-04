using iTextSharp.text.pdf;
using Microsoft.Azure.Documents.Client;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace CognitiveDocumentEnricher
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Checking Configuration Values...");
            Console.ResetColor();

            Console.WriteLine("--------------------------------");

            if (!Config.USE_COGNITIVE_SERVICES_V2 && !Config.USE_COGNITIVE_SERVICES_V3)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("You must have either Cognitive Services V2 or V3 enabled in the Config file.");
                Console.ReadLine();
                Environment.Exit(0);
            }

            Console.WriteLine("Use Cognitive Services Bing Entity Search: " + Config.USE_COGNITIVE_SERVICES_BING_ENTITY_SEARCH);
            Console.WriteLine("Use Azure Blob Storage: " + Config.USE_AZURE_BLOB_STORAGE);
            Console.WriteLine("Use Azure Table Storage: " + Config.USE_AZURE_TABLE_STORAGE);
            Console.WriteLine("Use CosmosDB Storage: " + Config.USE_COSMOSDB_STORAGE);
            Console.WriteLine("--------------------------------");
            Console.WriteLine(string.Empty);

            Dictionary<string, Exception> errors = new Dictionary<string, Exception>();
            Dictionary<string, int> longDocuments = new Dictionary<string, int>();
            Dictionary<string, Tuple<string, string, string>> processedTrainingFiles = new Dictionary<string, Tuple<string, string, string>>(1700);

            var scoringTableEntities = new List<Microsoft.Azure.CosmosDB.Table.DynamicTableEntity>();
            var topThreeClassificationNamesDictionary = new Dictionary<string, List<string>>();
            var topThreeClassificationProbabilitiesDictionary = new Dictionary<string, List<double>>();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Extracting content from documents...");
            Console.ResetColor();

            // List of types of extensions
            var fileTypes = new List<Tuple<string, string>>();
            var currentFilesDirectory = string.Empty;
            var filePath = string.Empty;
            
            currentFilesDirectory = Config.LOCAL_LOCATION_FILES_SOURCE_DOCUMENTS;

            var docExt = new List<string> { ".DOC", ".DOCX", ".DOTX", ".DOT" };
            var files = Util.DirectoryTraverseForFiles(currentFilesDirectory).ToList();

            try
            {
                Console.WriteLine("--------------------------------");
                Console.WriteLine("Processing Files...");

                // 2) Process Files
                for (int fileNum = 0; fileNum != files.Count; fileNum++)
                {
                    // Cognitive Services API Calls
                    var cognitiveServicesApiCalls = new CognitiveServicesApiCalls();

                    // Retrieve the file path
                    filePath = files[fileNum];

                    // Retrieve the directory, file name & extension
                    var categoryAndFileNames = filePath.Replace(currentFilesDirectory, string.Empty)
                        .Split(new string[] { "\\" }, StringSplitOptions.None);
                    var originalDocumentExtension = Path.GetExtension(filePath).ToUpper();

                    // Clean up file names
                    var category = categoryAndFileNames[0];
                    var cleanCategory = category.Replace(" ", "_").Replace(".", "_");
                    var fileName = Path.GetFileName(filePath);
                    var cleanFileName = fileName.Replace(" ", "_").Replace(".", "_");
                    var fileTotalOcr = string.Empty;
                    var keyPhraseString = string.Empty;
                    var entitiesString = string.Empty;
                    var pages = 0;
                    var uri = string.Empty;
                    var documentType = "Unknown"; //type of document (i.e. PDF, Word, Excel etc.)
                    var documentSizeInBytes = 0L;

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Processing file {0} : ID={1}  [{2} of {3}]", fileName, cleanFileName,
                        files.IndexOf(filePath) + 1,
                        files.Count);
                    Console.ResetColor();

                    // Group file types based on the required processing
                    fileTypes.Add(new Tuple<string, string>(fileName, originalDocumentExtension));
                    var wordDocuments = new List<string> { ".DOC", ".DOCX", ".DOTX", ".DOT" };
                    var excelDocuments = new List<string> { ".XLS", ".XLSX", ".XLT", ".XLTX" };
                    var htmlDocuments = new List<string> { ".HTM", ".HTML", ".SHTML" };

                    // Hold the values for the image pages
                    List<MemoryStream> imageStreams = new List<MemoryStream>();
                    // Hold the values for the OCR from pages
                    List<string> imagePagesOcr = new List<string>();

                    // Process/read document
                    MemoryStream documentStream = new MemoryStream();
                    using (var file = File.OpenRead(filePath))
                    {
                        documentSizeInBytes = new System.IO.FileInfo(filePath).Length;
                        var pdfDocumentPartFileNames = new List<string>();

                        // PDF Files
                        if (originalDocumentExtension == ".PDF")
                        {
                            documentType = "PDF";
                            // Setup PDFReader for unethical reading
                            PdfReader.unethicalreading = true;

                            // Setup PDF part cache location
                            var directoryCategoryPdfCache = Config.LOCAL_LOCATION_FILES_PROCESSED_OUTPUTS + @"\PDFCache\" + cleanCategory + @"\";
                            var directoryFilePdfCache = directoryCategoryPdfCache + cleanFileName;
                            System.IO.Directory.CreateDirectory(directoryCategoryPdfCache.ToLower());
                            System.IO.Directory.CreateDirectory(directoryFilePdfCache.ToLower());

                            Console.WriteLine("\tCracking Documents into Pages...");
                            using (PdfReader pdfReader = new PdfReader(filePath))
                            {
                                for (int pagenumber = 1; pagenumber <= pdfReader.NumberOfPages; pagenumber++)
                                {
                                    iTextSharp.text.Document iTextDocument = new iTextSharp.text.Document();

                                    var fullFilePdfPartName = directoryFilePdfCache + @"\" + cleanFileName + (pagenumber) + ".pdf";
                                    pdfDocumentPartFileNames.Add(fullFilePdfPartName);

                                    PdfCopy copy = new PdfCopy(iTextDocument, new FileStream(fullFilePdfPartName, FileMode.Create));

                                    iTextDocument.Open();
                                    copy.AddPage(copy.GetImportedPage(pdfReader, pagenumber));
                                    iTextDocument.Close();
                                }
                            }

                            Aspose.Pdf.Document document = new Aspose.Pdf.Document(file);
                            document.Save(documentStream);
                            pages = document.Pages.Count;
                            Console.WriteLine(string.Format("\tPages: {0}", pages));

                            //imageStreams.Add(document.ConvertPageToPNGMemoryStream(document.Pages[1]));
                        }
                        // Excel Files
                        else if (excelDocuments.Contains(originalDocumentExtension))
                        {
                            documentType = "Excel";
                            Aspose.Cells.Workbook workBook = new Aspose.Cells.Workbook(file);
                            pages = workBook.Worksheets.Count;
                            workBook.Save(documentStream, Aspose.Cells.SaveFormat.Pdf);
                        }
                        // HTML & Word Documents
                        else if (htmlDocuments.Contains(originalDocumentExtension)
                            || wordDocuments.Contains(originalDocumentExtension))
                        {
                            documentType = "Html";
                            Aspose.Words.Document document = new Aspose.Words.Document(file);
                            pages = document.PageCount;
                            document.Save(documentStream, Aspose.Words.SaveFormat.Pdf);
                        }

                        // Convert any documents into images
                        if (originalDocumentExtension == ".PDF")
                        {
                            foreach(var pdfDcoumentFileName in pdfDocumentPartFileNames)
                            {
                                var pdfDocument = new Aspose.Pdf.Document(pdfDcoumentFileName);
                                var pdfDocumentPage = pdfDocument.Pages[1];

                                try
                                {
                                    imageStreams.Add(pdfDocument.ConvertPageToPNGMemoryStream(pdfDocumentPage));
                                }
                                catch(Exception e)
                                {
                                    Console.WriteLine("!!! ERROR !!!: Converting PDF To Image - " + pdfDcoumentFileName + " ||| " + e.ToString());
                                }
                            }
                        }
                        // Word Documents
                        else if (wordDocuments.Contains(originalDocumentExtension.ToUpper()))
                        {
                            documentType = "Word";
                            Microsoft.Office.Interop.Word.Application wordApp = new Microsoft.Office.Interop.Word.Application();
                            var wordDocument = new Microsoft.Office.Interop.Word.Document();
                            object missing = System.Type.Missing;
                            wordDocument = wordApp.Documents.Open(filePath);

                            foreach (Microsoft.Office.Interop.Word.Window window in wordDocument.Windows)
                            { 
                                foreach (Microsoft.Office.Interop.Word.Pane pane in window.Panes)
                                {
                                    // set the pages
                                    pages = pane.Pages.Count;

                                    for (var i = 1; i <= pane.Pages.Count; i++)
                                    {
                                        var bits = pane.Pages[i].EnhMetaFileBits;
                                        try
                                        {
                                            using (var ms = new MemoryStream((byte[])(bits)))
                                            {
                                                var imageStream = new MemoryStream();
                                                var image = System.Drawing.Image.FromStream(ms);
                                                image.Save(imageStream, ImageFormat.Png);
                                                //ms.Position = 0;
                                                
                                                imageStreams.Add(imageStream);
                                            }
                                        }
                                        catch (System.Exception ex)
                                        {
                                            var error = ex.Message;
                                            throw (ex);
                                        }
                                    }
                                }
                            }
                            wordDocument.Close(Type.Missing, Type.Missing, Type.Missing);
                            wordApp.Quit(Type.Missing, Type.Missing, Type.Missing);

                        }
                        else if (documentStream != null && imageStreams.Count == 0)
                        {
                            var pdfDocument = new Aspose.Pdf.Document(documentStream);

                            // Done here as Aspose workaround for free version
                            for (int pageNum = 0; pageNum != pdfDocument.Pages.Count; pageNum++)
                            {
                                if (pageNum == 4)
                                {
                                    // Track documents over 4 pages
                                    longDocuments.Add(fileName, pdfDocument.Pages.Count);
                                    break;
                                }

                                // PageCollection starts at 1, not 0 index
                                imageStreams.Add(pdfDocument.ConvertPageToPNGMemoryStream(pdfDocument.Pages[pageNum + 1]));
                            }
                        }
                    }  // EOF reading file

                    Console.WriteLine("\tConverting Pages to Images...");
                    for (int i = 0; i != imageStreams.Count; i++)
                    {
                        //Console.WriteLine(string.Format("\tProcessing Image {0} of {1}", (i + 1), imageStreams.Count));

                        var imageStream = imageStreams[i];

                        // Cognitive Services Requirement: Convert to MB, Computer Vision images max out at 4 MB
                        // Fine for individual page documents
                        var megaBytes = Util.ConvertBytesToMegabytes(imageStream.Length);

                        do
                        {
                            var reducedImage = Util.ReduceImageQuality(imageStream);
                            imageStream = reducedImage;
                        }
                        while(
                           Util.ConvertBytesToMegabytes(imageStream.Length) > 3.99
                        );


                        // Setup Image store location
                        var directoryCategory = Config.LOCAL_LOCATION_FILES_PROCESSED_OUTPUTS + @"\" + cleanCategory + @"\";
                        var directoryFile = directoryCategory + cleanFileName;
                        var fullFileImageName = directoryFile + @"\" + cleanFileName + (i + 1) + ".png";
                        System.IO.Directory.CreateDirectory(directoryCategory.ToLower());
                        System.IO.Directory.CreateDirectory(directoryFile.ToLower());

                        // Save the image to local image folder (cache)
                        using (Bitmap image = (Bitmap) System.Drawing.Image.FromStream(imageStream))
                        {
                            var resizedImage = Util.ResizeImageForCognitiveOCR(image);
                            resizedImage.Save(fullFileImageName, ImageFormat.Png);
                        }

                        var basePath = cleanCategory + @"\" + cleanFileName + @"\" + cleanFileName + (i + 1);

                        // Set up cloud path for JSON content
                        var cloudImagePath = (basePath + ".png").ToLower();
                        var cloudOcrPath = (basePath + ".json").ToLower();

                        // Use API that passes image binary directly
                        var ocrResult = CognitiveServices.VisionOCRResultBatchReadFromImageAsync(fullFileImageName, "v2.1").Result;
                        //var ocrResult = CognitiveServices.OCRResultBatchRead(imageUrl, "v2.1").Result;
                        cognitiveServicesApiCalls.ApiCallCount++;

                        if (Config.USE_AZURE_BLOB_STORAGE)
                        {
                            // Azure Storage Objects Init
                            var cloudStorageBloblClient = Util.BlobStorageAccount.CreateCloudBlobClient();
                            var enrichmentContainer = cloudStorageBloblClient.GetContainerReference(Config.STORAGE_TABLE_AND_CONTAINER_NAMES.ToLower());
                            uri = enrichmentContainer.StorageUri.PrimaryUri + @"/" + cleanCategory + @"/" + cleanFileName + @"/";
                            var trainingImage = enrichmentContainer.GetBlockBlobReference(cloudImagePath);
                            var trainingImageOcr = enrichmentContainer.GetBlockBlobReference(cloudOcrPath);

                            // Send PNG image to Blob Storage
                            using (var fs = new FileStream(fullFileImageName, FileMode.Open))
                            {
                                trainingImage.UploadFromStream(fs);
                            }

                            // Retrieve OCR keyPhraseResult and upload JSON to cloud
                            // Uses API based on cloud storage
                            var imageUrl = trainingImage.Uri.AbsoluteUri + Config.STORAGE_ACCOUNT_TEMP_SAS_KEY;

                            // Upload the JSON response to the blob containers
                            trainingImageOcr.UploadText(ocrResult.Item1);
                        }

                        // Write JSON to local disk
                        var jsonFileName = basePath + ".json";
                        System.IO.File.WriteAllText(Config.LOCAL_LOCATION_FILES_PROCESSED_OUTPUTS + @"\" + jsonFileName, ocrResult.Item1);


                        var ocrString = ocrResult.Item2.ToString();
                        imagePagesOcr.Add(ocrString);

                        // Console.WriteLine("Number of OCR Regions Found - " + ocrResult.Item2.regions.Count);
                    }

                    var ocrPhrases = new List<KeyValuePair<string, string>>();

                    foreach (var ocrItem in imagePagesOcr)
                    {
                        // remove the trial items from Aspose
                        var tempOcrItem = ocrItem.
                            Replace("Evaluation Only. Created with Aspose.PDF. Copyright 2002-2019 Aspose Pty Ltd.", string.Empty).
                            Replace("Evaluation Only. Created with Aspose.PDF. Copyright 2002-2018 Aspose Pty Ltd.", string.Empty).
                            Replace("Evaluation Only. Created with Aspose.PDF", string.Empty).
                            Replace("Evaluation Only. Created with Aspose.Words", string.Empty).
                            Replace("Evaluation Only. Created with Aspose.Cells", string.Empty).
                            Replace("Created with Aspose.Cells for .NET.Copyright 2003 - 2018", string.Empty).
                            Replace("Copyright 2002-2019 Aspose Pty Ltd.", string.Empty).
                            Replace("Copyright 2002-2018 Aspose Pty Ltd.", string.Empty).
                            Replace("Aspose Pty Ltd.", string.Empty).
                            Replace("Created with Aspose.", string.Empty).
                            Replace("Copyright 2002-2018.", string.Empty).
                            Replace("Copyright 2003-2018.", string.Empty).
                            Replace("Copyright 2002-2018", string.Empty).
                            Replace("Copyright 2003-2018", string.Empty).
                            Replace("spose Pty Ltd.", string.Empty).
                            Replace("Pty Ltd.", string.Empty).
                            Replace("Evaluation With A", string.Empty).
                            Replace("Evaluation With %002-2018 A", string.Empty).
                            Replace("Evaluation Only.", string.Empty).
                            Replace(".  Aspose Pty ", string.Empty).
                            Replace("Evaluatqoh With %002-2018 A", string.Empty).
                            Replace("Created with Aspose.Cells for .NET.Copyright 2003 - 2018 A", string.Empty).
                            Replace("Evaluation Only• created 18 Aspose Pty", string.Empty).
                            Replace("Evaluation Only. Created with Aspose.PDF", string.Empty).
                            Replace("with Aspose. PDF", string.Empty).
                            Replace("Aspose.PDF", string.Empty);
                        // add to main OCR file
                        fileTotalOcr += tempOcrItem + System.Environment.NewLine;

                        // Add to phrases to process (english)
                        if (tempOcrItem.Length > 5000)
                        {
                            tempOcrItem = tempOcrItem.Substring(0, 5000);
                        }

                        ocrPhrases.Add(new KeyValuePair<string, string>("en", tempOcrItem));
                    }

                    List<string> keyPhrasesV2 = new List<string>();
                    List<string> entitiesV2 = new List<string>();
                    var piiResultV2 = new PIIResult();
                    string distinctKeyPhraseString = string.Empty;
                    string distinctEntitiesString = string.Empty;

                    if (Config.USE_COGNITIVE_SERVICES_V2)
                    {
                        // Key Phrases - V2
                        Console.WriteLine("\tKey Phrases V2...");
                        var keyPhraseResult = CognitiveServices.TextAnalyticsKeyPhrasesAndEntities(ocrPhrases, ref cognitiveServicesApiCalls);
                        keyPhrasesV2 = keyPhraseResult.Item1.Documents.SelectMany(i => i.KeyPhrases).Where(a => Helpers.IsEntity(a)).ToList();
                        distinctKeyPhraseString = string.Join(" ;;;; ", keyPhrasesV2.Distinct().ToArray());
                        keyPhraseString = string.Join(" ;;;; ", keyPhrasesV2.ToArray());

                        // Entities - V2
                        Console.WriteLine("\tEntities V2...");
                        var entitiesRecords = keyPhraseResult.Item2.Documents.SelectMany(i => i.Entities).Where(a => Helpers.IsEntity(a.Name)).ToList();
                        entitiesV2 = entitiesRecords.Select(i => i.Name.Replace(System.Environment.NewLine, string.Empty).Trim()).ToList();
                        distinctEntitiesString = string.Join(" ;;;; ", entitiesV2.Distinct().ToArray());
                        entitiesString = string.Join(" ;;;; ", entitiesV2.ToArray());

                        // PII Result - V2
                        Console.WriteLine("\tPII Information V2...");
                        piiResultV2 = CognitiveServices.TextAnalyticsPIIResultV2(ocrPhrases, ref cognitiveServicesApiCalls);
                    }

                    List<string> keyPhrasesV3 = new List<string>();
                    List<CognitiveServiceClasses.Entities.Entity> entitiesV3 = new List<CognitiveServiceClasses.Entities.Entity>();
                    List<CognitiveServiceClasses.PII.Entity> piiResultV3 = new List<CognitiveServiceClasses.PII.Entity>();
                    SentimentV3Response sentimentV3Prediction = new SentimentV3Response();

                    if (Config.USE_COGNITIVE_SERVICES_V3)
                    {
                        // Key Phrases - V3
                        Console.WriteLine("\tKey Phrases V3...");
                        var textAnalyticsV3KeyPhrasesPrediction = CognitiveServices.TextAnalyticsKeyPhrasesV3PreviewAsync(ocrPhrases).Result;
                        keyPhrasesV3 = textAnalyticsV3KeyPhrasesPrediction.documents.SelectMany(a => a.keyPhrases).ToList();
                        cognitiveServicesApiCalls.ApiCallV3Count++;

                        // Entities - V3
                        Console.WriteLine("\tEntities V3...");
                        var textAnalyticsV3EntitiesPrediction = CognitiveServices.TextAnalyticsEntitiesV3PreviewAsync(ocrPhrases).Result;
                        entitiesV3 = textAnalyticsV3EntitiesPrediction.documents.SelectMany(a => a.entities).ToList();
                        cognitiveServicesApiCalls.ApiCallV3Count++;

                        // PIIs - V3
                        Console.WriteLine("\tPIIs V3...");
                        var textAnalyticsV3PIIPrediction = CognitiveServices.TextAnalyticsPIIV3PreviewAsync(ocrPhrases).Result;
                        piiResultV3 = textAnalyticsV3PIIPrediction.documents.SelectMany(a => a.entities).ToList();
                        cognitiveServicesApiCalls.ApiCallV3Count++;

                        // Sentiment Analysis - V3
                        Console.WriteLine("\tSentiment Analysis V3...");
                        var textAnalyticsInput = new TextAnalyticsInput()
                        {
                            Id = "1",
                            Text = fileTotalOcr.Length > 5100 ? fileTotalOcr.Substring(0, 5100) : fileTotalOcr
                        };
                        var textAnalyticsInputs = new List<TextAnalyticsInput> { textAnalyticsInput };
                        sentimentV3Prediction = CognitiveServices.TextAnalyticsSentimentAnalysisV3PreviewAsync(textAnalyticsInputs).Result;
                        cognitiveServicesApiCalls.ApiCallV3Count++;
                    }


                    List<BingEntityData> entityTaxonyResult = new List<BingEntityData>();

                    if (Config.USE_COGNITIVE_SERVICES_BING_ENTITY_SEARCH)
                    {
                        Console.WriteLine("\tRetrieving Bing Entitites...");
                        entityTaxonyResult = CognitiveServices.BingEntities(entitiesV2);
                        Console.WriteLine("\tFinished Retrieving Bing Entitites.");
                    }


                    // Add up OCR pages & Process Key Entities
                    processedTrainingFiles.Add(cleanFileName, new Tuple<string, string, string>(cleanCategory, fileTotalOcr, keyPhraseString));

                    // Azure Table Storage
                    // Note: most attributes are truncated due to limitations for Azure Table Storage
                    // For large documents use: CosmosDb or blob storage
                    if (Config.USE_AZURE_TABLE_STORAGE)
                    {
                        var tableStorageClient = Util.BlobStorageAccount.CreateCloudTableClient();
                        Microsoft.WindowsAzure.Storage.Table.CloudTable table = tableStorageClient.GetTableReference(Config.STORAGE_TABLE_AND_CONTAINER_NAMES);
                        table.CreateIfNotExists();

                        Util.WriteToBlobStorageTable(table, cleanCategory, cleanFileName, fileTotalOcr,
                            keyPhraseString, distinctKeyPhraseString,
                            entitiesString, distinctEntitiesString,
                            pages, uri, documentType, documentSizeInBytes,
                            piiResultV2, piiResultV3,
                            entityTaxonyResult, sentimentV3Prediction,
                            cognitiveServicesApiCalls);
                        Console.WriteLine("\tPersisted: Azure Table Storage");
                    }

                    if (Config.USE_COSMOSDB_STORAGE)
                    {
                        // Cosmos DB - Documents Client
                        DocumentClient documentClient = new DocumentClient(Config.COSMOSDB_DOCUMENTS_URI, Config.COSMOSDB_DOCUMENTS_KEY);

                        //// CosmosDB SQL API
                        Util.WriteToCosmosDbStorageSQLApi(documentClient, cleanCategory, cleanFileName, fileTotalOcr,
                            keyPhrasesV2, keyPhrasesV3,
                            entitiesV2, entitiesV3,
                            pages, uri, documentType, documentSizeInBytes,
                            piiResultV2, piiResultV3,
                            entityTaxonyResult, sentimentV3Prediction,
                            cognitiveServicesApiCalls);
                        Console.WriteLine("\tPersisted: CosmosDB - SQL API");
                    }

                    //// CosmosDB SQL API
                    Util.WriteToLocalStorage(cleanCategory, cleanFileName, fileTotalOcr,
                        keyPhrasesV2, keyPhrasesV3,
                        entitiesV2, entitiesV3,
                        pages, uri, documentType, documentSizeInBytes,
                        piiResultV2, piiResultV3,
                        entityTaxonyResult, sentimentV3Prediction,
                        cognitiveServicesApiCalls);
                    Console.WriteLine("\tPersisted: Local Storage");

                }; // EOF for loop
            }
            catch (Exception e)
            {
                errors.Add(filePath, e);
                Console.WriteLine("!!! ERROR !!!: " + e.ToString());
            }

            // Write File
            // Util.WriteCsvFile(processedTrainingFiles);

            // Distribution of various file types (by extension)
            var fileTypeCounts = fileTypes.GroupBy(a => a.Item2).
                Select(group => new
                {
                    Extension = group.Key.ToUpper(),
                    Count = group.Count()
                }).OrderByDescending(o => o.Count);


            // Print out errors file
            File.Delete(Config.LOCAL_LOCATION_FILES_SOURCE_DOCUMENTS + "Errors.txt");
            using (TextWriter tw = new StreamWriter(Config.LOCAL_LOCATION_FILES_SOURCE_DOCUMENTS + "Errors.txt"))
            {
                foreach (var error in errors)
                {
                    tw.WriteLine(error.Key);
                }
            }

            Console.WriteLine("Number of errors: " + errors.Count);
            Console.ReadLine();
        }
    }
}