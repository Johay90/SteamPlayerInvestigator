using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace SteamPlayerInvestigator.Classes
{
    public class PlayerProfile
    {
        public string Nickname { get; set; }
        public string RealName { get; set; }
        public int CityCode { get; set; }
        public int CountryCode { get; set; }
        public int StateCode { get; set; }
        public int PrimaryGroupId { get; set; }
        public string MostPlayedGame { get; set; }
        public float LinkPrediction { get; set; }
    }

    public class PlayerProfileData
    {
        [LoadColumn(0)] public string Nickname { get; set; }
        [LoadColumn(1)] public string RealName { get; set; }
        [LoadColumn(2)] public int CityCode { get; set; }
        [LoadColumn(3)] public int CountryCode { get; set; }
        [LoadColumn(4)] public int StateCode { get; set; }
        [LoadColumn(5)] public int PrimaryGroupId { get; set; }
        [LoadColumn(6)] public string MostPlayedGame { get; set; }
        [LoadColumn(7), ColumnName("Label")] public float LinkPrediction { get; set; }
    }

    public class LinkPredictionModel
    {
        private MLContext mlContext;
        private ITransformer mlModel;

        public LinkPredictionModel(string modelPath)
        {
            mlContext = new MLContext();
            mlModel = mlContext.Model.Load(modelPath, out var modelInputSchema);
        }

        public float PredictLink(PlayerProfile player1, PlayerProfile player2)
        {
            var player1Data = new PlayerProfileData
            {
                Nickname = player1.Nickname,
                RealName = player1.RealName,
                CityCode = player1.CityCode,
                CountryCode = player1.CountryCode,
                StateCode = player1.StateCode,
                PrimaryGroupId = player1.PrimaryGroupId,
                MostPlayedGame = player1.MostPlayedGame,
                LinkPrediction = 0
            };

            var player2Data = new PlayerProfileData
            {
                Nickname = player2.Nickname,
                RealName = player2.RealName,
                CityCode = player2.CityCode,
                CountryCode = player2.CountryCode,
                StateCode = player2.StateCode,
                PrimaryGroupId = player2.PrimaryGroupId,
                MostPlayedGame = player2.MostPlayedGame,
                LinkPrediction = 0
            };

            var playersData = new List<PlayerProfileData> { player1Data, player2Data };
            var playersDataView = mlContext.Data.LoadFromEnumerable(playersData);

            var predictions = mlModel.Transform(playersDataView);
            var predictionFunction = mlContext.Model.CreatePredictionEngine<PlayerProfileData, LinkPrediction>(mlModel);

            return predictionFunction.Predict(player2Data).PredictedLink;
        }
    }
    
    public class LinkPrediction
    {
        [ColumnName("Score")]
        public float PredictedLink { get; set; }
    }
    
}