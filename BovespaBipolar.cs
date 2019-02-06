using System;
using System.Security.Authentication;
using BovespaBipolar.Domain;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using RestSharp;
using Tweetinvi;

namespace Frai.BovespaFunction
{
    public static class BovespaBipolar
    {
        [FunctionName("BovespaBipolar")]
        public static void Run([TimerTrigger("0 30 10-17 * * 1-5")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            double GetBovespa() {
                var site = ("https://www.alphavantage.co/query?function=TIME_SERIES_INTRADAY&symbol=^BVSP&interval=1min&apikey=" + "YOUR_ALPHAVANTAGE_API_KEY_HERE"); // enter your API key here
                var client = new RestClient(site);
                IRestResponse response = client.Execute(new RestRequest(Method.GET));
                return Convert.ToDouble(JObject.Parse(response.Content)["Time Series (1min)"].First.First.Value<String>("1. open"));
            }

            Auth.SetUserCredentials("CONSUMER_KEY", "CONSUMER_SECRET", "ACCESS_TOKEN", "ACCESS_TOKEN_SECRET");
            MongoClientSettings settings = MongoClientSettings.FromUrl(
              new MongoUrl("YOUR_CONNECTION_STRING_HERE")
            );
            settings.SslSettings = new SslSettings() { EnabledSslProtocols = SslProtocols.Tls12 };
            var mongoClient = new MongoClient(settings);

            var mongoCollection = mongoClient.GetDatabase("Bovespa").GetCollection<Bovespa>("BovespaCollection");

            var lastValueOfBovespa = mongoCollection.Find(new BsonDocument()).ToList()[0].Value;
            var newValueOfBovespa = GetBovespa();

            var bovespa = new Bovespa()
            {
                Id = new Guid("GUID_OF_BOVESPA_IN_DATABASE"),
                Value = newValueOfBovespa,
            };
            var filter = Builders<BsonDocument>.Filter.Eq("Id", "GUID_OF_BOVESPA_IN_DATABASE");
            var updateDef = Builders<Bovespa>.Update.Set(o => o.Value, bovespa.Value);

            mongoCollection.UpdateOne(x => x.Id == bovespa.Id, updateDef);

            if (newValueOfBovespa == lastValueOfBovespa)
            {
                log.LogInformation("A Bovespa não mudou :| - " + newValueOfBovespa + " às " + DateTime.Now.ToString("hh:mm tt"));
            }
            else if(newValueOfBovespa > lastValueOfBovespa)
            {
                Tweet.PublishTweet("A Bovespa subiu :) - " + newValueOfBovespa + " às " + DateTime.Now.ToString("hh:mm tt"));
                log.LogInformation("A Bovespa subiu :) - " + newValueOfBovespa + " às " + DateTime.Now.ToString("hh:mm tt"));
            }
            else
            {
                Tweet.PublishTweet("A Bovespa caiu :( - " + newValueOfBovespa + " às " + DateTime.Now.ToString("hh:mm tt"));
                log.LogInformation("A Bovespa caiu :( - " + newValueOfBovespa + " às " + DateTime.Now.ToString("hh:mm tt"));
            }
        }
    }
}
