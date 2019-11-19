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
        public static readonly string COGNITIVE_SERVICES_KEY;
        public static readonly string COGNITIVE_SERVICES_BING_ENTITY_SEARCH_KEY;
        public static readonly string COGNITIVE_SERVICES_REGION;
        public static readonly string COGNITIVE_SERVICES_REGION_TEXT_ANALYTICS;
        public static readonly string COGNITIVE_SERVICES_REGION_TEXT_ANALYTICS_SENTIMENTv3;

        // Cloud Storage
        public static readonly string STORAGE_TABLE_AND_CONTAINER_NAMES;
        public static readonly string STORAGE_ACCOUNT_NAME;
        public static readonly string STORAGE_ACCOUNT_KEY;
        public static readonly string STORAGE_ACCOUNT_TEMP_SAS_KEY;
        public static readonly string STORAGE_ENRICHED_LOCATION;

        // CosmosDb Storage
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
            COGNITIVE_SERVICES_KEY = ConfigurationManager.AppSettings["COGNITIVE_SERVICES_KEY"];
            COGNITIVE_SERVICES_BING_ENTITY_SEARCH_KEY = ConfigurationManager.AppSettings["COGNITIVE_SERVICES_BING_ENTITY_SEARCH_KEY"];
            COGNITIVE_SERVICES_REGION = ConfigurationManager.AppSettings["COGNITIVE_SERVICES_REGION"];
            COGNITIVE_SERVICES_REGION_TEXT_ANALYTICS = ConfigurationManager.AppSettings["COGNITIVE_SERVICES_REGION_TEXT_ANALYTICS"];
            COGNITIVE_SERVICES_REGION_TEXT_ANALYTICS_SENTIMENTv3 = ConfigurationManager.AppSettings["COGNITIVE_SERVICES_REGION_TEXT_ANALYTICS_SENTIMENTv3"];

            // 3) Cloud Storage
            STORAGE_TABLE_AND_CONTAINER_NAMES = ConfigurationManager.AppSettings["STORAGE_TABLE_AND_CONTAINER_NAMES"];
            STORAGE_ACCOUNT_NAME = ConfigurationManager.AppSettings["STORAGE_ACCOUNT_NAME"];
            STORAGE_ACCOUNT_KEY = ConfigurationManager.AppSettings["STORAGE_ACCOUNT_KEY"];
            STORAGE_ACCOUNT_TEMP_SAS_KEY = ConfigurationManager.AppSettings["STORAGE_ACCOUNT_TEMP_SAS_KEY"];
            STORAGE_ENRICHED_LOCATION = string.Format("https://{0}.blob.core.windows.net/{1}/", STORAGE_ACCOUNT_NAME, STORAGE_TABLE_AND_CONTAINER_NAMES.ToLower());

            // 4) CosmosDb Storage
            COSMOSDB_DOCUMENTS_URI = new Uri(ConfigurationManager.AppSettings["COSMOSDB_DOCUMENTS_URI"]);
            COSMOSDB_DOCUMENTS_KEY = ConfigurationManager.AppSettings["COSMOSDB_DOCUMENTS_KEY"];
            COSMOSDB_DOCUMENTS_SELFLINK = ConfigurationManager.AppSettings["COSMOSDB_DOCUMENTS_SELFLINK"];
        }
    }
}
