﻿using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using VainBotTwitch.Classes;
using VainBotTwitch.Commands;

namespace VainBotTwitch
{
    internal class Program
    {
        private static IConfiguration config;
        private static TwitchClient client;
        private static TwitchAPI api;
        private static readonly HttpClient httpClient = new HttpClient();
        private static readonly Random rng = new Random();
        private static readonly Regex validZip = new Regex("^[0-9]{5}$");

        private static void Main() => new Program().Run();

        public void Run()
        {
            config = new ConfigurationBuilder()
                .AddJsonFile("config.json")
                .Build();

            api = new TwitchAPI();
            api.Settings.ClientId = config["twitchClientId"];
            api.Settings.AccessToken = config["twitchOauth"];
            client = new TwitchClient();
            client.Initialize(new ConnectionCredentials(config["twitchUsername"], config["twitchOauth"]), config["twitchChannel"]);

            client.AddChatCommandIdentifier('!');
            client.OnChatCommandReceived += CommandHandler;
            //client.ChatThrottler = new MessageThrottler(client, 2, new TimeSpan(0, 0, 5));

            client.Connect();

            while (true)
            {
                Thread.Sleep(1000);
            }
        }

        private async void CommandHandler(object sender, OnChatCommandReceivedArgs e)
        {
            if (e.Command.ChatMessage.BotUsername == e.Command.ChatMessage.Username)
                return;

            var command = e.Command.CommandText.ToLower();
            var argCount = e.Command.ArgumentsAsList.Count;

            switch (command)
            {
                case "slothfact":
                case "slothfacts":
                    SlothFacts(e.GetChannel(client));
                    return;

                case "woppy":
                case "weather":
                    await WoppyWeather(e).ConfigureAwait(false);
                    return;
            }

            if (command == "quote" || command == "quotes")
            {
                if (argCount > 0 && e.Command.ChatMessage.IsModerator)
                {
                    await QuoteCommand.AddQuoteAsync(sender, e).ConfigureAwait(false);
                    return;
                }

                await QuoteCommand.GetQuoteAsync(sender, e, rng).ConfigureAwait(false);
                return;
            }

            if (command == "slothy" || command == "slothies")
            {
                if (argCount == 0)
                {
                    await GetSlothies(e).ConfigureAwait(false);
                    return;
                }

                if (argCount > 0 && string.Equals(e.Command.ArgumentsAsList[0], "help", StringComparison.OrdinalIgnoreCase))
                {
                    client.SendMessage(e.GetChannel(client), "Slothies are a made-up points system. " +
                        "They give you nothing other than bragging rights. Use !slothies to check how " +
                        $"many you have. {Utils.RandEmote()}");

                    return;
                }

                if (argCount == 2)
                {
                    await UpdateSlothies(e).ConfigureAwait(false);
                    return;
                }

                client.SendMessage(e.GetChannel(client), $"That's not a valid slothies command, you nerd. {Utils.RandEmote()}");
                return;
            }

            if (command == "multi" || command == "multitwitch")
            {
                if (argCount == 0)
                {
                    await MultitwitchCommand.GetMultitwitch(sender, e).ConfigureAwait(false);
                    return;
                }

                if (argCount > 0 && string.Equals(e.Command.ArgumentsAsList[0], "help", StringComparison.OrdinalIgnoreCase))
                {
                    if (!e.Command.ChatMessage.IsModerator)
                    {
                        client.SendMessage(e.GetChannel(client), "See who the nerd is playing with and " +
                            $"watch them all together using !multi. {Utils.RandEmote()}");
                    }
                    else
                    {
                        client.SendMessage(e.GetChannel(client), $"{Utils.RandEmote()} Mods: Clear the multi " +
                            "link using !multi clear. " +
                            "Set streamers by providing a list. For example: !multi gmart strippin");
                    }

                    return;
                }

                if (argCount > 0
                    && !string.Equals(e.Command.ArgumentsAsList[0], "help", StringComparison.CurrentCultureIgnoreCase)
                    && e.Command.ChatMessage.IsModerator)
                {
                    await MultitwitchCommand.UpdateMultitwitch(sender, e, api).ConfigureAwait(false);
                    return;
                }
            }
        }

