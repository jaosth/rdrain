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
            if (!this.memoryCache.TryGetValue("state", out var state))
            {
                return Ok("State unavailable");
            }

            return Ok(state);
        }

        /// <summary>
        /// Get the state
        /// </summary>
        [HttpGet("statetext")]
        [ProducesResponseType(typeof(string), 200)]
        public IActionResult GetStateText()
        {
            if (!this.memoryCache.TryGetValue("state", out var state))
            {
                return Ok("State unavailable");
            }

            return Ok(MakeStatusResponse((RoofDrainState)state));
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
            if (shouldDrain == true)
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

            const string notAvailable = "The roof drain is not currently available. It may be powered down or disconnected.";

            if (requestType == "IntentRequest" && intentName == "AMAZON.HelpIntent")
            {
                // Help
                return Ok(JObject.Parse(simpleResponse.Replace("PLACEHOLDER", "You may ask the roof drain for status or to drain the roof.")));
            }
            else if (requestType == "LaunchRequest" || (requestType == "IntentRequest" && intentName == "Status"))
            {
                // Status
                if (!this.memoryCache.TryGetValue("state", out var state))
                {
                    return Ok(JObject.Parse(simpleResponse.Replace("PLACEHOLDER", notAvailable)));
                }
                else
                {
                    return Ok(JObject.Parse(simpleResponse.Replace("PLACEHOLDER", MakeStatusResponse((RoofDrainState)state))));
                }
            }
            else if (requestType == "IntentRequest" && intentName == "Drain")
            {
                // Drain
                if (!this.memoryCache.TryGetValue("state", out var cachedState))
                {
                    return Ok(JObject.Parse(simpleResponse.Replace("PLACEHOLDER", notAvailable)));
                }

                var state = (RoofDrainState)cachedState;

                if (state.IsDraining)
                {
                    return Ok(JObject.Parse(simpleResponse.Replace("PLACEHOLDER", "The roof drain is already draining. While it is draining, it will continue to re-prime every hour.")));
                }

                var lastPrimedAgo = DateTimeOffset.Now - ((RoofDrainState)state).TimeOfLastPrime;

                if (lastPrimedAgo > TimeSpan.Zero && lastPrimedAgo < TimeSpan.FromMinutes(15))
                {
                    return Ok(JObject.Parse(simpleResponse.Replace("PLACEHOLDER", $"The roof drain primed only {lastPrimedAgo.Minutes} minutes ago. You must wait an additional {(TimeSpan.FromMinutes(15) - lastPrimedAgo).Minutes} minutes before asking the roof drain to start again.")));
                }

                this.memoryCache.Set("drain", (bool?)true, TimeSpan.FromMinutes(5));
                return Ok(JObject.Parse(simpleResponse.Replace("PLACEHOLDER", $"Ok, the roof drain is starting.")));
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
            builder.Append($"The roof drain is {state.Message}. ");

            if (!state.IsDraining)
            {
                var lastDrainedAgo = DateTimeOffset.Now - state.TimeOfLastDrain;
                builder.Append($"It last drained ");
                builder.Append(DurationToText(lastDrainedAgo));
                builder.Append(" ago. ");
            }
            else
            {
                builder.Append("It is currently draining. ");
            }

            var lastPrimedAgo = DateTimeOffset.Now - state.TimeOfLastPrime;
            builder.Append($"It last primed ");
            builder.Append(DurationToText(lastPrimedAgo));
            builder.Append(" ago. ");

            var nextPrime = state.TimeOfNextPrime - DateTimeOffset.Now;
            builder.Append($"It will prime again in ");
            builder.Append(DurationToText(nextPrime));
            builder.Append(". ");

            builder.Append($"The current temperature is {(state.CurrentTemperature * 1.8) + 32} degrees and the drain ");
            builder.Append(state.IsFrozen ? "is" : "is not");
            builder.Append($" frozen. ");

            return builder.ToString();
        }

        /// <summary>
        /// Converts the duration to a reasonable-sounding text
        /// </summary>
        private static string DurationToText(TimeSpan duration)
        {
            var builder = new StringBuilder();

            if (duration < TimeSpan.FromMinutes(1))
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

        private string simpleResponse = @"
{
  ""version"": ""1.0"",
  ""response"": {
    ""outputSpeech"": {
      ""type"": ""PlainText"",
      ""text"": ""PLACEHOLDER""
    },
    ""shouldEndSession"": true
  }
}
";
    }
}
