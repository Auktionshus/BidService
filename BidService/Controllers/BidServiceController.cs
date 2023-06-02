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

        private MongoClient dbClient;

        public BidController(
            ILogger<BidController> logger,
            Environment secrets,
            IConfiguration config
        )
        {
            try
            {
                _hostName = config["HostnameRabbit"];
                _secret = secrets.dictionary["Secret"];
                _issuer = secrets.dictionary["Issuer"];
                _mongoDbConnectionString = secrets.dictionary["ConnectionString"];

                _logger = logger;
                _logger.LogInformation($"Secret: {_secret}");
                _logger.LogInformation($"Issuer: {_issuer}");
                _logger.LogInformation($"MongoDbConnectionString: {_mongoDbConnectionString}");

                // Connect to MongoDB
                dbClient = new MongoClient(_mongoDbConnectionString);
            }
            catch (Exception e)
            {
                _logger.LogError($"Error getting environment variables{e.Message}");
            }
        }

        /// <summary>
        /// Gets bid from id
        /// </summary>
        /// <param name="id">The id of the bid</param>
        /// <returns>A bid</returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetBid(Guid id)
        {
            try
            {
                var collection = dbClient.GetDatabase("Bid").GetCollection<Bid>("Bids");
                Bid bid = await collection.Find(a => a.Id == id).FirstOrDefaultAsync();

                if (bid == null)
                {
                    return NotFound($"Bid with Id: {id} not found.");
                }
                return Ok(bid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Creates a bid
        /// </summary>
        /// <param name="bid">BidDTO</param>
        /// <returns>The created bid</returns>
        [Authorize]
        [HttpPost("{id}/placeBid")]
        public async Task<IActionResult> PlaceBid([FromBody] BidDTO bid)
        {
            _logger.LogInformation(
                $"Bid received for auction with id: {bid.Auction}, from user: {bid.Bidder} for {bid.Amount}"
            );
            if (bid != null)
            {
                // Check if Auction exists
                Auction auction = null;
                try
                {
                    var auctionCollection = dbClient
                        .GetDatabase("auction")
                        .GetCollection<Auction>("auctions");
                    auction = auctionCollection.Find(a => a.Id == bid.Auction).FirstOrDefault();
                    _logger.LogInformation($" [x] Received auction with id: {bid.Auction}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        $"An error occurred while querying the auction collection: {ex}"
                    );
                }
                // Check if user exists
                User user = null;
                try
                {
                    var userCollection = dbClient.GetDatabase("User").GetCollection<User>("Users");
                    user = userCollection.Find(u => u.Id == bid.Bidder).FirstOrDefault();
                    _logger.LogInformation($" [x] Received user with id: {user.Id}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"An error occurred while querying the user collection: {ex}");
                }
                if (user == null)
                {
                    _logger.LogInformation("User not found");
                    return BadRequest("User not found");
                }
                else if (auction == null)
                {
                    return NotFound($"Auction with Id {bid.Auction} not found.");
                }
                else
                {
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
            }
            else
            {
                return BadRequest("Bid object is null");
            }
        }

        /// <summary>
        /// Gets the version information of the service
        /// </summary>
        /// <returns>A list of version information</returns>
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
