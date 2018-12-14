﻿using BleemSync.Data.Models;
using System;
using SharpConfig;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using BleemSync.Utilities;
using RestSharp;
using BleemSync.Central.ViewModels;
using Newtonsoft.Json;

namespace BleemSync.Services
{
    public class GameService
    {
        public GameInfo GetGameInfo(int gameId)
        {
            GameInfo game;

            try
            {
                game = GetGameInfoFromFile(gameId);
            }
            catch (Exception e) {
                Console.WriteLine("Game.ini doesn't exist, grabbing from BleemSync Central");
                game = GetGameInfoFromCentral(gameId);
            }

            return game;
        }

        public GameInfo GetGameInfoFromFile(int gameId)
        {
            var gamesDirectory = Filesystem.GetGamesDirectory();

            Configuration config = null;

            var path = Path.Combine(new [] { gamesDirectory, gameId.ToString(), "GameData", "Game.ini"});
            config = Configuration.LoadFromFile(path);

            var section = config["Game"];

            var game = new GameInfo()
            {
                Id = gameId,
                Title = section["Title"].StringValue,
                Publisher = section["Publisher"].StringValue,
                Year = section["Year"].IntValue,
                Players = section["Players"].IntValue,
                DiscIds = section["Discs"].StringValue.Split(',').ToList()
            };

            return game;
        }

        public void WriteGameInfoToFile(GameInfo gameInfo, string path)
        {
            var config = new Configuration();

            config["Game"]["Title"].StringValue = gameInfo.Title.TrimStart('"').TrimEnd('"');
            config["Game"]["Publisher"].StringValue = gameInfo.Publisher.TrimStart('"').TrimEnd('"');
            config["Game"]["Year"].StringValue = gameInfo.Year.ToString();
            config["Game"]["Players"].StringValue = gameInfo.Players.ToString();
            config["Game"]["Discs"].StringValue = String.Join(',', gameInfo.DiscIds);

            config.SaveToFile(Path.Combine(path, "GameData", "Game.ini"));
        }

        public GameInfo GetGameInfoFromCentral(int gameId)
        {
            var gamesDirectory = Filesystem.GetGamesDirectory();

            var files = Directory.GetFiles(Path.Combine(gamesDirectory, gameId.ToString(), "GameData"));
            var discMap = new Dictionary<string, string>();
            var gameInfo = new GameInfo();

            foreach (var file in files)
            {
                if (file.EndsWith(".bin") || file.EndsWith(".iso"))
                {
                    try
                    {
                        var serial = DiscImage.GetSerialNumber(Path.Combine(gamesDirectory, gameId.ToString(), file));

                        Console.Write($"Found serial {serial} from disc image file {file}");

                        var client = new RestClient("http://209.97.151.235");
                        var request = new RestRequest($"api/games/GetBySerial/{serial}");
                        var result = client.Execute<GameDTO>(request);

                        var game = JsonConvert.DeserializeObject<GameDTO>(result.Content);

                        gameInfo.Title = game.Title;
                        gameInfo.Year = game.DateReleased.Year;
                        gameInfo.Publisher = game.Publisher;
                        gameInfo.Players = game.Players;
                        gameInfo.DiscIds = game.Discs.Select(d => d.SerialNumber).ToList();

                        WriteGameInfoToFile(gameInfo, Path.Combine(gamesDirectory, gameId.ToString()));
                    }
                    catch { }
                }
            }

            return gameInfo;
        }
    }
}
