using System;
using System.Configuration;

namespace CognitiveDocumentEnricher
{
    public static class Config
    {
        // Location of the source document files to process
        public static readonly string LOCAL_LOCATION_FILES_SOURCE_DOCUMENTS;
        public static readonly string LOCAL_LOCATION_FILES_PROCESSED_OUTPUTS;
        public static readonly string LOCAL_LOCATION_FILES_ERRORS;

        // Cognitive Services
        public static readonly bool USE_COGNITIVE_SERVICES_V2;
        public static readonly bool USE_COGNITIVE_SERVICES_V3;
        public static readonly bool USE_COGNITIVE_SERVICES_BING_ENTITY_SEARCH;

        public static readonly string COGNITIVE_SERVICES_KEY;
        public static readonly string COGNITIVE_SERVICES_BING_ENTITY_SEARCH_KEY;
        public static readonly string COGNITIVE_SERVICES_REGION;
        public static readonly string COGNITIVE_SERVICES_REGION_URI;

        // Cloud Storage
        public static readonly bool USE_AZURE_BLOB_STORAGE;
        public static readonly bool USE_AZURE_TABLE_STORAGE;

        public static readonly string STORAGE_TABLE_AND_CONTAINER_NAMES;
        public static readonly string STORAGE_ACCOUNT_NAME;
        public static readonly string STORAGE_ACCOUNT_KEY;
        public static readonly string STORAGE_ACCOUNT_TEMP_SAS_KEY;
        public static readonly string STORAGE_ENRICHED_LOCATION;

        // CosmosDb Storage
        public static readonly bool USE_COSMOSDB_STORAGE;

        public static readonly Uri COSMOSDB_DOCUMENTS_URI;
        public static readonly string COSMOSDB_DOCUMENTS_KEY;
        public static readonly string COSMOSDB_DOCUMENTS_SELFLINK;

        static Config()
        {
            // Read the configuration values, keys from App.config

            // 1) Local Files
            LOCAL_LOCATION_FILES_SOURCE_DOCUMENTS = ConfigurationManager.AppSettings["LOCAL_LOCATION_FILES_SOURCE_DOCUMENTS"];
            LOCAL_LOCATION_FILES_PROCESSED_OUTPUTS = ConfigurationManager.AppSettings["LOCAL_LOCATION_FILES_PROCESSED_OUTPUTS"];
            LOCAL_LOCATION_FILES_ERRORS = ConfigurationManager.AppSettings["LOCAL_LOCATION_FILES_ERRORS"];

            // 2) Cognitive Services
            USE_COGNITIVE_SERVICES_V2 = Convert.ToBoolean(ConfigurationManager.AppSettings["USE_COGNITIVE_SERVICES_V2"]);
            USE_COGNITIVE_SERVICES_V3 = Convert.ToBoolean(ConfigurationManager.AppSettings["USE_COGNITIVE_SERVICES_V3"]);
            USE_COGNITIVE_SERVICES_BING_ENTITY_SEARCH = Convert.ToBoolean(ConfigurationManager.AppSettings["USE_COGNITIVE_SERVICES_BING_ENTITY_SEARCH"]);

            COGNITIVE_SERVICES_KEY = ConfigurationManager.AppSettings["COGNITIVE_SERVICES_KEY"];
            COGNITIVE_SERVICES_BING_ENTITY_SEARCH_KEY = ConfigurationManager.AppSettings["COGNITIVE_SERVICES_BING_ENTITY_SEARCH_KEY"];
            COGNITIVE_SERVICES_REGION_URI = ConfigurationManager.AppSettings["COGNITIVE_SERVICES_REGION_URI"];


            // 3) Cloud Storage
            USE_AZURE_BLOB_STORAGE = Convert.ToBoolean(ConfigurationManager.AppSettings["USE_AZURE_BLOB_STORAGE"]);
            USE_AZURE_TABLE_STORAGE = Convert.ToBoolean(ConfigurationManager.AppSettings["USE_AZURE_TABLE_STORAGE"]);

            STORAGE_TABLE_AND_CONTAINER_NAMES = ConfigurationManager.AppSettings["STORAGE_TABLE_AND_CONTAINER_NAMES"];
            STORAGE_ACCOUNT_NAME = ConfigurationManager.AppSettings["STORAGE_ACCOUNT_NAME"];
            STORAGE_ACCOUNT_KEY = ConfigurationManager.AppSettings["STORAGE_ACCOUNT_KEY"];
            STORAGE_ACCOUNT_TEMP_SAS_KEY = ConfigurationManager.AppSettings["STORAGE_ACCOUNT_TEMP_SAS_KEY"];
            STORAGE_ENRICHED_LOCATION = string.Format("https://{0}.blob.core.windows.net/{1}/", STORAGE_ACCOUNT_NAME, STORAGE_TABLE_AND_CONTAINER_NAMES.ToLower());

            // 4) CosmosDb Storage
            USE_COSMOSDB_STORAGE = Convert.ToBoolean(ConfigurationManager.AppSettings["USE_COSMOSDB_STORAGE"]);

            if (USE_COSMOSDB_STORAGE)
            {
                COSMOSDB_DOCUMENTS_URI = new Uri(ConfigurationManager.AppSettings["COSMOSDB_DOCUMENTS_URI"]);
                COSMOSDB_DOCUMENTS_KEY = ConfigurationManager.AppSettings["COSMOSDB_DOCUMENTS_KEY"];
                COSMOSDB_DOCUMENTS_SELFLINK = ConfigurationManager.AppSettings["COSMOSDB_DOCUMENTS_SELFLINK"];
            }
        }
    }
}
