using Microsoft.WindowsAzure.Storage.Auth;
using WindowsAzureStorage = Microsoft.WindowsAzure.Storage;

namespace CognitiveDocumentEnricher
{
    public static class AzureStorage
    {
        // Blob Storage Account
        public static WindowsAzureStorage.CloudStorageAccount BlobStorageAccount = new WindowsAzureStorage.CloudStorageAccount(
            new StorageCredentials(Config.STORAGE_ACCOUNT_NAME, Config.STORAGE_ACCOUNT_KEY),
            true);
    }
}