        private async Task GetSlothies(OnChatCommandReceivedArgs e)
        {
            var channel = e.GetChannel(client);
            var count = 0M;

            using (var db = new VbContext())
            {
                var record = await db.Slothies.FindAsync(e.Command.ChatMessage.UserId).ConfigureAwait(false);
                if (record != null)
                    count = record.Count;
            }

            client.SendMessage(
                channel, $"{e.Command.ChatMessage.DisplayName} has {count.ToDisplayString()}. {Utils.RandEmote()}");
        }

        private async Task UpdateSlothies(OnChatCommandReceivedArgs e)
        {
            var channel = e.GetChannel(client);
            var origUsername = e.Command.ArgumentsAsList[0].TrimStart('@');

            var username = origUsername.ToLower();
            if (username.Length >= 200)
            {
                client.SendMessage(channel, $"That's not a valid user, you nerd. {Utils.RandEmote()}");
                return;
            }

            var users = await api.Users.v5.GetUserByNameAsync(username).ConfigureAwait(false);
            if (users.Total != 1)
            {
                client.SendMessage(channel, $"That's not a valid user, you nerd. {Utils.RandEmote()}");
                return;
            }

            var userId = users.Matches[0].Id;

            if (userId == e.Command.ChatMessage.UserId)
            {
                client.SendMessage(channel, $"You can't change your own slothies, you nerd. {Utils.RandEmote()}");
                return;
            }

            if (userId == "45447900")
            {
                client.SendMessage(channel, $"vaindil's slothies can't be edited, you nerd. {Utils.RandEmote()}");
                return;
            }

            var validDecimal = decimal.TryParse(e.Command.ArgumentsAsList[1], out var count);
            if (!validDecimal)
            {
                client.SendMessage(channel, $"That's not a valid number, you nerd. {Utils.RandEmote()}");
                return;
            }

            count = Math.Round(count, 2);

            if (!e.Command.ChatMessage.IsModerator)
            {
                client.SendMessage(channel, $"You're not a mod, you nerd. {Utils.RandEmote()}");
                return;
            }

            using (var db = new VbContext())
            {
                var record = await db.Slothies.FindAsync(userId).ConfigureAwait(false);
                if (record == null)
                {
                    var newRecord = new SlothyRecord
                    {
                        UserId = userId,
                        Count = count
                    };

                    db.Slothies.Add(newRecord);
                }
                else
                {
                    record.Count += count;
                    count = record.Count;

                    if (count == 0)
                    {
                        db.Slothies.Remove(record);
                    }
                }

                await db.SaveChangesAsync().ConfigureAwait(false);
            }

            client.SendMessage(channel, $"{origUsername} now has {count.ToDisplayString()}. {Utils.RandEmote()}");
        }

        private void SlothFacts(JoinedChannel channel)
        {
            var i = rng.Next(0, _slothFacts.Count);

            client.SendMessage(channel, _slothFacts[i]);
        }

        private async Task WoppyWeather(OnChatCommandReceivedArgs e)
        {
            var channel = e.GetChannel(client);

            if (e.Command.ArgumentsAsList.Count > 0
                && string.Equals(e.Command.ArgumentsAsList[0], "help", StringComparison.OrdinalIgnoreCase))
            {
                client.SendMessage(channel, "Woppy the weather bot can get your current weather. " +
                    "Use !woppy followed by a US zip code, for example !woppy 90210. Only works " +
                    $"in the US for now! {Utils.RandEmote()}");

                return;
            }

            if (!validZip.IsMatch(e.Command.ArgumentsAsString))
            {
                client.SendMessage(channel, $"That's not a valid US zip code. Try again! {Utils.RandEmote()}");
                return;
            }

            var response = await httpClient
                .GetAsync("http://api.openweathermap.org/data/2.5/weather?" +
                    $"zip={e.Command.ArgumentsAsString},us&APPID={config["openWeatherMapApiKey"]}")
                .ConfigureAwait(false);

            var respString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("Weather error, code " + response.StatusCode.ToString());
                Console.WriteLine("Weather content: " + respString);

                client.SendMessage(channel, $"Error getting the weather. IT'S THEIR FAULT, NOT MINE! {Utils.RandEmote()}");
                return;
            }

