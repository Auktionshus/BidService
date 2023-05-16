using Microsoft.AspNetCore.Mvc;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace BidService.Controllers;

[ApiController]
[Route("[controller]")]
public class BidServiceController : ControllerBase
{
    private readonly ILogger<BidServiceController> _logger;

    public BidServiceController(ILogger<BidServiceController> logger)
    {
        _logger = logger;
    }
}
