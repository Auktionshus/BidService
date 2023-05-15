using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Security.Claims;

namespace BidService.Controllers;

[ApiController]
[Route("[controller]")]
public class BidServiceController : ControllerBase
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    private readonly ILogger<WeatherForecastController> _logger;

    public BidServiceController(ILogger<BidServicesController> logger)
    {
        _logger = logger;
    }

    [HttpGet(Name = "GetBidService")]
    public IEnumerable<BidServices> Get()
    {
        return Enumerable.Range(1, 5).Select(index => new BidServices
        {
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        })
        .ToArray();
    }
}
