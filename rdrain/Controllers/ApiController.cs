namespace RoofDrain.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json.Linq;
    using RoofDrain.Models;
    using System;

    /// <summary>
    /// Main api controller
    /// </summary>
    [Route("api")]
    public class ApiController : Controller
    {
        private readonly string apiKey;
        private readonly IMemoryCache memoryCache;

        /// <summary>
        /// Instantiated by pipeline
        /// </summary>
        public ApiController(IMemoryCache memoryCacheParam, IConfiguration configurationParam)
        {
            this.apiKey = configurationParam["Authorization:ApiKey"];
            this.memoryCache = memoryCacheParam;
        }

        /// <summary>
        /// Get the state
        /// </summary>
        [HttpGet("state")]
        [ProducesResponseType(typeof(RoofDrainState), 200)]
        public IActionResult GetState()
        {
            if(!this.memoryCache.TryGetValue("state", out var state))
            {
                return Ok("State unavailable");
            }

            return Ok(state);
        }

        /// <summary>
        /// Update the state
        /// </summary>
        [HttpPost("state")]
        public IActionResult PostState([FromBody]RoofDrainPostBody body, [FromQuery]string apiKey)
        {
            if (this.apiKey != null && apiKey != this.apiKey)
            {
                return Unauthorized();
            }

            var now = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Pacific Standard Time");

            var newState = new RoofDrainState
            {
                CurrentTemperature = body.CurrentTemperature,
                TimeOfLastPrime = now - TimeSpan.FromMilliseconds(body.CurrentTime - body.TimeOfLastPrime),
                IsDraining = body.IsDraining,
                IsFrozen = body.IsFrozen,
                Message = body.Message,
                TimeOfLastDrain = now - TimeSpan.FromMilliseconds(body.CurrentTime - body.TimeOfLastDrain),
                TimeOfNextPrime = now - TimeSpan.FromMilliseconds(body.CurrentTime - body.TimeOfNextPrime),
                Updated = now
            };

            this.memoryCache.Set("state", newState, TimeSpan.FromMinutes(5));
            return Ok(new { Drain = false });
        }

        /// <summary>
        /// Update the state
        /// </summary>
        [HttpPost("alexa")]
        public IActionResult Alexa([FromBody]JObject body)
        {
            return Ok(JObject.Parse(test));
        }
        
        private string test = @"
{
  ""version"": ""1.0"",
  ""response"": {
    ""outputSpeech"": {
      ""type"": ""PlainText"",
      ""text"": ""Test plain text response""
    },
    ""shouldEndSession"": true
  }
}
";
    }




}
