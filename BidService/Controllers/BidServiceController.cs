using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using MongoDB.Driver;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;

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

        public BidController(ILogger<BidController> logger, IConfiguration config)
        {
            _mongoDbConnectionString = config["MongoDbConnectionString"];
            _hostName = config["HostnameRabbit"];
            _secret = config["Secret"];
            _issuer = config["Issuer"];

            _logger = logger;
            _logger.LogInformation($"Connection: {_hostName}");
        }

        // Placeholder for the auction data storage
        private static readonly List<Auction> Auctions = new List<Auction>();

        [HttpGet("{id}")]
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
        [HttpPost("{id}/placeBid")]
        public async Task<IActionResult> PlaceBid(Guid id, [FromBody] Bid bid)
        {
            MongoClient dbClient = new MongoClient(
                "mongodb+srv://GroenOlsen:BhvQmiihJWiurl2V@auktionshusgo.yzctdhc.mongodb.net/?retryWrites=true&w=majority"
            );
            var collection = dbClient.GetDatabase("auction").GetCollection<Auction>("auctions");
            var bidCollection = dbClient.GetDatabase("Bid").GetCollection<Bid>("Bids");

            Auction auction = await collection.Find(a => a.Id == id).FirstOrDefaultAsync();

            if (auction == null)
            {
                return NotFound($"Auction with Id {id} not found.");
            }

            if (auction.BidHistory == null)
            {
                auction.BidHistory = new List<Bid>();
            }

            if (bid.Amount <= auction.CurrentPrice)
            {
                return BadRequest(
                    $"Bid amount must be higher than {auction.CurrentPrice} the current price."
                );
            }

            bid.Id = Guid.NewGuid();
            bid.Date = DateTime.UtcNow;
            auction.BidHistory.Add(bid);
            auction.CurrentPrice = bid.Amount;

            var update = Builders<Auction>.Update
                .Set(a => a.CurrentPrice, bid.Amount)
                .Push(a => a.BidHistory, bid);

            await collection.UpdateOneAsync(a => a.Id == id, update);

            await bidCollection.InsertOneAsync(bid);

            return CreatedAtAction(nameof(GetAuction), new { id = id }, auction);
        }
    }
}
