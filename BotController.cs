using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Processing;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using WebhookPicFlipBot.Models;

namespace WebhookPicFlipBot.Controllers
{
    [Route("api/bot")]
    [ApiController]
    public class BotController : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> updateAsync ([FromBody] Update update)
        {
            var botClient = new TelegramBotClient("5022293744:AAGcA_cnyzaLTQ6aH-DbgQZgGuEHpfqWHqQ");
            var chatId = update.Message.Chat.Id;
            // Only process Message updates: https://core.telegram.org/bots/api#message
            if (update.Message.Photo != null)
            {
                string connectionString = "mongodb://localhost:27017/PhotoDb";
                var connection = new MongoUrlBuilder(connectionString);
                // получаем клиента для взаимодействия с базой данных
                MongoClient client = new MongoClient(connectionString);
                // получаем доступ к самой базе данных
                IMongoDatabase database = client.GetDatabase(connection.DatabaseName);
                // получаем доступ к файловому хранилищу
                var gridFS = new GridFSBucket(database);
                // обращаемся к коллекции Products
                var photo = database.GetCollection<Photo>("PhotoDb");
                var fileID = update.Message.Photo.Last().FileId;
                var fileExt = botClient.GetFileAsync(fileID).Result.FilePath.Split('.').Last();
                var fileName = fileID + "." + fileExt;
                using (FileStream fileStream = new FileStream(fileName, FileMode.Create))
                {
                    await botClient.GetInfoAndDownloadFileAsync(update.Message.Photo.Last().FileId, fileStream);

                }

                using (Image myImage = await Image.LoadAsync(fileName))
                {
                    myImage.Mutate(img => img.Rotate(RotateMode.Rotate180));
                    myImage.Save(fileName);
                    Configuration.Default.MemoryAllocator = new ArrayPoolMemoryAllocator(40000000);
                }

                using (FileStream stream = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite))
                {
                    InputOnlineFile inputOnlineFile = new InputOnlineFile(stream, fileName);
                    await botClient.SendPhotoAsync(chatId, inputOnlineFile);
                }

                using (FileStream stream = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite))
                {
                    var imageIdFromDb = await gridFS.UploadFromStreamAsync(fileName, stream);
                    var newPhoto = new Photo
                    {
                        ChatId = chatId.ToString(),
                        Date = DateTime.Now,
                        ImageIdFromTelegram = fileID,
                        ImageIdFromDb = imageIdFromDb,
                        ImageName = fileName
                    };
                    photo.InsertOneAsync(newPhoto);
                }



                //make a graphics object from the empty bitmap

                return Ok();
            }

            // Only process text messages
            if (update.Message!.Type != MessageType.Text) { 
            var messageText = update.Message.Text;
            Console.WriteLine(update.Message.Text);

            Console.WriteLine($"Received a '{messageText}' message in chat {chatId}.");

                botClient.SendTextMessageAsync(chatId, "Im recive your message");
            return Ok();
            }
            return BadRequest();
            // Echo received message text

        }
    } 
}
