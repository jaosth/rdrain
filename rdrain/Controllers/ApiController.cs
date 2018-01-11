namespace RoofDrain.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json.Linq;
    using RoofDrain.Models;
    using System;
    using System.Text;

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

            var shouldDrain = (bool?)this.memoryCache.Get("drain");
            if(shouldDrain == true)
            {
                this.memoryCache.Remove("drain");
            }

            return Ok(new { Drain = (shouldDrain == true) });
        }

        /// <summary>
        /// Update the state
        /// </summary>
        [HttpPost("alexa")]
        public IActionResult Alexa([FromBody]JObject body)
        {
            var applicationId = body.Value<JObject>("session").Value<JObject>("application").Value<string>("applicationId");

            if (applicationId != "amzn1.ask.skill.44a4aeea-e9c9-428d-a034-7cb600a4363a")
            {
                return NotFound();
            }
            
            var requestType = body.Value<JObject>("request").Value<string>("type");
            var intentName = body.Value<JObject>("request").Value<JObject>("intent")?.Value<string>("name");

            if(requestType == "IntentRequest" && intentName == "AMAZON.HelpIntent")
            {
                // Help
                return Ok(JObject.Parse(helpResponse));
            }
            else if (requestType == "LaunchRequest" || (requestType == "IntentRequest" && intentName == "Status"))
            {
                // Status
                if (!this.memoryCache.TryGetValue("state", out var state))
                {
                    return Ok(JObject.Parse(statusNotAvailableResponse));
                }
                else
                {
                    return Ok(JObject.Parse(String.Format(statusResponseFormatString, MakeStatusResponse((RoofDrainState)state))));
                }
            }
            else if (requestType == "IntentRequest" && intentName == "Drain")
            {
                // Drain
                this.memoryCache.Set("drain", (bool?)true, TimeSpan.FromMinutes(5));
                return Ok(JObject.Parse(drainResponse));
            }
            else
            {
                return Ok();
            }
        }

        /// <summary>
        /// Make a human-understandable status response from the current state.
        /// </summary>
        private string MakeStatusResponse(RoofDrainState state)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"The roof drain is {state.Message}.");

            if(!state.IsDraining)
            {
                var lastDrainedAgo = DateTimeOffset.Now - state.TimeOfLastDrain;
                builder.Append($"It last drained ");
                builder.Append(DurationToText(lastDrainedAgo));
                builder.AppendLine(" ago.");
            }
            else
            {
                builder.AppendLine("It is currently draining.");
            }

            var lastPrimedAgo = DateTimeOffset.Now - state.TimeOfLastPrime;
            builder.Append($"It last primed ");
            builder.Append(DurationToText(lastPrimedAgo));
            builder.AppendLine(" ago.");

            var nextPrime = state.TimeOfNextPrime - DateTimeOffset.Now;
            builder.Append($"It will prime again in ");
            builder.Append(DurationToText(lastPrimedAgo));
            builder.AppendLine(".");

            builder.Append($"The current temperature is {state.CurrentTemperature} degrees Celsius and the drain ");
            builder.Append(state.IsFrozen ? "is" : "is not");
            builder.AppendLine($" frozen.");

            return builder.ToString();
        }

        /// <summary>
        /// Converts the duration to a reasonable-sounding text
        /// </summary>
        private static string DurationToText(TimeSpan duration)
        {
            var builder = new StringBuilder();

            if(duration < TimeSpan.FromMinutes(1))
            {
                duration = TimeSpan.FromMinutes(1);
            }

            var days = duration.Days;
            var hours = duration.Hours;
            var minutes = duration.Minutes;

            if (days > 0)
            {
                builder.Append($"{days} days ");
            }

            if (hours > 0)
            {
                builder.Append($"{hours} hours ");
            }

            if (minutes > 0)
            {
                builder.Append($"{minutes} minutes ");
            }

            return builder.ToString();
        }

        private string helpResponse = @"
{
  ""version"": ""1.0"",
  ""response"": {
    ""outputSpeech"": {
      ""type"": ""PlainText"",
      ""text"": ""You may ask the roof drain for status or to drain the roof.""
    },
    ""shouldEndSession"": true
  }
}
";

        private string statusResponseFormatString = @"
{
  ""version"": ""1.0"",
  ""response"": {
    ""outputSpeech"": {
      ""type"": ""PlainText"",
      ""text"": ""{0}""
    },
    ""shouldEndSession"": true
  }
}
";

        private string statusNotAvailableResponse = @"
{
  ""version"": ""1.0"",
  ""response"": {
    ""outputSpeech"": {
      ""type"": ""PlainText"",
      ""text"": ""Status is not currently available. The roof drain may be powered down or disconnected.""
    },
    ""shouldEndSession"": true
  }
}
";

        private string drainResponse = @"
{
  ""version"": ""1.0"",
  ""response"": {
    ""outputSpeech"": {
      ""type"": ""PlainText"",
      ""text"": ""Ok, the roof drain will drain the roof.""
    },
    ""shouldEndSession"": true
  }
}
";

    }
}
