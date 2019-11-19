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
            Dictionary<string, Exception> errors = new Dictionary<string, Exception>();
            Dictionary<string, int> longDocuments = new Dictionary<string, int>();
            Dictionary<string, Tuple<string, string, string>> processedTrainingFiles = new Dictionary<string, Tuple<string, string, string>>(1700);

            var scoringTableEntities = new List<Microsoft.Azure.CosmosDB.Table.DynamicTableEntity>();
            var topThreeClassificationNamesDictionary = new Dictionary<string, List<string>>();
            var topThreeClassificationProbabilitiesDictionary = new Dictionary<string, List<double>>();
            var piiResult = new PIIResult();

            // Cosmos DB Objects Init

            Console.WriteLine("Extracting content from documents");

            // List of types of extensions
            var fileTypes = new List<Tuple<string, string>>();
            var currentFilesDirectory = string.Empty;
            var filePath = string.Empty;

            // Initialize blob & table client

            // Azure Storage Objects Init
            var cloudStorageBloblClient = Util.BlobStorageAccount.CreateCloudBlobClient();
            var tableStorageClient = Util.BlobStorageAccount.CreateCloudTableClient();
            Microsoft.WindowsAzure.Storage.Table.CloudTable table = tableStorageClient.GetTableReference(Config.STORAGE_TABLE_AND_CONTAINER_NAMES);
            table.CreateIfNotExists();
            // Cosmos DB Objects Init
            // Note: Not Used
            // var cosmosDbTableStorageClient = Util.CosmosDbStorageAccount.CreateCloudTableClient();
                //var cosmosDbTable = cosmosDbTableStorageClient.GetTableReference(Config.STORAGE_TABLE_AND_CONTAINER_NAMES);
                //cosmosDbTable.CreateIfNotExists();
            // Cosmos DB - Documents Client
            DocumentClient documentClient = new DocumentClient(Config.COSMOSDB_DOCUMENTS_URI, Config.COSMOSDB_DOCUMENTS_KEY);

            // ML Scoring Table
            // Note: Note Used
            // var cosmosDbTableStorageClientScoringTable = Util.CosmosDbStorageAccountScoringTable.CreateCloudTableClient();
            // Microsoft.Azure.CosmosDB.Table.CloudTable cosmosDbTableScoringTable = null; // cosmosDbTableStorageClientScoringTable.GetTableReference(Config.STORAGE_TABLENAME_DOCUMENTSCORING);

            // Test or Full
            var runTestFiles = false;

            // read files
            if (runTestFiles)
            {
                currentFilesDirectory = Config.LOCAL_LOCATION_FILES_ERRORS;
            }
            else
            {
                currentFilesDirectory = Config.LOCAL_LOCATION_FILES_SOURCE_DOCUMENTS;
            }

            var docExt = new List<string> { ".DOC", ".DOCX", ".DOTX", ".DOT" };
            var files = Util.DirectoryTraverseForFiles(currentFilesDirectory).ToList();

            try
            {
                // 1) Read the ML Scoring Table
                //Console.WriteLine("Reading Scoring Table...");
                //if (cosmosDbTableScoringTable != null)
                //{
                //    var query = new Microsoft.Azure.CosmosDB.Table.TableQuery<Microsoft.Azure.CosmosDB.Table.DynamicTableEntity>();
                //    var result = cosmosDbTableScoringTable.ExecuteQuerySegmented<Microsoft.Azure.CosmosDB.Table.DynamicTableEntity>(query, null);
                //    scoringTableEntities.AddRange(result.ToList());

                //    if (result.ContinuationToken != null)
                //    {
                //        var result2 = cosmosDbTableScoringTable.ExecuteQuerySegmented<Microsoft.Azure.CosmosDB.Table.DynamicTableEntity>(query, result.ContinuationToken);
                //        scoringTableEntities.AddRange(result2.ToList());
                //    }

                //    for (int i = 0; i != scoringTableEntities.Count; i++)
                //    {
                //        var scoredLabels = scoringTableEntities[i].Properties.Where(a => a.Key != "PartitionKey" && a.Key != "RowKey" && a.Key != "ScoredLabels")
                //            .OrderByDescending(a => Convert.ToDouble(a.Value.DoubleValue)).Take(3).ToList();
                //        var rowKey = scoringTableEntities[i].Properties.Where(a => a.Key == "RowKey").ToList()[0].Value.ToString();

                //        var classes = scoredLabels.Select(a => a.Key).ToList();
                //        var probabilities = scoredLabels.Select(a => Convert.ToDouble(a.Value.DoubleValue)).ToList();

                //        topThreeClassificationNamesDictionary.Add(rowKey, classes);
                //        topThreeClassificationProbabilitiesDictionary.Add(rowKey, probabilities);
                //    }
                //}
                //Console.WriteLine("Reading Scoring Table...Finished");


                Console.WriteLine("--------------------------------");
                Console.WriteLine("Processing Files...");

                // 2) Process Files
                for (int fileNum = 0; fileNum != files.Count; fileNum++)
                {
                    // retrieve the file
                    filePath = files[fileNum];
                    //var filePath = activeFile;
                    // retrieve the directory & file name
                    var categoryAndFileNames = filePath.Replace(currentFilesDirectory, string.Empty)
                        .Split(new string[] { "\\" }, StringSplitOptions.None);
                    var originalDocumentExtension = Path.GetExtension(filePath).ToUpper();
                    // Document Attributes (inserted into Cloud Table)
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

                            Console.WriteLine("\tCracking Pages...");
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
                        else if (htmlDocuments.Contains(originalDocumentExtension))
                            //|| wordDocuments.Contains(originalDocumentExtension))
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

                            // Done here as Aspose workaround
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
                        using (Bitmap image = (Bitmap)System.Drawing.Image.FromStream(imageStream))
                        {
                            var resizedImage = Util.ResizeImageForCognitiveOCR(image);
                            resizedImage.Save(fullFileImageName, ImageFormat.Png);
                        }

                        var basePath = cleanCategory + @"\" + cleanFileName + @"\" + cleanFileName + (i + 1);

                        // Set up cloud path for JSON content
                        var enrichmentContainer = cloudStorageBloblClient.GetContainerReference(Config.STORAGE_TABLE_AND_CONTAINER_NAMES.ToLower());
                        uri = enrichmentContainer.StorageUri.PrimaryUri + @"/" + cleanCategory + @"/" + cleanFileName + @"/";
                        var cloudImagePath = (basePath + ".png").ToLower();
                        var cloudOcrPath = (basePath + ".json").ToLower();
                        var trainingImage = enrichmentContainer.GetBlockBlobReference(cloudImagePath);
                        var trainingImageOcr = enrichmentContainer.GetBlockBlobReference(cloudOcrPath);

                        // Send image to the cloud
                        using (var fs = new FileStream(fullFileImageName, FileMode.Open))
                        {
                            trainingImage.UploadFromStream(fs);
                        }

                        // Retrieve OCR keyPhraseResult and upload JSON to cloud
                        // Uses API based on cloud storage
                        var imageUrl = trainingImage.Uri.AbsoluteUri + Config.STORAGE_ACCOUNT_TEMP_SAS_KEY;
                        var ocrResult = Util.OCRResult(imageUrl, "v2.0").Result;
                        trainingImageOcr.UploadText(ocrResult.Item1);

                        var ocrString = ocrResult.Item2.ToString();
                        imagePagesOcr.Add(ocrString);

                        // Console.WriteLine("Number of OCR Regions Found - " + ocrResult.Item2.regions.Count);
                    }

                    var ocrPhrases = new List<KeyValuePair<string, string>>();

                    foreach (var ocrItem in imagePagesOcr)
                    {
                        // remove the trial items from Aspose
                        var tempOcrItem = ocrItem.
                            Replace("Evaluation Only. Created with Aspose.PDF. Copyright 2002-2018 Aspose Pty Ltd.", string.Empty).
                            Replace("Evaluation Only. Created with Aspose.PDF", string.Empty).
                            Replace("Evaluation Only. Created with Aspose.Words", string.Empty).
                            Replace("Evaluation Only. Created with Aspose.Cells", string.Empty).
                            Replace("Created with Aspose.Cells for .NET.Copyright 2003 - 2018", string.Empty).
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
                            Replace("Evaluation Only. Created with Aspose.PD", string.Empty);
                        // add to main OCR file
                        fileTotalOcr += tempOcrItem + System.Environment.NewLine;

                        // Add to phrases to process (english)
                        if (tempOcrItem.Length > 5000)
                        {
                            tempOcrItem = tempOcrItem.Substring(0, 5000);
                        }

                        ocrPhrases.Add(new KeyValuePair<string, string>("en", tempOcrItem));
                    }

                    var keyPhraseResult = Util.GetKeyPhrasesAndEntities(ocrPhrases);
                    Console.WriteLine("\tPII Information...");
                    piiResult = Util.GetPIIResponse(ocrPhrases);

                    // Key Phrases
                    Console.WriteLine("\tKey Phrases & Entities...");
                    var keyPhrases = keyPhraseResult.Item1.Documents.SelectMany(i => i.KeyPhrases).Where(a => Helpers.IsEntity(a)).Distinct().ToList();
                    var distinctKeyPhraseString = string.Join(" ;;;; ", keyPhrases.Distinct().ToArray());
                    keyPhraseString = string.Join(" ;;;; ", keyPhrases.ToArray());

                    // Entities
                    var entitiesRecords = keyPhraseResult.Item2.Documents.SelectMany(i => i.Entities).Where(a => Helpers.IsEntity(a.Name) ).Distinct().ToList();
                    var entities = entitiesRecords.Select(i => i.Name.Replace(System.Environment.NewLine, string.Empty).Trim()).Distinct().ToList();
                    var distinctEntitiesString = string.Join(" ;;;; ", entities.Distinct().ToArray());
                    entitiesString = string.Join(" ;;;; ", entities.ToArray());

                    // Sentiment Analysis
                    Console.WriteLine("\tSentiment Analysis...");
                    var textAnalyticsInput = new TextAnalyticsInput()
                    {
                        Id = "1",
                        Text = fileTotalOcr.Length > 5100 ? fileTotalOcr.Substring(0, 5100) : fileTotalOcr
                    };
                    var textAnalyticsInputs = new List<TextAnalyticsInput> { textAnalyticsInput };
                    var sentimentV3Prediction = Util.SentimentV3PreviewPredictAsync(textAnalyticsInputs).Result;

                    Console.WriteLine("\tRetrieving Bing Entitites...");
                    List<BingEntityData> entityTaxonyResult = Util.GetEntitiesSearchResponse(entities);
                    Console.WriteLine("\tFinished Retrieving Bing Entitites.");

                    // var len1 = distinctKeyPhraseString.Length;
                    // var len2 = keyPhraseString.Length;
                    
                    //// Build the keyphrase string to be persisted
                    //for (int k = 0; k != keyPhraseResult.Documents.Count; k++)
                    //{
                    //    if (keyPhraseResult.Documents[k].KeyPhrases.Count > 0)
                    //    {
                    //        var tempPhraseString = string.Join(" ;;;; ", keyPhraseResult.Documents[k].KeyPhrases.ToArray());
                    //        keyPhraseString += tempPhraseString;
                    //        // Console.WriteLine($"Key phrases of \"{ocrPhrases[k].Value}\": {keyPhraseString}");
                    //    }
                    //}

                    // Add up OCR pages & Process Key Entities
                    processedTrainingFiles.Add(cleanFileName, new Tuple<string, string, string>(cleanCategory, fileTotalOcr, keyPhraseString));

                    // Azure Table Storage
                    // Note: most attributes are truncated due to limitations for Azure Table Storage
                    // For large documents use: CosmosDb or blob storage
                    Util.WriteToBlobStorageTable(table, cleanCategory, cleanFileName, fileTotalOcr,
                        keyPhraseString, distinctKeyPhraseString,
                        entitiesString, distinctEntitiesString,
                        pages, uri, documentType, documentSizeInBytes,
                        piiResult, entityTaxonyResult, sentimentV3Prediction);
                    Console.WriteLine("\tPersisted: Azure Table Storage");

                    //// CosmosDB SQL API
                    Util.WriteToCosmosDbStorageSQLApi(documentClient, cleanCategory, cleanFileName, fileTotalOcr,
                        keyPhraseString, distinctKeyPhraseString,
                        entitiesString, distinctEntitiesString,
                        pages, uri, documentType, documentSizeInBytes,
                        topThreeClassificationNamesDictionary, topThreeClassificationProbabilitiesDictionary,
                        piiResult, entityTaxonyResult, sentimentV3Prediction);
                    Console.WriteLine("\tPersisted: CosmosDB - SQL API");

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