            var weather = JsonConvert.DeserializeObject<OpenWeatherMapResponse>(respString);

            var temp = (int)Math.Round(((9 / 5) * (weather.Main.Temperature - 273)) + 32);

            client.SendMessage(channel, $"WOPPY ACTIVATED! Weather for {e.Command.ArgumentsAsString}: " +
                $"{weather.Weather[0].Description}, {temp}° F {Utils.RandEmote()}");
        }

        private static readonly List<string> _slothFacts = new List<string>
        {
            "Sloths can sometimes maintain their grasp on limbs after death.",
            "Both two-toed and three-toed sloths grow to 1.5 to 2 feet long.",
            "The digestion process can take as long as a month to complete for an adult sloth!",
            "Sloths can rotate their heads around 270 degrees!",
            "Sloths have very slow metabolisms for creatures their size. This is why they can survive with leaves as their main source of food.",
            "Sloths are only about 25% muscle. They can't shiver if they get too cold!",
            "As much as 2/3 of a well-fed sloth's weight can be contained within its stomach chambers.",
            "Sloths mate and give birth while hanging in trees!",
            "Three-toed sloths have short stubby tails while two-toed sloths don't.",
            "To help conserve energy, a sloth's internal body temperature is only 30-34° C. It can drop even lower while they sleep.",
            "A sloth's laziness is actually a great method of survival. Its slow movement and camouflage helps it evade detection from natural predators.",
            "Sloths are sturdy! They are usually unharmed from falls.",
            "A sloth's maximum weight is about 40 pounds. Nearly two-thirds of this weight can be contained in the sloth's stomach compartments if it is well-fed.",
            "Three-toed sloths use their short tail to dig a hole and bury their poops! 💩",
            "The sloth can tolerate the largest change in body temperature of any mammal, from 74 to 92° F!",
            "Sloths only have one baby at a time.",
            "The outer hairs on a sloth actually grow in the opposite direction compared to other mammals.",
            "Sloths' internal organs are fixed to their ribcage. This prevents their lungs from being compressed while hanging upside down!",
            "There are two different families of sloths: Megalonychidae (two-toed) and Bradypodidae (three-toed).",
            "Sloths are the world's slowest-digesting mammal, only defecating once a week!",
            "Up until about 10,000 years ago, several species of ground sloths existed, such as Megatherium. This species grew to about the size of an elephant!",
            "Sloths are actually excellent swimmers! While in water they can slow their heart rate down to one-third its average pace. They can also move about three times faster in water with their version of the doggy paddle!",
            "The sloth is the world's slowest mammal!",
            "Sloths don't sweat and don't emit body odor. This helps avoid predation.",
            "Three-toed sloths have a maximum land speed of about 2 meters per minute!",
            "Sloths are excellent survivors. Of the five species of sloth, only one is currently endangered: the Maned Three-Toed Sloth.",
            "Healthy sloths generally live from 10 to 16 years in the wild. In captivity they can live to be over 30!",
            "A sloth can hold its breath for up to 40 minutes while in water!",
            "Despite the name, two-toed sloths actually have three toes. They only have two fingers though and they generally move a bit quicker than three-toed sloths.",
            "Sloths sometimes fatally mistake powerlines for trees. BibleThump",
            "Sloths tend to prefer the leaves of the Cecropia tree, sometimes known as pumpwoods."
        };
    }
}