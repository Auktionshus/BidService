using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using RabbitMQ.Client;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.Commons;

namespace BidService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BidController : ControllerBase
    {
        private readonly ILogger<BidController> _logger;
        private readonly string _hostName;
        private readonly string _secret;
        private readonly string _issuer;
        private readonly string _mongoDbConnectionString;

        public BidController(ILogger<BidController> logger, Environment secrets)
        {
            try
            {
                _secret = secrets.dictionary["Secret"];
                _issuer = secrets.dictionary["Issuer"];
                _mongoDbConnectionString = secrets.dictionary["ConnectionString"];

                _logger = logger;
                _logger.LogInformation($"Secret: {_secret}");
                _logger.LogInformation($"Issuer: {_issuer}");
                _logger.LogInformation($"MongoDbConnectionString: {_mongoDbConnectionString}");
            }
            catch (Exception e)
            {
                _logger.LogError($"Error getting environment variables{e.Message}");
            }
        }

        // Placeholder for the auction data storage
        private static readonly List<Auction> Auctions = new List<Auction>();

        [HttpGet("bid/{id}")]
        public async Task<IActionResult> GetAuction(Guid id)
        {
            MongoClient dbClient = new MongoClient(
                "mongodb+srv://GroenOlsen:BhvQmiihJWiurl2V@auktionshusgo.yzctdhc.mongodb.net/?retryWrites=true&w=majority"
            );
            var collection = dbClient.GetDatabase("auction").GetCollection<Auction>("auctions");
            Auction auction = await collection.Find(a => a.Id == id).FirstOrDefaultAsync();

            if (auction == null)
            {
                return NotFound($"Auction with Id {id} not found.");
            }
            return Ok(auction);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetAuth()
        {
            return Ok("You're authorized");
        }

        [Authorize]
        [HttpPost("{id}/placeBid")]
        public async Task<IActionResult> PlaceBid([FromBody] BidDTO bid)
        {
            try
            {
                _logger.LogInformation($"Bid received for auction with id: {bid.Auction}");
                if (bid != null)
                {
                    _logger.LogInformation("Place bid called");
                    try
                    {
                        // Connects to RabbitMQ
                        var factory = new ConnectionFactory { HostName = _hostName };

                        using var connection = factory.CreateConnection();
                        using var channel = connection.CreateModel();

                        channel.ExchangeDeclare(exchange: "topic_fleet", type: ExchangeType.Topic);

                        // Serialize to JSON
                        string message = JsonSerializer.Serialize(bid);

                        // Convert to byte-array
                        var body = Encoding.UTF8.GetBytes(message);

                        // Send to queue
                        channel.BasicPublish(
                            exchange: "topic_fleet",
                            routingKey: "bids.create",
                            basicProperties: null,
                            body: body
                        );

                        _logger.LogInformation("Bid placed and sent to RabbitMQ");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInformation("error " + ex.Message);
                        return StatusCode(500);
                    }
                    return Ok(bid);
                }
                else
                {
                    return BadRequest("Bid object is null");
                }
            }
            catch
            {
                _logger.LogInformation("An error occurred while trying to create item");
                return BadRequest();
            }
        }

        [HttpGet("version")]
        public IEnumerable<string> Get()
        {
            var properties = new List<string>();
            var assembly = typeof(Program).Assembly;
            foreach (var attribute in assembly.GetCustomAttributesData())
            {
                _logger.LogInformation("Tilføjer " + attribute.AttributeType.Name);
                properties.Add($"{attribute.AttributeType.Name} - {attribute.ToString()}");
            }
            return properties;
        }
    }
}
