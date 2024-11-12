using System.Text.Json;
using Azure.Storage.Queues.Models;
using BelugaFactory.Common;
using Microsoft.AspNetCore.Http;
using BelugaFactory.Config;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Blob;

namespace BelugaFactory.Services.Storage;
public class AzureStorageService
{
    private readonly CloudStorageAccount _cloudStorageAccount;

    public AzureStorageService()
    {
        _cloudStorageAccount = CloudStorageAccount.Parse(EnvironmentSettings.AzureStorageConnectionString);
    }

    public async Task ProcesQueueMessage(string queueName, Func<SendTranslationRequest, Task> processFunction)
    {
        try
        {
            CloudQueueClient queueClient = _cloudStorageAccount.CreateCloudQueueClient();
        
            Console.WriteLine("CONNECTED-TO-QUEUE");
            
            CloudQueue queue = queueClient.GetQueueReference(queueName);

            while (true)
            {
                CloudQueueMessage message = await queue.GetMessageAsync();
         
                if (message != null)
                {
                    Console.WriteLine($"MESSAGE-FOUND");
                    
                    string blobId = message.AsString;
                    
                    Console.WriteLine(blobId);

                    var req = JsonSerializer.Deserialize<SendTranslationRequest>(message.AsString);
                    
                    if (req ==  null)
                        break;
                    
                    // Chama a função de processamento passada como parâmetro
                    await processFunction(req);

                    // Após o processamento, remova a mensagem da fila
                    await queue.DeleteMessageAsync(message);
                }
                else
                {
                    Console.WriteLine($"MESSAGE-NOT-FOUND");
                    // Aguarda antes de verificar novamente a fila
                    await Task.Delay(TimeSpan.FromSeconds(10));
                }
            }
        }        
        catch (Exception ex)
        {
            throw ex;
        }
    }
    
    public async Task<bool> AddMessage(string Queue, string req)
    {
        try
        {
            // Create the queue client.
            CloudQueueClient queueClient = _cloudStorageAccount.CreateCloudQueueClient();

            // Retrieve a reference to a queue.
            CloudQueue queue = queueClient.GetQueueReference(Queue);

            // Create the queue if it doesn't already exist.
            await queue.CreateIfNotExistsAsync();

            // Create a message and add it to the queue.
            CloudQueueMessage message = new CloudQueueMessage(req);
            await queue.AddMessageAsync(message);

            return true;

        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    public async Task<bool> RemoveFile(string containerName, string fileName)
    {
        try
        {
            CloudBlobClient blobClient = _cloudStorageAccount.CreateCloudBlobClient();

            CloudBlobContainer container = blobClient.GetContainerReference(containerName);

            await container.CreateIfNotExistsAsync();

            CloudBlockBlob blockBlob = container.GetBlockBlobReference(fileName);

            return await blockBlob.DeleteIfExistsAsync();
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    public async Task<string> GetVideoUrl(string containerName, string blobName)
    {
        try
        {
            CloudBlobClient blobClient = _cloudStorageAccount.CreateCloudBlobClient();

            CloudBlobContainer container = blobClient.GetContainerReference(containerName);

            await container.CreateIfNotExistsAsync();

            CloudBlockBlob blockBlob = container.GetBlockBlobReference(blobName);


            SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy();
            sasConstraints.SharedAccessExpiryTime = DateTimeOffset.UtcNow.AddHours(24);
            sasConstraints.Permissions = SharedAccessBlobPermissions.Read;

            //Generate the shared access signature on the container, setting the constraints directly on the signature.
            string sasContainerToken = container.GetSharedAccessSignature(sasConstraints);

            //Return the URI string for the container, including the SAS token.
            //return container.Uri + sasContainerToken;

            return blockBlob.Uri + sasContainerToken;

        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    public async Task UploadFile(IFormFile file, string containerName, string fileName)
    {
        try
        {
            CloudBlobClient blobClient = _cloudStorageAccount.CreateCloudBlobClient();

            CloudBlobContainer container = blobClient.GetContainerReference(containerName);

            await container.CreateIfNotExistsAsync();

            CloudBlockBlob blockBlob = container.GetBlockBlobReference(fileName);

            blockBlob.Properties.ContentType = file.ContentType;

            using (var fileStream = file.OpenReadStream())
            {
                await blockBlob.UploadFromStreamAsync(fileStream);
            }
        }
        catch (Exception ex)
        {

            throw ex;
        }
    }

    public async Task<CloudBlockBlob> UploadBlob(Stream stream, string contentType, string containerName, string fileName)
    {
        try
        {
            CloudBlobClient blobClient = _cloudStorageAccount.CreateCloudBlobClient();

            CloudBlobContainer container = blobClient.GetContainerReference(containerName);

            await container.CreateIfNotExistsAsync();

            CloudBlockBlob blockBlob = container.GetBlockBlobReference(fileName);

            blockBlob.Properties.ContentType = contentType;
            
            await blockBlob.UploadFromStreamAsync(stream);

            return blockBlob;
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }
    
    public async Task<CloudBlockBlob> GetByFilename(string containerName, string fileName)
    {
        try
        {

            CloudBlobClient blobClient = _cloudStorageAccount.CreateCloudBlobClient();

            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(fileName);

            return blockBlob;

        }
        catch (Exception ex)
        {
            throw ex;
        }
    }   
    
    public async Task DownloadBlob(string containerName, string blobName, string targetFilePath)
    {
        try
        {

            CloudBlobClient blobClient = _cloudStorageAccount.CreateCloudBlobClient();

            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(blobName);

            await blockBlob.DownloadToFileAsync(targetFilePath, FileMode.CreateNew);
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    public async Task<Stream> GetStreamByFilename(string containerName, string fileName)
    {
        try
        {
            CloudBlobClient blobClient = _cloudStorageAccount.CreateCloudBlobClient();
            
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);

            CloudBlockBlob blockBlob = container.GetBlockBlobReference(fileName);

            if (await blockBlob.ExistsAsync())
            {
                Stream blobStream = new MemoryStream();
        
                // Faz o download do blob para o stream
                await blockBlob.DownloadToStreamAsync(blobStream);

                // Reseta a posição do stream para o início
                blobStream.Position = 0;

                return blobStream;
            }
            else
            {
                throw new FileNotFoundException("blob not found");
            }
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }
}