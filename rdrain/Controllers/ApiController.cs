using Microsoft.AspNetCore.Mvc;
using rdrain.Models;

namespace rdrain.Controllers
{
    [Route("api")]
    public class ApiController : Controller
    {
        [HttpGet("state")]
        public State GetState()
        {
            return new State { Name = "foo" };
        }
    }
}
